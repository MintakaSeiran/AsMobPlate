using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using AsMobPlate.Hunts;

namespace AsMobPlate.Overlay;

public sealed class HuntNameplateRenderer : IDisposable
{
    private const float PaddingX = 8.0f;
    private const float PaddingY = 5.0f;
    private const float GapY = 4.0f;

    private readonly Configuration configuration;
    private readonly HuntMarkRegistry registry;
    private readonly IObjectTable objectTable;
    private readonly IGameGui gameGui;
    private readonly IPluginLog pluginLog;
    private readonly HuntNotifier notifier;
    private readonly HuntMapFlagger mapFlagger;
    private readonly AnnouncedStartTimeTracker announcedStartTimeTracker;
    private readonly HpTracker hpTracker = new();

    public DebugLog DebugLog { get; } = new();

    public HuntNameplateRenderer(
        Configuration configuration,
        HuntMarkRegistry registry,
        IObjectTable objectTable,
        IGameGui gameGui,
        IClientState clientState,
        IChatGui chatGui,
        IFramework framework,
        IPluginLog pluginLog)
    {
        this.configuration = configuration;
        this.registry = registry;
        this.objectTable = objectTable;
        this.gameGui = gameGui;
        this.pluginLog = pluginLog;
        this.notifier = new HuntNotifier(configuration, chatGui, pluginLog);
        this.mapFlagger = new HuntMapFlagger(configuration, gameGui, clientState, pluginLog);
        this.announcedStartTimeTracker = new AnnouncedStartTimeTracker(configuration, chatGui, framework, clientState, pluginLog, this.DebugLog);
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
                return;

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

                if ((rank == HuntRank.A && !this.configuration.ShowARank) || (rank == HuntRank.S && !this.configuration.ShowSRank))
                    continue;

                if (npc.MaxHp == 0 || npc.CurrentHp == 0)
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
                    this.DrawNameplate(npc, rank, distance, timeToKill);
            }
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
        this.announcedStartTimeTracker.Dispose();
        this.notifier.Dispose();
    }

    private void DrawNameplate(IBattleNpc npc, HuntRank rank, float distance, TimeSpan? timeToKill)
    {
        var worldPosition = npc.Position + new Vector3(0.0f, MathF.Max(2.0f, npc.HitboxRadius * 1.5f) + this.configuration.YOffset, 0.0f);
        if (!this.gameGui.WorldToScreen(worldPosition, out var screenPosition))
            return;

        var drawList = ImGui.GetForegroundDrawList();
        var scale = MathF.Max(0.25f, this.configuration.Scale);
        var nameFontSize = MathF.Max(8.0f, this.configuration.NameFontSize) * scale;
        var barWidth = MathF.Max(20.0f, this.configuration.HpBarWidth) * scale;
        var barHeight = MathF.Max(4.0f, this.configuration.HpBarHeight) * scale;
        var paddingX = PaddingX * scale;
        var paddingY = PaddingY * scale;
        var gapY = GapY * scale;

        var rankTag = rank == HuntRank.A ? "[A]" : "[S]";
        var objectIndexSuffix = this.configuration.ShowObjectIndex ? $" [{npc.ObjectIndex}]" : string.Empty;
        var nameText = $"{rankTag} {npc.Name}{objectIndexSuffix}";
        var nameSize = ImGui.CalcTextSize(nameText);
        nameSize *= nameFontSize / ImGui.GetFontSize();

        var hpRatio = Math.Clamp((float)npc.CurrentHp / npc.MaxHp, 0.0f, 1.0f);
        var hpText = $"{hpRatio * 100.0f:0}%";
        var hpTextSize = ImGui.CalcTextSize(hpText);
        var infoText = BuildInfoText(distance, timeToKill);
        var startText = this.announcedStartTimeTracker.TryGetDisplayText(out var announcedStartText, out var isInProgress, out var startRemainingSeconds) ? announcedStartText : string.Empty;
        var inProgressText = isInProgress && this.configuration.ShowInProgressLabel ? "討伐中" : string.Empty;
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
        var topLeft = screenPosition - new Vector2(boxSize.X * 0.5f, boxSize.Y);
        var bottomRight = topLeft + boxSize;

        var backgroundColor = isInProgress ? this.configuration.InProgressBackgroundColor : this.configuration.BackgroundColor;
        drawList.AddRectFilled(topLeft, bottomRight, ToU32(backgroundColor), 4.0f * scale);
        this.DrawCountdownFrame(drawList, topLeft, bottomRight, startText.Length > 0, isInProgress, startRemainingSeconds, scale);

        var currentY = topLeft.Y + paddingY;
        if (inProgressText.Length > 0)
        {
            drawList.AddText(new Vector2(topLeft.X + paddingX, currentY), ToU32(new Vector4(1.0f, 0.48f, 0.48f, 1.0f)), inProgressText);
            currentY += inProgressSize.Y + gapY;
        }

        var textColor = ToU32(rank == HuntRank.A ? this.configuration.ARankTextColor : this.configuration.SRankTextColor);
        var textPos = new Vector2(topLeft.X + (boxSize.X - nameSize.X) * 0.5f, currentY);
        drawList.AddText(ImGui.GetFont(), nameFontSize, textPos, textColor, nameText);

        if (!this.configuration.ShowHpBar)
        {
            var nextY = this.DrawInfoText(drawList, topLeft, boxSize, textPos.Y + nameFontSize + gapY, infoText, infoSize);
            this.DrawInfoText(drawList, topLeft, boxSize, nextY, startText, startSize);
            return;
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
            return;
        }

        var hpTextPos = new Vector2(
            barTopLeft.X + (barWidth - hpTextSize.X) * 0.5f,
            barTopLeft.Y + (barHeight - hpTextSize.Y) * 0.5f);
        drawList.AddText(hpTextPos + new Vector2(1.0f, 1.0f), ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.85f)), hpText);
        drawList.AddText(hpTextPos, ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)), hpText);

        var startY = this.DrawInfoText(drawList, topLeft, boxSize, barBottomRight.Y + gapY, infoText, infoSize);
        this.DrawInfoText(drawList, topLeft, boxSize, startY, startText, startSize);
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

    private void DrawCountdownFrame(ImDrawListPtr drawList, Vector2 topLeft, Vector2 bottomRight, bool hasStartTime, bool isInProgress, double remainingSeconds, float scale)
    {
        if (!this.configuration.ShowCountdownFrame || !hasStartTime || isInProgress)
            return;

        var countdownWindow = this.announcedStartTimeTracker.CountdownWindowSeconds;
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
            text = text.Length == 0 ? $"ETA {ttk}" : $"{text}  ETA {ttk}";
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
