using System;
using System.Numerics;
using System.IO;
using Dalamud.Bindings.ImGui;
using AsMobPlate.Overlay;

namespace AsMobPlate.Windows;

public sealed class ConfigWindow
{
    private readonly Configuration configuration;
    private readonly DebugLog debugLog;

    public bool IsOpen;

    public ConfigWindow(Configuration configuration, DebugLog debugLog)
    {
        this.configuration = configuration;
        this.debugLog = debugLog;
    }

    public void Draw()
    {
        if (!this.IsOpen)
            return;

        if (!ImGui.Begin("AS Mob Plate Settings", ref this.IsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var changed = false;
        changed |= ImGui.Checkbox("Show A rank", ref this.configuration.ShowARank);
        changed |= ImGui.Checkbox("Show S rank", ref this.configuration.ShowSRank);
        ImGui.Separator();
        changed |= ImGui.Checkbox("Show HP bar", ref this.configuration.ShowHpBar);
        changed |= ImGui.Checkbox("Show HP percent", ref this.configuration.ShowHpPercent);
        changed |= ImGui.Checkbox("Show distance", ref this.configuration.ShowDistance);
        changed |= ImGui.Checkbox("Show time to kill", ref this.configuration.ShowTimeToKill);
        changed |= ImGui.Checkbox("Show announced start time", ref this.configuration.ShowAnnouncedStartTime);
        changed |= ImGui.Checkbox("Show ET at announcement", ref this.configuration.ShowEtAtAnnouncement);
        changed |= ImGui.Checkbox("Show in-progress label", ref this.configuration.ShowInProgressLabel);
        changed |= ImGui.Checkbox("Show countdown frame", ref this.configuration.ShowCountdownFrame);
        changed |= SliderFloat("Countdown frame thickness", ref this.configuration.CountdownFrameThickness, 1.0f, 10.0f, "%.1f px");
        changed |= ImGui.Checkbox("Start countdown before announced time", ref this.configuration.EnableCountdownOnAnnouncedStart);
        changed |= ImGui.Checkbox("Show ObjectIndex", ref this.configuration.ShowObjectIndex);
        ImGui.Separator();
        changed |= ImGui.Checkbox("Play FFXIV sound on detection", ref this.configuration.EnableNotificationSound);
        changed |= ImGui.Checkbox("Place map flag on detection", ref this.configuration.EnableMapFlagOnDetection);
        changed |= SliderInt("Sound Effect Id", ref this.configuration.SoundEffectId, 0, 99);
        changed |= SliderInt("Sound Repeat Count", ref this.configuration.SoundRepeatCount, 1, 20);
        changed |= SliderFloat("Sound Repeat Interval", ref this.configuration.SoundRepeatIntervalSeconds, 0.05f, 2.0f, "%.2f sec");
        changed |= ImGui.Checkbox("Speak TTS on detection", ref this.configuration.EnableTts);
        changed |= ImGui.Checkbox("Print chat notification", ref this.configuration.EnableChatNotification);
        changed |= SliderFloat("Notification cooldown", ref this.configuration.NotificationCooldownSeconds, 0.0f, 300.0f, "%.0f sec");
        changed |= SliderInt("TTS Volume", ref this.configuration.TtsVolume, 0, 100);
        changed |= SliderInt("TTS Rate", ref this.configuration.TtsRate, -10, 10);
        changed |= ImGui.InputText("TTS Format", ref this.configuration.TtsFormat, 128);
        ImGui.Separator();
        changed |= SliderFloat("Max display distance", ref this.configuration.MaxDistance, 20.0f, 2000.0f, "%.0f yalms");
        changed |= SliderFloat("A rank notification distance", ref this.configuration.ARankNotificationDistance, 20.0f, 1000.0f, "%.0f yalms");
        changed |= SliderFloat("S rank notification distance", ref this.configuration.SRankNotificationDistance, 20.0f, 2000.0f, "%.0f yalms");
        changed |= SliderFloat("Time to kill sample window", ref this.configuration.TtkSampleWindowSeconds, 10.0f, 180.0f, "%.0f sec");
        changed |= SliderFloat("ET announcement window", ref this.configuration.EtStartPastToleranceMinutes, 0.0f, 240.0f, "+/- %.0f ET min");
        changed |= SliderFloat("Start time display after pull", ref this.configuration.StartTimeDisplayAfterSeconds, 0.0f, 1800.0f, "%.0f sec");
        changed |= SliderInt("Countdown seconds", ref this.configuration.CountdownSeconds, 5, 30);
        changed |= SliderFloat("Scale", ref this.configuration.Scale, 0.5f, 2.5f, "%.2f");
        changed |= SliderFloat("Y Offset", ref this.configuration.YOffset, -10.0f, 20.0f, "%.1f");
        changed |= SliderFloat("Name Font Size", ref this.configuration.NameFontSize, 8.0f, 36.0f, "%.0f");
        changed |= SliderFloat("HP Bar Width", ref this.configuration.HpBarWidth, 40.0f, 360.0f, "%.0f");
        changed |= SliderFloat("HP Bar Height", ref this.configuration.HpBarHeight, 4.0f, 48.0f, "%.0f");
        ImGui.Separator();
        changed |= ColorEdit("A rank text color", ref this.configuration.ARankTextColor);
        changed |= ColorEdit("S rank text color", ref this.configuration.SRankTextColor);
        changed |= ColorEdit("HP bar color", ref this.configuration.HpBarColor);
        changed |= ColorEdit("Background color", ref this.configuration.BackgroundColor);
        changed |= ColorEdit("In-progress background color", ref this.configuration.InProgressBackgroundColor);
        changed |= ColorEdit("Countdown frame color", ref this.configuration.CountdownFrameColor);
        ImGui.Separator();
        this.DrawDebugLogControls();

        if (changed)
            this.configuration.Save();

        ImGui.End();
    }

    private static bool SliderFloat(string label, ref float value, float min, float max, string format)
    {
        return ImGui.SliderFloat(label, ref value, min, max, format);
    }

    private static bool SliderInt(string label, ref int value, int min, int max)
    {
        return ImGui.SliderInt(label, ref value, min, max);
    }

    private static bool ColorEdit(string label, ref Vector4 color)
    {
        return ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
    }

    private void DrawDebugLogControls()
    {
        ImGui.TextUnformatted("Debug log");
        ImGui.TextUnformatted(this.debugLog.ClockStatus);
        if (ImGui.Button("Copy debug log"))
            ImGui.SetClipboardText(this.debugLog.Dump());

        ImGui.SameLine();
        if (ImGui.Button("Dump debug log"))
            File.WriteAllText(GetDebugDumpPath(), this.debugLog.Dump());

        ImGui.SameLine();
        if (ImGui.Button("Clear debug log"))
            this.debugLog.Clear();

        var dump = this.debugLog.Dump();
        ImGui.InputTextMultiline("##debug-log", ref dump, 12000, new Vector2(760.0f, 180.0f), ImGuiInputTextFlags.ReadOnly);
        ImGui.TextUnformatted(GetDebugDumpPath());
    }

    private static string GetDebugDumpPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "devPlugins",
            "AsMobPlate",
            "debug-log.txt");
    }
}
