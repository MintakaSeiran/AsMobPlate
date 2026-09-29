using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;

namespace AsMobPlate.Overlay;

// Pure drawing: live objects and previews both supply a snapshot; no game actions occur here.
public sealed class NameplatePainter
{
    private const float PaddingX = 8.0f;
    private const float PaddingY = 5.0f;
    private const float GapY = 4.0f;
    private readonly Configuration configuration;

    public NameplatePainter(Configuration configuration) => this.configuration = configuration;

    public Vector2 Measure(in NameplateData data) => this.Draw(default, Vector2.Zero, data, true);

    public Vector2 Draw(ImDrawListPtr drawList, Vector2 screenPosition, in NameplateData data, bool measureOnly = false)
    {
        var scale = MathF.Max(0.25f, this.configuration.Scale);
        var nameFontSize = MathF.Max(8.0f, this.configuration.NameFontSize) * scale;
        var barWidth = MathF.Max(20.0f, this.configuration.HpBarWidth) * scale;
        var barHeight = MathF.Max(4.0f, this.configuration.HpBarHeight) * scale;
        var paddingX = PaddingX * scale;
        var paddingY = PaddingY * scale;
        var gapY = GapY * scale;

        var rankTag = data.Rank == HuntRank.Minion
            ? $"[{UiText.Get("Minion", this.configuration.Language)}]" : $"[{data.Rank}]";
        var objectIndexSuffix = this.configuration.ShowObjectIndex ? $" [{data.ObjectIndex}]" : string.Empty;
        var nameText = $"{rankTag} {data.Name}{objectIndexSuffix}";
        var nameSize = ImGui.CalcTextSize(nameText);
        nameSize *= nameFontSize / ImGui.GetFontSize();

        var hpRatio = Math.Clamp(data.HpRatio, 0.0f, 1.0f);
        var ssTriggered = data.IsDefeated && data.Rank == HuntRank.S && data.SsTriggered;
        var ssTrigger = ssTriggered && this.configuration.ShowSsTriggerFrame;
        var hpText = $"{hpRatio * 100.0f:0}%";
        var hpTextSize = ImGui.CalcTextSize(hpText);
        var infoText = data.IsDefeated
            ? (this.configuration.ShowDistance ? $"{data.Distance:0}y" : string.Empty)
            : BuildInfoText(data.Distance, data.TimeToKill);
        var tooLate = !data.IsDefeated && this.configuration.ShowArrivalWarning
            && (data.ArrivalWarning || (data.ArrivalSeconds is double arrival
                && data.TimeToKill is TimeSpan ttk && arrival > ttk.TotalSeconds));
        if (this.configuration.ShowArrivalWarning && data.ArrivalSeconds is double seconds)
            infoText += $"\n{UiText.Get("Arrival", this.configuration.Language)} {seconds:0}s";
        if (tooLate)
            infoText += $"\n{UiText.Get("Arrival too late", this.configuration.Language)}";
        var startText = data.StartText;
        var isInProgress = data.IsInProgress;
        var startRemainingSeconds = data.RemainingSeconds;
        var inProgressText = ssTriggered ? UiText.Get("Defeated SS trigger", this.configuration.Language)
            : data.IsDefeated ? UiText.Get("Defeated", this.configuration.Language)
            : isInProgress && this.configuration.ShowInProgressLabel ? UiText.Get("In progress", this.configuration.Language) : string.Empty;
        if (!data.IsDefeated && inProgressText.Length > 0 && data.CombatElapsedSeconds is double elapsed)
        {
            var totalSeconds = (long)Math.Max(0, elapsed);
            inProgressText += $" {totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }
        var infoSize = ImGui.CalcTextSize(infoText);
        var startSize = ImGui.CalcTextSize(startText);
        var inProgressSize = ImGui.CalcTextSize(inProgressText);

        var contentWidth = MathF.Max(MathF.Max(MathF.Max(MathF.Max(nameSize.X, infoSize.X), startSize.X), inProgressSize.X), this.configuration.ShowHpBar ? barWidth : 0.0f);
        var contentHeight = nameFontSize;
        if (inProgressText.Length > 0)
            contentHeight += gapY + inProgressSize.Y;
        if (this.configuration.ShowHpBar)
            contentHeight += gapY + barHeight;
        if (infoText.Length > 0)
            contentHeight += gapY + infoSize.Y;
        if (startText.Length > 0)
            contentHeight += gapY + startSize.Y;

        var boxSize = new Vector2(contentWidth + paddingX * 2.0f, contentHeight + paddingY * 2.0f);
        if (measureOnly)
            return boxSize;

        var topLeft = screenPosition - new Vector2(boxSize.X * 0.5f, boxSize.Y);
        var bottomRight = topLeft + boxSize;

        var backgroundColor = isInProgress ? this.configuration.InProgressBackgroundColor : this.configuration.BackgroundColor;
        if (ssTrigger)
        {
            drawList.AddRectFilled(topLeft, bottomRight, ToU32(new Vector4(0.01f, 0.01f, 0.01f, 0.94f)), 4 * scale);
            this.DrawSsTriggerFrame(drawList, topLeft, bottomRight, scale);
        }
        else if (tooLate || data.IsDefeated)
        {
            // A slow, continuous pulse keeps the text readable without abrupt flashes.
            var pulse = (float)(0.5 - 0.5 * Math.Cos(ImGui.GetTime() * Math.PI));
            var animatedBackground = Vector4.Lerp(new Vector4(0.01f, 0.01f, 0.01f, 0.88f),
                new Vector4(0.42f, 0.01f, 0.01f, 0.88f), pulse);
            drawList.AddRectFilled(topLeft, bottomRight, ToU32(animatedBackground), 4.0f * scale);
            this.DrawWarningFrame(drawList, topLeft, bottomRight, scale);
        }
        else
            drawList.AddRectFilled(topLeft, bottomRight, ToU32(backgroundColor), 4.0f * scale);
        if (!data.IsDefeated)
            this.DrawCountdownFrame(drawList, topLeft, bottomRight, startText.Length > 0, isInProgress, startRemainingSeconds, scale, data.CountdownWindowSeconds);

        var currentY = topLeft.Y + paddingY;
        if (inProgressText.Length > 0)
        {
            drawList.AddText(new Vector2(topLeft.X + paddingX, currentY), ToU32(ssTrigger ? Vector4.One : new Vector4(1.0f, 0.48f, 0.48f, 1.0f)), inProgressText);
            currentY += inProgressSize.Y + gapY;
        }

        var textColor = ToU32(data.Rank == HuntRank.A ? this.configuration.ARankTextColor : this.configuration.SRankTextColor);
        var textPos = new Vector2(topLeft.X + (boxSize.X - nameSize.X) * 0.5f, currentY);
        drawList.AddText(ImGui.GetFont(), nameFontSize, textPos, textColor, nameText);

        if (!this.configuration.ShowHpBar)
        {
            var nextY = this.DrawInfoText(drawList, topLeft, boxSize, textPos.Y + nameFontSize + gapY, infoText, infoSize);
            this.DrawInfoText(drawList, topLeft, boxSize, nextY, startText, startSize);
            return boxSize;
        }

        var barTopLeft = new Vector2(topLeft.X + (boxSize.X - barWidth) * 0.5f, textPos.Y + nameFontSize + gapY);
        var barBottomRight = barTopLeft + new Vector2(barWidth, barHeight);
        drawList.AddRectFilled(barTopLeft, barBottomRight, ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.86f)), 2.0f * scale);
        drawList.AddRectFilled(barTopLeft, new Vector2(barTopLeft.X + barWidth * hpRatio, barBottomRight.Y), ToU32(this.configuration.HpBarColor), 2.0f * scale);
        drawList.AddRect(barTopLeft, barBottomRight, ToU32(new Vector4(1.0f, 1.0f, 1.0f, 0.25f)), 2.0f * scale);

        if (!this.configuration.ShowHpPercent)
        {
            var nextY = this.DrawInfoText(drawList, topLeft, boxSize, barBottomRight.Y + gapY, infoText, infoSize);
            this.DrawInfoText(drawList, topLeft, boxSize, nextY, startText, startSize);
            return boxSize;
        }

        var hpTextPos = new Vector2(
            barTopLeft.X + (barWidth - hpTextSize.X) * 0.5f,
            barTopLeft.Y + (barHeight - hpTextSize.Y) * 0.5f);
        drawList.AddText(hpTextPos + new Vector2(1.0f, 1.0f), ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.85f)), hpText);
        drawList.AddText(hpTextPos, ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)), hpText);

        var startY = this.DrawInfoText(drawList, topLeft, boxSize, barBottomRight.Y + gapY, infoText, infoSize);
        this.DrawInfoText(drawList, topLeft, boxSize, startY, startText, startSize);
        return boxSize;
    }

    private void DrawSsTriggerFrame(ImDrawListPtr list, Vector2 min, Vector2 max, float scale)
    {
        var width = (float.IsFinite(this.configuration.SsTriggerFrameThickness)
            ? Math.Clamp(this.configuration.SsTriggerFrameThickness, 1, 10) : 4) * scale;
        var period = float.IsFinite(this.configuration.SsTriggerPulseSeconds)
            ? Math.Clamp(this.configuration.SsTriggerPulseSeconds, 0.5f, 3) : 1;
        var color = this.configuration.SsTriggerFrameColor;
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W))
            color = new Vector4(0, 229f / 255f, 1, 1);
        color = Vector4.Clamp(color, Vector4.Zero, Vector4.One);
        var wave = (float)(0.5 + 0.5 * Math.Cos(ImGui.GetTime() * Math.Tau / period));
        var alternate = color;
        color.W *= 0.35f + 0.65f * wave;
        alternate.W *= 0.35f + 0.65f * (1 - wave);
        // Diagonal pairs alternate; black-edged corner brackets stay outside the content.
        var inset = new Vector2(width * 0.5f + scale);
        min -= inset;
        max += inset;
        var arms = (max - min) * 0.28f;
        DrawCorner(min, 1, 1, color);
        DrawCorner(max, -1, -1, color);
        DrawCorner(new Vector2(max.X, min.Y), -1, 1, alternate);
        DrawCorner(new Vector2(min.X, max.Y), 1, -1, alternate);

        void DrawCorner(Vector2 corner, float horizontal, float vertical, Vector4 tint)
        {
            var radius = MathF.Min(4 * scale, MathF.Min(arms.X, arms.Y));
            var center = corner + new Vector2(horizontal * radius, vertical * radius);
            var startAngle = vertical > 0 ? -MathF.PI / 2 : MathF.PI / 2;
            var endAngle = horizontal > 0 ? (vertical > 0 ? -MathF.PI : MathF.PI) : 0;
            Stroke(ToU32(new Vector4(0, 0, 0, 1)), width + 2 * scale);
            Stroke(ToU32(tint), width);

            void Stroke(uint strokeColor, float thickness)
            {
                list.PathLineTo(corner + new Vector2(horizontal * arms.X, 0));
                list.PathLineTo(corner + new Vector2(horizontal * radius, 0));
                list.PathArcTo(center, radius, startAngle, endAngle, 6);
                list.PathLineTo(corner + new Vector2(0, vertical * arms.Y));
                list.PathStroke(strokeColor, ImDrawFlags.None, thickness);
            }
        }
    }

    private void DrawWarningFrame(ImDrawListPtr list, Vector2 min, Vector2 max, float scale)
    {
        var thickness = 4 * scale;
        var outerMin = min - new Vector2(thickness);
        var outerMax = max + new Vector2(thickness);
        // Four non-overlapping clip regions leave the entire content area stripe-free.
        this.DrawWarningStripes(list, outerMin, outerMax, outerMin, new Vector2(outerMax.X, min.Y), scale);
        this.DrawWarningStripes(list, outerMin, outerMax, new Vector2(outerMin.X, max.Y), outerMax, scale);
        this.DrawWarningStripes(list, outerMin, outerMax, new Vector2(outerMin.X, min.Y), new Vector2(min.X, max.Y), scale);
        this.DrawWarningStripes(list, outerMin, outerMax, new Vector2(max.X, min.Y), new Vector2(outerMax.X, max.Y), scale);
    }

    private void DrawWarningStripes(ImDrawListPtr list, Vector2 min, Vector2 max, Vector2 clipMin, Vector2 clipMax, float scale)
    {
        var width = 14 * scale;
        var height = max.Y - min.Y;
        // Adjacent, non-overlapping stripes preserve each color's configured alpha.
        list.PushClipRect(clipMin, clipMax, true);
        for (var i = 0; i * width < max.X - min.X + height; i++)
        {
            var x = min.X - height + i * width;
            var color = i % 2 == 0 ? this.configuration.ArrivalWarningRed : this.configuration.ArrivalWarningYellow;
            list.AddQuadFilled(new Vector2(x, min.Y), new Vector2(x + width, min.Y),
                new Vector2(x + width + height, max.Y), new Vector2(x + height, max.Y), ToU32(color));
        }
        list.PopClipRect();
    }

    private float DrawInfoText(ImDrawListPtr drawList, Vector2 topLeft, Vector2 boxSize, float y, string infoText, Vector2 infoSize)
    {
        if (infoText.Length == 0)
            return y;

        var infoPos = new Vector2(topLeft.X + (boxSize.X - infoSize.X) * 0.5f, y);
        drawList.AddText(infoPos + new Vector2(1.0f, 1.0f), ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.85f)), infoText);
        drawList.AddText(infoPos, ToU32(new Vector4(1.0f, 1.0f, 1.0f, 0.92f)), infoText);
        return y + infoSize.Y + GapY * MathF.Max(0.25f, this.configuration.Scale);
    }

    private void DrawCountdownFrame(ImDrawListPtr drawList, Vector2 topLeft, Vector2 bottomRight, bool hasStartTime, bool isInProgress, double remainingSeconds, float scale, int countdownWindow)
    {
        if (!this.configuration.ShowCountdownFrame || !hasStartTime || isInProgress)
            return;

        if (remainingSeconds > countdownWindow || remainingSeconds < 0.0)
            return;

        var progress = Math.Clamp((float)(remainingSeconds / countdownWindow), 0.0f, 1.0f);
        if (progress <= 0.0f)
            return;

        var configuredThickness = float.IsFinite(this.configuration.CountdownFrameThickness)
            ? Math.Clamp(this.configuration.CountdownFrameThickness, 1.0f, 10.0f)
            : 2.0f;
        var thickness = MathF.Max(1.0f, configuredThickness * scale);
        // ImGui centers strokes on the path; keep the inner half outside the plate.
        var inset = MathF.Max(1.0f * scale, thickness * 0.5f);
        var min = topLeft - new Vector2(inset, inset);
        var max = bottomRight + new Vector2(inset, inset);
        DrawPartialRectFromTopRight(drawList, min, max, progress, ToU32(this.configuration.CountdownFrameColor), thickness);
    }

    private static void DrawPartialRectFromTopRight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float progress, uint color, float thickness)
    {
        var width = MathF.Max(1.0f, max.X - min.X);
        var height = MathF.Max(1.0f, max.Y - min.Y);
        var remaining = (width * 2.0f + height * 2.0f) * progress;
        var current = new Vector2(max.X, min.Y);

        remaining = DrawEdge(drawList, ref current, new Vector2(max.X, max.Y), remaining, color, thickness);
        remaining = DrawEdge(drawList, ref current, new Vector2(min.X, max.Y), remaining, color, thickness);
        remaining = DrawEdge(drawList, ref current, new Vector2(min.X, min.Y), remaining, color, thickness);
        DrawEdge(drawList, ref current, new Vector2(max.X, min.Y), remaining, color, thickness);
    }

    private static float DrawEdge(ImDrawListPtr drawList, ref Vector2 from, Vector2 edgeEnd, float remainingLength, uint color, float thickness)
    {
        if (remainingLength <= 0.0f)
            return 0.0f;

        var edge = edgeEnd - from;
        var edgeLength = edge.Length();
        if (edgeLength <= 0.0f)
            return remainingLength;

        var drawLength = MathF.Min(edgeLength, remainingLength);
        var to = from + edge / edgeLength * drawLength;
        drawList.AddLine(from, to, color, thickness);
        from = edgeEnd;
        return remainingLength - drawLength;
    }

    private string BuildInfoText(float distance, TimeSpan? timeToKill)
    {
        var text = string.Empty;
        if (this.configuration.ShowDistance)
            text = $"{distance:0}y";

        if (this.configuration.ShowTimeToKill)
        {
            var ttk = FormatTimeToKill(timeToKill);
            var etaLabel = UiText.Get("ETA", this.configuration.Language);
            text = text.Length == 0 ? $"{etaLabel} {ttk}" : $"{text}  {etaLabel} {ttk}";
        }

        return text;
    }

    private static string FormatTimeToKill(TimeSpan? timeToKill)
    {
        if (timeToKill == null)
            return "--:--";

        var value = timeToKill.Value;
        if (value.TotalHours >= 1.0)
            return $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}";

        return $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private static uint ToU32(Vector4 color)
    {
        return ImGui.ColorConvertFloat4ToU32(color);
    }
}

public readonly record struct NameplateData(
    HuntRank Rank,
    string Name,
    uint ObjectIndex,
    float HpRatio,
    float Distance,
    TimeSpan? TimeToKill,
    string StartText,
    bool IsInProgress,
    double RemainingSeconds,
    int CountdownWindowSeconds,
    double? ArrivalSeconds = null,
    bool IsDefeated = false,
    bool ArrivalWarning = false,
    double? CombatElapsedSeconds = null,
    bool SsTriggered = false);
