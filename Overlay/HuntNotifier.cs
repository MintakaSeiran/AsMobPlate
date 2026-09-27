using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;

namespace AsMobPlate.Overlay;

public sealed class HuntNotifier : IDisposable
{
    private readonly Configuration configuration;
    private readonly IChatGui chatGui;
    private readonly IPluginLog pluginLog;
    private readonly Dictionary<ulong, DateTime> lastNotifiedAt = new();
    private readonly HashSet<ulong> visibleNow = new();
    private readonly HashSet<ulong> visibleThisFrame = new();
    private readonly SpeechSynthesizer? speechSynthesizer;
    private int remainingSoundRepeats;
    private DateTime nextSoundAt = DateTime.MinValue;

    public HuntNotifier(Configuration configuration, IChatGui chatGui, IPluginLog pluginLog)
    {
        this.configuration = configuration;
        this.chatGui = chatGui;
        this.pluginLog = pluginLog;

        try
        {
            this.speechSynthesizer = new SpeechSynthesizer();
        }
        catch (Exception ex)
        {
            this.pluginLog.Warning(ex, "Could not initialize Windows TTS.");
        }
    }

    public void BeginFrame()
    {
        this.visibleThisFrame.Clear();
        this.ProcessSoundQueue();
    }

    public void Seen(IBattleNpc npc, HuntRank rank)
    {
        var key = GetObjectKey(npc);
        this.visibleThisFrame.Add(key);

        if (this.visibleNow.Contains(key))
            return;

        this.visibleNow.Add(key);

        var now = DateTime.UtcNow;
        if (this.lastNotifiedAt.TryGetValue(key, out var previous) &&
            (now - previous).TotalSeconds < Math.Max(0.0f, this.configuration.NotificationCooldownSeconds))
            return;

        this.lastNotifiedAt[key] = now;
        this.Notify(npc, rank);
    }

    public void EndFrame()
    {
        this.visibleNow.RemoveWhere(key => !this.visibleThisFrame.Contains(key));
    }

    public void Dispose()
    {
        this.speechSynthesizer?.Dispose();
    }

    private void Notify(IBattleNpc npc, HuntRank rank)
    {
        var rankText = rank == HuntRank.A ? "A" : "S";
        var name = npc.Name.ToString();
        var message = this.configuration.TtsFormat
            .Replace("{rank}", rankText, StringComparison.OrdinalIgnoreCase)
            .Replace("{name}", name, StringComparison.OrdinalIgnoreCase);

        if (this.configuration.EnableNotificationSound)
            this.QueueGameSound();

        if (this.configuration.EnableChatNotification)
            this.chatGui.Print(UiText.Format("Hunt detected", this.configuration.Language, rankText, name), "AS Mob Plate");

        if (!this.configuration.EnableTts || this.speechSynthesizer == null)
            return;

        try
        {
            this.speechSynthesizer.Volume = Math.Clamp(this.configuration.TtsVolume, 0, 100);
            this.speechSynthesizer.Rate = Math.Clamp(this.configuration.TtsRate, -10, 10);
            this.speechSynthesizer.SpeakAsyncCancelAll();
            this.speechSynthesizer.SpeakAsync(message);
        }
        catch (Exception ex)
        {
            this.pluginLog.Warning(ex, "Failed to play AS hunt TTS.");
        }
    }

    private void QueueGameSound()
    {
        this.remainingSoundRepeats = Math.Clamp(this.configuration.SoundRepeatCount, 1, 20);
        this.nextSoundAt = DateTime.UtcNow;
        this.ProcessSoundQueue();
    }

    private void ProcessSoundQueue()
    {
        if (this.remainingSoundRepeats <= 0)
            return;

        var now = DateTime.UtcNow;
        if (now < this.nextSoundAt)
            return;

        try
        {
            UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(this.configuration.SoundEffectId, 0, 99));
        }
        catch (Exception ex)
        {
            this.remainingSoundRepeats = 0;
            this.pluginLog.Warning(ex, "Failed to play AS hunt game sound effect.");
            return;
        }

        this.remainingSoundRepeats--;
        this.nextSoundAt = now.AddSeconds(Math.Max(0.05f, this.configuration.SoundRepeatIntervalSeconds));
    }

    private static ulong GetObjectKey(IBattleNpc npc)
    {
        return npc.GameObjectId != 0 ? npc.GameObjectId : ((ulong)npc.NameId << 32) | npc.ObjectIndex;
    }
}
