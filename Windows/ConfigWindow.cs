using System;
using System.Numerics;
using System.IO;
using Dalamud.Bindings.ImGui;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;

namespace AsMobPlate.Windows;

public sealed class ConfigWindow
{
    private static readonly string[] Tabs = ["Display", "Notifications", "Timing", "Appearance", "Debug", "About"];
    private static readonly string[] Languages = ["EN", "JP", "DE", "FR"];
    private static readonly Version AssemblyVersion = typeof(ConfigWindow).Assembly.GetName().Version!;
    private static readonly string ReleaseVersion = $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build:000}";
    private readonly Configuration configuration;
    private readonly DebugLog debugLog;
    private readonly NameplatePreview preview;
    private string logSaveStatus = string.Empty;

    public bool IsOpen;

    public void OpenPreview()
    {
        this.IsOpen = true;
        this.preview.RequestOpen();
    }

    public ConfigWindow(Configuration configuration, DebugLog debugLog)
    {
        this.configuration = configuration;
        this.debugLog = debugLog;
        this.preview = new NameplatePreview(configuration);
    }

    public void Draw()
    {
        if (!this.IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(700, 560), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(520, 320), new Vector2(float.MaxValue, float.MaxValue));
        if (!ImGui.Begin($"AS Mob Plate v{ReleaseVersion}###ASMobPlateSettings", ref this.IsOpen))
        {
            ImGui.End();
            return;
        }

        var changed = false;
        try
        {
            ImGui.TextUnformatted(this.Text("Language"));
            for (var i = 0; i < Languages.Length; i++)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton(Languages[i], (int)this.configuration.Language == i))
                {
                    this.configuration.Language = (UiLanguage)i;
                    changed = true;
                }
            }

            ImGui.Separator();
            this.preview.Draw();
            if (ImGui.BeginTabBar("settings-tabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                for (var i = 0; i < Tabs.Length; i++)
                {
                    if (!ImGui.BeginTabItem(this.Label(Tabs[i])))
                        continue;

                    ImGui.PushID(Tabs[i]);
                    if (ImGui.BeginChild("tab-content", Vector2.Zero, false))
                    {
                        ImGui.PushItemWidth(-1);
                        switch (i)
                        {
                            case 0: changed |= this.DrawDisplay(); break;
                            case 1: changed |= this.DrawNotifications(); break;
                            case 2: changed |= this.DrawTiming(); break;
                            case 3: changed |= this.DrawAppearance(); break;
                            case 4: this.DrawDebugLogControls(); break;
                            case 5: this.DrawAbout(); break;
                        }
                        ImGui.PopItemWidth();
                    }
                    ImGui.EndChild();
                    ImGui.PopID();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            if (changed)
                this.configuration.Save();
        }
        finally
        {
            ImGui.End();
        }
    }

    private bool DrawDisplay()
    {
        var changed = this.Checkbox("Show A rank", ref this.configuration.ShowARank);
        changed |= this.Checkbox("Show S rank", ref this.configuration.ShowSRank);
        changed |= this.Checkbox("Show HP bar", ref this.configuration.ShowHpBar);
        changed |= this.Checkbox("Show HP percent", ref this.configuration.ShowHpPercent);
        changed |= this.Checkbox("Show distance", ref this.configuration.ShowDistance);
        changed |= this.Checkbox("Show time to kill", ref this.configuration.ShowTimeToKill);
        changed |= this.Checkbox("Show ObjectIndex", ref this.configuration.ShowObjectIndex);
        changed |= this.SliderFloat("Max display distance", ref this.configuration.MaxDistance, 20, 2000, this.Text("Distance unit"));
        changed |= this.SliderFloat("Time to kill sample window", ref this.configuration.TtkSampleWindowSeconds, 10, 180, this.Text("Seconds unit"));
        return changed;
    }

    private bool DrawNotifications()
    {
        var changed = this.Checkbox("Play FFXIV sound on detection", ref this.configuration.EnableNotificationSound);
        changed |= this.Checkbox("Place map flag on detection", ref this.configuration.EnableMapFlagOnDetection);
        changed |= this.Checkbox("Print chat notification", ref this.configuration.EnableChatNotification);
        changed |= this.SliderFloat("A rank notification distance", ref this.configuration.ARankNotificationDistance, 20, 1000, this.Text("Distance unit"));
        changed |= this.SliderFloat("S rank notification distance", ref this.configuration.SRankNotificationDistance, 20, 2000, this.Text("Distance unit"));
        changed |= this.SliderInt("Sound Effect Id", ref this.configuration.SoundEffectId, 0, 99);
        changed |= this.SliderInt("Sound Repeat Count", ref this.configuration.SoundRepeatCount, 1, 20);
        changed |= this.SliderFloat("Sound Repeat Interval", ref this.configuration.SoundRepeatIntervalSeconds, 0.05f, 2, this.Text("Interval unit"));
        changed |= this.SliderFloat("Notification cooldown", ref this.configuration.NotificationCooldownSeconds, 0, 300, this.Text("Seconds unit"));
        ImGui.Separator();
        changed |= this.Checkbox("Speak TTS on detection", ref this.configuration.EnableTts);
        changed |= this.SliderInt("TTS Volume", ref this.configuration.TtsVolume, 0, 100);
        changed |= this.SliderInt("TTS Rate", ref this.configuration.TtsRate, -10, 10);
        this.FieldTitle("TTS Format");
        changed |= ImGui.InputText("##TTS Format", ref this.configuration.TtsFormat, 128);
        return changed;
    }

    private bool DrawTiming()
    {
        var changed = this.Checkbox("Show announced start time", ref this.configuration.ShowAnnouncedStartTime);
        changed |= this.Checkbox("Show ET at announcement", ref this.configuration.ShowEtAtAnnouncement);
        changed |= this.Checkbox("Show in-progress label", ref this.configuration.ShowInProgressLabel);
        changed |= this.Checkbox("Show countdown frame", ref this.configuration.ShowCountdownFrame);
        changed |= this.Checkbox("Start countdown before announced time", ref this.configuration.EnableCountdownOnAnnouncedStart);
        changed |= this.SliderFloat("ET announcement window", ref this.configuration.EtStartPastToleranceMinutes, 0, 240, this.Text("ET minutes unit"));
        changed |= this.SliderFloat("Start time display after pull", ref this.configuration.StartTimeDisplayAfterSeconds, 0, 1800, this.Text("Seconds unit"));
        changed |= this.SliderInt("Countdown seconds", ref this.configuration.CountdownSeconds, 5, 30);
        return changed;
    }

    private bool DrawAppearance()
    {
        var changed = this.SliderFloat("Countdown frame thickness", ref this.configuration.CountdownFrameThickness, 1, 10, "%.1f px");
        changed |= this.SliderFloat("Scale", ref this.configuration.Scale, 0.5f, 2.5f, "%.2f");
        changed |= this.SliderFloat("Y Offset", ref this.configuration.YOffset, -10, 20, "%.1f");
        changed |= this.SliderFloat("Name Font Size", ref this.configuration.NameFontSize, 8, 36, "%.0f px");
        changed |= this.SliderFloat("HP Bar Width", ref this.configuration.HpBarWidth, 40, 360, "%.0f px");
        changed |= this.SliderFloat("HP Bar Height", ref this.configuration.HpBarHeight, 4, 48, "%.0f px");
        changed |= this.ColorEdit("A rank text color", ref this.configuration.ARankTextColor);
        changed |= this.ColorEdit("S rank text color", ref this.configuration.SRankTextColor);
        changed |= this.ColorEdit("HP bar color", ref this.configuration.HpBarColor);
        changed |= this.ColorEdit("Background color", ref this.configuration.BackgroundColor);
        changed |= this.ColorEdit("In-progress background color", ref this.configuration.InProgressBackgroundColor);
        changed |= this.ColorEdit("Countdown frame color", ref this.configuration.CountdownFrameColor);
        return changed;
    }

    private string Text(string key) => UiText.Get(key, this.configuration.Language);
    private string Label(string key) => UiText.Label(key, this.configuration.Language);
    private void FieldTitle(string key) => ImGui.TextWrapped(this.Text(key));

    private bool Checkbox(string key, ref bool value)
    {
        // Put the label on its own wrapped line when a narrow window cannot fit it.
        if (ImGui.CalcTextSize(this.Text(key)).X + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X <= ImGui.GetContentRegionAvail().X)
            return ImGui.Checkbox(this.Label(key), ref value);
        this.FieldTitle(key);
        return ImGui.Checkbox($"##{key}", ref value);
    }

    private bool SliderFloat(string key, ref float value, float min, float max, string format)
    {
        this.FieldTitle(key);
        return ImGui.SliderFloat($"##{key}", ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp);
    }

    private bool SliderInt(string key, ref int value, int min, int max)
    {
        this.FieldTitle(key);
        return ImGui.SliderInt($"##{key}", ref value, min, max, "%d", ImGuiSliderFlags.AlwaysClamp);
    }

    private bool ColorEdit(string key, ref Vector4 color)
    {
        this.FieldTitle(key);
        return ImGui.ColorEdit4($"##{key}", ref color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
    }

    private void DrawDebugLogControls()
    {
        var clock = this.debugLog.ClockStatus;
        const string clockPrefix = "Game ET: ";
        if (clock.StartsWith(clockPrefix, StringComparison.Ordinal))
            clock = clock[clockPrefix.Length..];
        ImGui.TextWrapped($"{this.Text("Game ET")}: {this.Text(clock)}");
        if (ImGui.Button(this.Label("Copy debug log")))
            ImGui.SetClipboardText(this.debugLog.Dump());
        this.SameLineIfFits("Dump debug log");
        if (ImGui.Button(this.Label("Dump debug log")))
        {
            try
            {
                var path = GetDebugDumpPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, this.debugLog.Dump());
                this.logSaveStatus = "Log saved";
            }
            catch (Exception ex)
            {
                this.logSaveStatus = "Log save failed";
                this.debugLog.Add($"log dump failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        this.SameLineIfFits("Clear debug log");
        if (ImGui.Button(this.Label("Clear debug log")))
        {
            this.debugLog.Clear();
            this.logSaveStatus = string.Empty;
        }
        if (this.logSaveStatus.Length > 0)
            ImGui.TextWrapped(this.Text(this.logSaveStatus));
        ImGui.TextWrapped(GetDebugDumpPath());
        var dump = this.debugLog.Dump();
        ImGui.InputTextMultiline("##debug-log", ref dump, Math.Max(12000, dump.Length * 4 + 1),
            new Vector2(-1, MathF.Max(120, ImGui.GetContentRegionAvail().Y - 4)), ImGuiInputTextFlags.ReadOnly);
    }

    private void SameLineIfFits(string key)
    {
        var end = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
        var required = ImGui.CalcTextSize(this.Text(key)).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X;
        if (end + required < ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X)
            ImGui.SameLine();
    }

    private void DrawAbout()
    {
        ImGui.TextUnformatted("AS Mob Plate");
        ImGui.TextUnformatted($"{this.Text("Version")}: {ReleaseVersion}");
        ImGui.TextUnformatted($"{this.Text("Author")}: MintakaSeiran");
        ImGui.TextWrapped("https://github.com/MintakaSeiran/AsMobPlate");
    }

    internal static string GetDebugDumpPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XIVLauncher", "devPlugins", "AsMobPlate", "debug-log.txt");
}
