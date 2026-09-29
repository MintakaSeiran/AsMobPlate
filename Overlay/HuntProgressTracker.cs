using System;
using System.Diagnostics;
using System.Numerics;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LogMessage = Lumina.Excel.Sheets.LogMessage;

namespace AsMobPlate.Overlay;

public sealed class HuntProgressTracker : IDisposable
{
    private readonly Configuration config;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private readonly IChatGui chat;
    private readonly ICondition condition;
    private readonly HuntMarkRegistry registry;
    private readonly DebugLog log;
    private readonly Action clearStartTime;
    private readonly Action notifySsTrigger;
    private readonly SsSystemMessages systemMessages;
    private (uint Territory, uint World, uint Instance) context;
    private bool disposed;
    private bool suspended = true;
    private double nextError;
    private double reportUntil;
    private int generation;
    public HuntProgress Progress { get; } = new();
    public static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    public HuntProgressTracker(Configuration config, IClientState client, IObjectTable objects,
        IFramework framework, IChatGui chat, ICondition condition, IDataManager data,
        HuntMarkRegistry registry, DebugLog log, Action clearStartTime, Action notifySsTrigger)
    {
        this.config = config;
        this.client = client;
        this.objects = objects;
        this.framework = framework;
        this.chat = chat;
        this.condition = condition;
        this.registry = registry;
        this.log = log;
        this.clearStartTime = clearStartTime;
        this.notifySsTrigger = notifySsTrigger;
        // LogMessage 9332/9334 use LogKind 57; ACT's 0839 includes source flags.
        var sheet = data.GetExcelSheet<LogMessage>();
        this.systemMessages = new SsSystemMessages(sheet.GetRow(9332).Text.ToString(), sheet.GetRow(9334).Text.ToString());
        this.framework.Update += this.Update;
        this.chat.ChatMessage += this.OnChat;
        this.client.TerritoryChanged += this.OnTerritory;
    }

    public void Dispose()
    {
        this.disposed = true;
        this.framework.Update -= this.Update;
        this.chat.ChatMessage -= this.OnChat;
        this.client.TerritoryChanged -= this.OnTerritory;
    }

    public bool IsShown(HuntRank rank) => rank switch
    {
        HuntRank.A => this.config.ShowARank,
        HuntRank.S => this.config.ShowSRank,
        HuntRank.Minion => this.config.ShowSsMinions,
        HuntRank.SS => this.config.ShowSsBoss,
        _ => false,
    };

    private void Reset()
    {
        this.generation++;
        this.Progress.Clear();
        this.reportUntil = 0;
        this.clearStartTime();
    }

    private void OnTerritory(uint _) { this.Reset(); this.suspended = true; }

    private unsafe bool RefreshContext()
    {
        if (!this.client.IsLoggedIn || this.objects.LocalPlayer == null
            || this.condition[ConditionFlag.BetweenAreas] || this.condition[ConditionFlag.BetweenAreas51])
        {
            if (!this.suspended) this.Reset();
            this.suspended = true;
            return false;
        }
        var state = UIState.Instance();
        var current = ((uint)this.client.TerritoryType, this.objects.LocalPlayer.CurrentWorld.RowId,
            state == null ? 0u : state->PublicInstance.InstanceId);
        if (this.suspended || current != this.context)
        {
            this.Reset();
            this.context = current;
        }
        this.suspended = false;
        return true;
    }

    private void Update(IFramework _)
    {
        if (this.disposed) return;
        var now = Now;
        try
        {
            if (!this.RefreshContext()) return;
            var before = this.Progress.Stage;
            var bossId = HuntMarkRegistry.SsNameId(this.context.Territory);
            foreach (var obj in this.objects)
            {
                if (obj is not IBattleNpc npc || npc.MaxHp == 0) continue;
                var rank = this.registry.GetRank(npc.NameId);
                if (rank == HuntRank.None) continue;
                var id = npc.GameObjectId;
                if (id == 0 || id == 0xE0000000) continue;
                var previous = this.Progress.Find(id);
                var dead = npc.CurrentHp == 0 || npc.IsDead;
                if (dead && previous?.DiedAt == null)
                    this.log.Add($"hunt defeated: nameId={npc.NameId}, object={id:X}, observed HP={npc.CurrentHp}");
                this.Progress.Observe(id, npc.NameId, npc.ObjectIndex,
                    previous?.Name ?? npc.Name.ToString(), rank, npc.Position, npc.HitboxRadius,
                    dead, (npc.StatusFlags & StatusFlags.InCombat) != 0,
                    bossId != 0 && (npc.NameId == bossId || npc.NameId == bossId + 1), now);
                if (!dead)
                    this.Progress.Find(id)?.Combat.Update(npc.CurrentHp, npc.MaxHp,
                        (npc.StatusFlags & StatusFlags.InCombat) != 0, now);
            }
            this.Progress.ResolveSsTrigger(now, this.DeadDuration);
            this.Progress.Prune(now, this.DeadDuration);
            if (before != this.Progress.Stage)
                this.log.Add($"SS stage: {before} -> {this.Progress.Stage}; observed kills={this.Progress.ObservedKills}");
        }
        catch (Exception ex)
        {
            if (now < this.nextError) return;
            this.nextError = now + 30;
            this.log.Add($"hunt tracking failed: {ex.Message}");
        }
    }

    private void OnChat(IHandleableChatMessage message)
    {
        var kind = (int)message.LogKind;
        if (kind is not (57 or 10 or 11 or 30)) return;
        var text = message.Message.ToString().Trim();
        var systemEvent = this.systemMessages.Match(kind, text);
        var start = systemEvent == SsSystemEvent.Start;
        var returned = systemEvent == SsSystemEvent.Returned;
        var report = kind != 57 && (text.Equals("END", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith("END", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Sモブ終了", StringComparison.Ordinal)
            || text.Contains("Sモブ没", StringComparison.Ordinal));
        if (!start && !returned && !report) return;
        // Do not retain the event's message object or apply messages after zoning.
        var territory = this.client.TerritoryType;
        var receivedGeneration = this.generation;
        void Apply()
        {
            if (this.disposed || territory != this.client.TerritoryType || !this.RefreshContext()
                || receivedGeneration != this.generation || !HuntMarkRegistry.IsSsTerritory(this.context.Territory)) return;
            if (start && this.Progress.Start(Now, this.DeadDuration))
            {
                this.clearStartTime();
                this.notifySsTrigger();
                this.log.Add("SS trigger alert requested: 5 sounds (respects notification sound setting)");
            }
            if (returned) this.Progress.Return(Now);
            if (report && this.Progress.Stage != HuntStage.None) this.reportUntil = Now + 30;
            if (start || returned) this.log.Add($"SS system message: {(start ? "start" : "return")}; territory={territory}");
        }
        if (this.framework.IsInFrameworkUpdateThread) Apply();
        else _ = this.framework.RunOnFrameworkThread(Apply);
    }

    public double DeadDuration => float.IsFinite(this.config.DefeatedDisplaySeconds)
        ? Math.Clamp(this.config.DefeatedDisplaySeconds, 1, 300) : 30;

    public void Draw(IGameGui gameGui, NameplatePainter painter, Vector3 player)
    {
        if (this.suspended) return;
        var now = Now;
        if (this.config.ShowDefeated)
            foreach (var item in this.Progress.Observations)
            {
                if (item.DiedAt is not double died || now - died >= this.DeadDuration || !this.IsShown(item.Rank)) continue;
                var distance = Vector3.Distance(player, item.Position);
                if (distance > this.config.MaxDistance) continue;
                var anchor = item.Position + new Vector3(0, MathF.Max(2, item.Radius * 1.5f) + this.config.YOffset, 0);
                if (!gameGui.WorldToScreen(anchor, out var screen)) continue;
                painter.Draw(ImGui.GetForegroundDrawList(), screen, new NameplateData(item.Rank, item.Name,
                    item.ObjectIndex, 0, distance, null,
                    UiText.Format("Defeated elapsed", this.config.Language, (int)(now - died)), false, 0, 10, null, true,
                    SsTriggered: item.SsTriggered));
            }
        var stage = this.Progress.Stage;
        if (!this.config.ShowHuntProgress || stage == HuntStage.None
            || (stage is HuntStage.Defeated or HuntStage.Returned && now - this.Progress.ChangedAt >= this.DeadDuration)) return;
        var viewport = ImGui.GetMainViewport();
        var offset = this.config.HuntProgressPosition;
        offset.X = float.IsFinite(offset.X) ? Math.Clamp(offset.X, 0, Math.Max(0, viewport.Size.X - 320)) : 24;
        offset.Y = float.IsFinite(offset.Y) ? Math.Clamp(offset.Y, 0, Math.Max(0, viewport.Size.Y - 150)) : 180;
        ImGui.SetNextWindowPos(viewport.Pos + offset, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Math.Min(320, viewport.Size.X), 0));
        ImGui.SetNextWindowBgAlpha(0.85f);
        if (ImGui.Begin("###ASMobPlateProgress", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing))
        {
            ImGui.TextWrapped(UiText.Get("Stage " + stage, this.config.Language));
            if (stage is HuntStage.Searching or HuntStage.Fighting)
            {
                ImGui.TextWrapped(UiText.Format("Observed kills", this.config.Language, this.Progress.ObservedKills));
                // Five-minute engagement guidance is verified only for ShB/EW.
                if (this.Progress.StartedAt is double began && this.context.Territory < 1187)
                {
                    var remaining = Math.Max(0, 300 - (now - began));
                    ImGui.TextWrapped(remaining > 0
                        ? UiText.Format("Engagement guide", this.config.Language, (int)Math.Ceiling(remaining))
                        : UiText.Get("Awaiting confirmation", this.config.Language));
                }
            }
            if (now < this.reportUntil) ImGui.TextWrapped(UiText.Get("Completion reported", this.config.Language));
        }
        ImGui.End();
    }
}
