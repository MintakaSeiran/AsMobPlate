using System;
using System.Numerics;
using System.Text;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;

namespace AsMobPlate.Overlay;

internal static class RecruitmentComment
{
    internal static string Build(UiLanguage language, HuntRank rank, string name, string area, Vector2 mapPosition, string? startEt)
    {
        var suffix = string.IsNullOrWhiteSpace(startEt) ? string.Empty
            : $" / {UiText.Get("Recruitment start", language)} ET {startEt}";
        var prefix = FormattableString.Invariant($"[{rank}] {name} / {area} ({mapPosition.X:0.0}, {mapPosition.Y:0.0})");
        // Preserve the announced ET when long localized names consume the native UTF-8 buffer.
        var budget = 190 - Encoding.UTF8.GetByteCount(suffix);
        var builder = new StringBuilder();
        foreach (var rune in prefix.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > budget)
                break;
            builder.Append(rune.ToString());
            budget -= rune.Utf8SequenceLength;
        }
        return builder.ToString().TrimEnd() + suffix;
    }
}
