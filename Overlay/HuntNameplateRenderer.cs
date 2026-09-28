using System;
using System.Numerics;
using System.Diagnostics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using AsMobPlate.Hunts;

namespace AsMobPlate.Overlay;

public sealed class HuntNameplateRenderer : IDisposable
{
    private readonly Configuration configuration;
    private readonly NameplatePainter painter;
    private readonly HuntMarkRegistry registry;
    private readonly IObjectTable objectTable;
    private readonly IGameGui gameGui;
    private readonly IPluginLog pluginLog;
    private readonly HuntNotifier notifier;
    private readonly HuntMapFlagger mapFlagger;
    private readonly AnnouncedStartTimeTracker announcedStartTimeTracker;
    private readonly HpTracker hpTracker = new();
    private readonly ArrivalEstimator arrivalEstimator = new();
    private readonly HuntProgressTracker progressTracker;

    public DebugLog DebugLog { get; } = new();

    public HuntNameplateRenderer(
        Configuration configuration,
        HuntMarkRegistry registry,
        IObjectTable objectTable,
        IGameGui gameGui,
        IClientState clientState,
        IChatGui chatGui,
        IFramework framework,
        IPluginLog pluginLog,
        ICondition condition,
        IDataManager dataManager)
    {
        this.configuration = configuration;
        this.painter = new NameplatePainter(configuration);
        this.registry = registry;
        this.objectTable = objectTable;
        this.gameGui = gameGui;
        this.pluginLog = pluginLog;
        this.notifier = new HuntNotifier(configuration, chatGui, pluginLog);
        this.mapFlagger = new HuntMapFlagger(configuration, gameGui, clientState, pluginLog);
        this.announcedStartTimeTracker = new AnnouncedStartTimeTracker(configuration, chatGui, framework, clientState, pluginLog, this.DebugLog);
        this.progressTracker = new HuntProgressTracker(configuration, clientState, objectTable, framework,
            chatGui, condition, dataManager, registry, this.DebugLog,
            () => this.announcedStartTimeTracker.Clear("hunt context or SS event changed"));
    }

    public void Draw()
    {
        this.notifier.BeginFrame();
        this.mapFlagger.BeginFrame();
        this.hpTracker.BeginFrame();

        try
        {
            var localPlayer = this.objectTable.LocalPlayer;
            if (localPlayer == null)
            {
                this.arrivalEstimator.Reset();
                return;
            }
            this.arrivalEstimator.Update(localPlayer.Position, (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

            var maxDisplayDistanceSq = this.configuration.MaxDistance * this.configuration.MaxDistance;
            var aNotificationDistanceSq = this.configuration.ARankNotificationDistance * this.configuration.ARankNotificationDistance;
            var sNotificationDistanceSq = this.configuration.SRankNotificationDistance * this.configuration.SRankNotificationDistance;

            foreach (var obj in this.objectTable)
            {
                if (obj is not IBattleNpc npc)
                    continue;

                var rank = this.registry.GetRank(npc.NameId);
                if (rank == HuntRank.None)
                    continue;

                if (!this.progressTracker.IsShown(rank))
                    continue;

                if (npc.MaxHp == 0 || npc.CurrentHp == 0 || npc.IsDead)
                    continue;

                var distanceSq = Vector3.DistanceSquared(localPlayer.Position, npc.Position);
                var notificationDistanceSq = rank == HuntRank.A ? aNotificationDistanceSq : sNotificationDistanceSq;
                if (distanceSq <= notificationDistanceSq)
                {
                    this.notifier.Seen(npc, rank);
                    this.mapFlagger.Seen(npc);
                }

                var distance = MathF.Sqrt(distanceSq);
                var timeToKill = this.hpTracker.Update(npc, this.configuration.TtkSampleWindowSeconds);

                if (distanceSq <= maxDisplayDistanceSq)
                {
                    var arrival = (npc.StatusFlags & StatusFlags.InCombat) != 0
                        ? this.arrivalEstimator.Estimate(localPlayer.Position, npc.Position, this.configuration.ArrivalDistance, this.configuration.ArrivalPreparationSeconds)
                        : null;
                    this.DrawNameplate(npc, rank, distance, timeToKill, arrival);
                }
            }
            this.progressTracker.Draw(this.gameGui, this.painter, localPlayer.Position);
        }
        catch (Exception ex)
        {
            this.pluginLog.Warning(ex, "Failed to draw AS hunt nameplates.");
        }
        finally
        {
            this.hpTracker.EndFrame();
            this.mapFlagger.EndFrame();
            this.notifier.EndFrame();
        }
    }

    public void Dispose()
    {
        this.progressTracker.Dispose();
        this.announcedStartTimeTracker.Dispose();
        this.notifier.Dispose();
    }

    private void DrawNameplate(IBattleNpc npc, HuntRank rank, float distance, TimeSpan? timeToKill, double? arrival)
    {
        var worldPosition = npc.Position + new Vector3(0.0f, MathF.Max(2.0f, npc.HitboxRadius * 1.5f) + this.configuration.YOffset, 0.0f);
        if (!this.gameGui.WorldToScreen(worldPosition, out var screenPosition))
            return;

        var arrivalWarning = this.configuration.ShowArrivalWarning
            && this.progressTracker.Progress.LatchArrivalWarning(npc.GameObjectId, npc.NameId, arrival, timeToKill);

        var hasStart = this.announcedStartTimeTracker.TryGetDisplayText(out var startText, out var inProgress, out var remaining);
        if (rank == HuntRank.Minion)
        {
            hasStart = false;
            inProgress = (npc.StatusFlags & StatusFlags.InCombat) != 0;
            remaining = 0;
        }
        var data = new NameplateData(rank, npc.Name.ToString(), npc.ObjectIndex,
            (float)npc.CurrentHp / npc.MaxHp, distance, timeToKill,
            hasStart ? startText : string.Empty, inProgress, remaining, this.announcedStartTimeTracker.CountdownWindowSeconds, arrival,
            ArrivalWarning: arrivalWarning);
        this.painter.Draw(ImGui.GetForegroundDrawList(), screenPosition, data);
    }
}
