using System;
using System.Numerics;
using System.Text;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;

namespace AsMobPlate.Overlay;

internal static class RecruitmentComment
{
    internal static string Build(UiLanguage language, HuntRank rank, string name, string area, Vector2 mapPosition, string? startEt, string? nearestAetheryte = null)
    {
        var suffix = string.IsNullOrWhiteSpace(startEt) ? string.Empty
            : $" / {UiText.Get("Recruitment start", language)} ET {startEt}";
        var prefix = $"[{rank}] {name} / {area}";
        var location = string.IsNullOrWhiteSpace(nearestAetheryte)
            ? FormattableString.Invariant($" ({mapPosition.X:0.0}, {mapPosition.Y:0.0})")
            : $" / {UiText.Get("Recruitment nearest", language)}: {nearestAetheryte}";
        // Reserve the destination and ET before shortening long hunt/area names.
        var budget = 190 - Encoding.UTF8.GetByteCount(suffix);
        location = Truncate(location, Math.Max(0, budget - 32));
        return Truncate(prefix, budget - Encoding.UTF8.GetByteCount(location)).TrimEnd() + location + suffix;
    }

    private static string Truncate(string text, int budget)
    {
        var builder = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > budget)
                break;
            builder.Append(rune.ToString());
            budget -= rune.Utf8SequenceLength;
        }
        return builder.ToString();
    }
}
