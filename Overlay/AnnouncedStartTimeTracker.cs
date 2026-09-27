using System;
using System.Diagnostics;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using AsMobPlate.Localization;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace AsMobPlate.Overlay;

// Timing specification and in-game verification steps: README.md, "Announced Start Time".
public sealed class AnnouncedStartTimeTracker : IDisposable
{
    private readonly Configuration configuration;
    private readonly IChatGui chatGui;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IPluginLog pluginLog;
    private readonly DebugLog debugLog;
    private readonly EorzeaStartSchedule schedule = new();

    private long currentEt;
    private uint announcementTerritory;
    private long nextClockStatusAt;
    private long nextSampleAt;
    private bool startLogged;
    private bool disposed;
    private string startEtText = string.Empty;
    private string announcedAtEtText = string.Empty;

    public AnnouncedStartTimeTracker(Configuration configuration, IChatGui chatGui, IFramework framework,
        IClientState clientState, IPluginLog pluginLog, DebugLog debugLog)
    {
        this.configuration = configuration;
        this.chatGui = chatGui;
        this.framework = framework;
        this.clientState = clientState;
        this.pluginLog = pluginLog;
        this.debugLog = debugLog;
        this.chatGui.ChatMessageUnhandled += this.OnChatMessage;
        this.framework.Update += this.OnFrameworkUpdate;
        this.debugLog.Add("timing v9: game ClientTime.EorzeaTime; 1 ET minute = 35/12 real seconds; no clock correction");
    }

    public int CountdownWindowSeconds => this.schedule.HasAnnouncement
        ? this.schedule.CountdownSeconds
        : Math.Clamp(this.configuration.CountdownSeconds, 5, 30);

    public void Dispose()
    {
        this.disposed = true;
        this.framework.Update -= this.OnFrameworkUpdate;
        this.chatGui.ChatMessageUnhandled -= this.OnChatMessage;
    }

    public bool TryGetDisplayText(out string text, out bool isInProgress, out double remainingSeconds)
    {
        text = string.Empty;
        isInProgress = false;
        remainingSeconds = 0.0;
        if (!this.configuration.ShowAnnouncedStartTime || !this.schedule.HasAnnouncement || this.currentEt <= 0)
            return false;

        remainingSeconds = this.schedule.GetRemainingSeconds(this.currentEt);
        isInProgress = remainingSeconds <= 0.0;
        var seconds = Math.Max(0, (int)Math.Ceiling(remainingSeconds));
        text = this.configuration.ShowEtAtAnnouncement
            ? UiText.Format("Announcement countdown", this.configuration.Language, this.announcedAtEtText, this.startEtText, seconds)
            : UiText.Format("Start countdown", this.configuration.Language, this.startEtText, seconds);
        return true;
    }

    private void OnChatMessage(IChatMessage message)
    {
        var text = message.Message.ToString();
        if (!AnnouncementTextParser.TryParseAnnouncement(text, out var hour, out var minute))
            return;

        // Copy chat data before scheduling: the callback's message is only valid during this event.
        var timestamp = message.Timestamp;
        if (this.framework.IsInFrameworkUpdateThread)
            this.CaptureAnnouncement(text, timestamp, hour * 60 + minute, false);
        else
            _ = this.framework.RunOnFrameworkThread(() => this.CaptureAnnouncement(text, timestamp, hour * 60 + minute, true));
    }

    private void CaptureAnnouncement(string text, int chatTimestamp, int targetMinute, bool deferred)
    {
        if (this.disposed)
            return;

        try
        {
            if (!this.clientState.IsLoggedIn || !TryReadGameTime(out var receivedEt))
            {
                this.debugLog.Add("announce ignored: game ET unavailable");
                return;
            }

            if (this.schedule.HasAnnouncement && this.announcementTerritory != this.clientState.TerritoryType)
                this.Clear("territory changed");

            var result = this.schedule.Set(receivedEt, targetMinute, this.configuration.EtStartPastToleranceMinutes,
                this.configuration.CountdownSeconds);
            if (result != AnnouncementResult.Accepted)
            {
                this.debugLog.Add($"announce ignored reason={result} gameET={EorzeaStartSchedule.FormatTime(receivedEt, true)} targetET={targetMinute / 60:00}:{targetMinute % 60:00} text=\"{SanitizeForLog(text)}\"");
                return;
            }

            this.currentEt = receivedEt;
            this.announcementTerritory = this.clientState.TerritoryType;
            this.startEtText = EorzeaStartSchedule.FormatTime(this.schedule.Target);
            this.announcedAtEtText = EorzeaStartSchedule.FormatTime(receivedEt);
            this.startLogged = false;
            this.nextSampleAt = Stopwatch.GetTimestamp() + 10 * Stopwatch.Frequency;
            var remaining = this.schedule.GetRemainingSeconds(receivedEt);
            var receivedLocal = DateTimeOffset.Now;
            this.debugLog.Add($"announce accepted source=game.ClientTime.EorzeaTime text=\"{SanitizeForLog(text)}\" chatTimestamp={chatTimestamp} deferred={deferred} receivedLocal={receivedLocal:O} rawET={receivedEt} receivedET={EorzeaStartSchedule.FormatTime(receivedEt, true)} targetRawET={this.schedule.Target} targetET={this.startEtText}:00 deltaETSeconds={this.schedule.Target - receivedEt} remainingRealSeconds={remaining:0.000} localStartEstimate={receivedLocal.AddSeconds(remaining):O} countdownArmed={!this.schedule.CountdownHandled}");
            if (this.schedule.CountdownHandled)
                this.debugLog.Add("countdown not armed: announcement arrived after the countdown trigger time");
        }
        catch (Exception ex)
        {
            this.debugLog.Add($"announce error: {ex.GetType().Name}: {ex.Message}");
            this.pluginLog.Warning(ex, "Failed to capture announced hunt start time.");
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (this.disposed)
            return;

        try
        {
            if (!this.clientState.IsLoggedIn || !TryReadGameTime(out var gameEt))
            {
                this.currentEt = 0;
                this.debugLog.ClockStatus = "Game ET: unavailable";
                this.Clear("logged out or game ET unavailable");
                return;
            }

            this.currentEt = gameEt;
            var tick = Stopwatch.GetTimestamp();
            if (tick >= this.nextClockStatusAt)
            {
                this.debugLog.ClockStatus = $"Game ET: {EorzeaStartSchedule.FormatTime(gameEt, true)}";
                this.nextClockStatusAt = tick + Stopwatch.Frequency / 10;
            }

            if (!this.schedule.HasAnnouncement)
                return;

            if (this.announcementTerritory != this.clientState.TerritoryType)
            {
                this.Clear("territory changed");
                return;
            }

            var remaining = this.schedule.GetRemainingSeconds(gameEt);
            if (remaining < -Math.Max(0.0f, this.configuration.StartTimeDisplayAfterSeconds))
            {
                this.Clear("start-display expired");
                return;
            }

            if (!this.startLogged && remaining <= 0.0)
            {
                this.startLogged = true;
                this.debugLog.Add($"start reached gameET={EorzeaStartSchedule.FormatTime(gameEt, true)} targetET={this.startEtText} remainingRealSeconds={remaining:0.000}");
            }

            if (tick >= this.nextSampleAt)
            {
                this.nextSampleAt = tick + 10 * Stopwatch.Frequency;
                this.debugLog.Add($"clock sample rawET={gameEt} gameET={EorzeaStartSchedule.FormatTime(gameEt, true)} targetET={this.startEtText} remainingRealSeconds={remaining:0.000}");
            }

            if (!this.configuration.EnableCountdownOnAnnouncedStart)
                return;

            var decision = this.schedule.TakeCountdown(gameEt);
            if (decision == CountdownDecision.SkippedLate)
                this.debugLog.Add($"countdown skipped: missed trigger window remainingRealSeconds={remaining:0.000}");
            else if (decision == CountdownDecision.Start)
                this.SendCountdown(remaining);
        }
        catch (Exception ex)
        {
            this.Clear("clock update failed");
            this.debugLog.Add($"clock error: {ex.GetType().Name}: {ex.Message}");
            this.pluginLog.Warning(ex, "Failed to update hunt start clock.");
        }
    }

    private static unsafe bool TryReadGameTime(out long eorzeaSeconds)
    {
        eorzeaSeconds = 0;
        var game = GameFramework.Instance();
        if (game == null || game->ClientTime.IsEorzeaTimeOverridden)
            return false;

        eorzeaSeconds = game->ClientTime.EorzeaTime;
        return eorzeaSeconds > 0;
    }

    private unsafe void SendCountdown(double remaining)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
        {
            this.debugLog.Add("countdown not submitted: UIModule unavailable");
            return;
        }

        var command = $"/countdown {this.schedule.CountdownSeconds}";
        var nativeCommand = Utf8String.FromString(command);
        try
        {
            // ICommandManager handles plugin commands, not the game's built-in /countdown.
            uiModule->ProcessChatBoxEntry(nativeCommand);
            this.debugLog.Add($"countdown submitted command=\"{command}\" gameET={EorzeaStartSchedule.FormatTime(this.currentEt, true)} remainingRealSeconds={remaining:0.000}; game acceptance not confirmed");
        }
        finally
        {
            nativeCommand->Dtor(true);
        }
    }

    private void Clear(string reason)
    {
        if (this.schedule.HasAnnouncement)
            this.debugLog.Add($"start cleared: {reason}");
        this.schedule.Clear();
        this.startEtText = string.Empty;
        this.announcedAtEtText = string.Empty;
        this.startLogged = false;
    }

    private static string SanitizeForLog(string text)
    {
        return text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
