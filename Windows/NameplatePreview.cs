using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;

namespace AsMobPlate.Windows;

// This window-only state never reaches hunt detection, notifications, or native commands.
public sealed class NameplatePreview
{
    private static readonly string[] Modes = ["Preview idle", "Preview countdown", "In progress", "Arrival too late"];
    private readonly Configuration configuration;
    private readonly NameplatePainter painter;
    private HuntRank rank = HuntRank.A;
    private int mode = 1;
    private float hpPercent = 100;
    private float remainingFraction = 0.6f;
    private bool animate;
    private double animationStart;
    private bool openRequested;

    public void RequestOpen() => this.openRequested = true;

    public NameplatePreview(Configuration configuration)
    {
        this.configuration = configuration;
        this.painter = new NameplatePainter(configuration);
    }

    public void Draw()
    {
        if (this.openRequested)
        {
            ImGui.SetNextItemOpen(true);
            this.openRequested = false;
        }
        if (!ImGui.CollapsingHeader(this.Label("Preview"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            this.animate = false;
            return;
        }

        ImGui.PushID("nameplate-preview");
        if (ImGui.RadioButton("A", this.rank == HuntRank.A))
            this.rank = HuntRank.A;
        ImGui.SameLine();
        if (ImGui.RadioButton("S", this.rank == HuntRank.S))
            this.rank = HuntRank.S;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(MathF.Max(120, ImGui.GetContentRegionAvail().X));
        if (ImGui.BeginCombo("##preview-state", this.Text(Modes[this.mode])))
        {
            for (var i = 0; i < Modes.Length; i++)
            {
                if (ImGui.Selectable(this.Label(Modes[i]), this.mode == i))
                {
                    this.mode = i;
                    this.animate = false;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(160);
        ImGui.SliderFloat(this.Label("Preview HP"), ref this.hpPercent, 0, 100, "%.0f%%", ImGuiSliderFlags.AlwaysClamp);

        var duration = Math.Clamp(this.configuration.CountdownSeconds, 5, 30);
        ImGui.BeginDisabled(this.mode != 1);
        if (ImGui.Checkbox(this.Label("Animate preview"), ref this.animate))
            this.animationStart = ImGui.GetTime() - duration * (1.0 - this.remainingFraction);
        var remaining = this.animate
            ? PreviewAnimation.RemainingSeconds(ImGui.GetTime() - this.animationStart, duration)
            : this.remainingFraction * duration;
        if (this.animate)
            this.remainingFraction = (float)Math.Max(0, remaining / duration);

        var seconds = (float)Math.Max(0, remaining);
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat(this.Label("Preview remaining"), ref seconds, 0, duration, this.Text("Interval unit"), ImGuiSliderFlags.AlwaysClamp))
        {
            this.animate = false;
            this.remainingFraction = seconds / duration;
            remaining = seconds;
        }
        ImGui.EndDisabled();

        var inProgress = this.configuration.ShowAnnouncedStartTime && (this.mode == 2 || (this.mode == 1 && remaining <= 0));
        var startText = string.Empty;
        if (this.mode != 0 && this.configuration.ShowAnnouncedStartTime)
        {
            var displaySeconds = this.mode == 2 ? 0 : Math.Max(0, (int)Math.Ceiling(remaining));
            startText = this.configuration.ShowEtAtAnnouncement
                ? UiText.Format("Announcement countdown", this.configuration.Language, "13:13", "13:19", displaySeconds)
                : UiText.Format("Start countdown", this.configuration.Language, "13:19", displaySeconds);
        }

        var data = new NameplateData(this.rank, this.Text("Preview hunt name"), 118, this.hpPercent / 100,
            this.mode == 3 ? 850 : 23, this.mode == 3 ? TimeSpan.FromSeconds(35) : this.hpPercent < 100 ? TimeSpan.FromSeconds(75) : null,
            this.mode == 3 ? string.Empty : startText, inProgress, this.mode == 2 ? 0 : remaining, duration,
            this.mode == 3 ? 48 : null);
        var size = this.painter.Measure(data);
        var margin = 16 + 12 * MathF.Max(0.25f, this.configuration.Scale);
        var maxCanvasHeight = Math.Clamp(ImGui.GetContentRegionAvail().Y - 120, 80, 260);
        var canvasHeight = Math.Clamp(size.Y + margin * 2 + 20, 80, maxCanvasHeight);
        if (ImGui.BeginChild("preview-canvas", new Vector2(0, canvasHeight), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if ((this.rank == HuntRank.A && !this.configuration.ShowARank) ||
                (this.rank == HuntRank.S && !this.configuration.ShowSRank))
            {
                ImGui.TextWrapped(this.Text("Preview rank hidden"));
            }
            else
            {
                // Include the outside stroke in the scrollable area; never shrink the configured scale.
                var available = ImGui.GetContentRegionAvail();
                var content = Vector2.Max(size + new Vector2(margin * 2), available);
                var origin = ImGui.GetCursorScreenPos();
                var anchor = origin + new Vector2(content.X / 2, (content.Y + size.Y) / 2);
                this.painter.Draw(ImGui.GetWindowDrawList(), anchor, data);
                ImGui.Dummy(content);
            }
        }
        ImGui.EndChild();
        ImGui.PopID();
    }

    private string Text(string key) => UiText.Get(key, this.configuration.Language);
    private string Label(string key) => UiText.Label(key, this.configuration.Language);
}
