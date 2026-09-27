using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AsMobPlate.Overlay;

internal static class AnnouncementTextParser
{
    private static readonly Regex ColonTimeRegex = new(@"(?i)(?:\bET\s*)?([01]?\d|2[0-3])\s*:\s*([0-5]\d)", RegexOptions.Compiled);
    private static readonly Regex CompactEtTimeRegex = new(@"(?i)\bET\s*([01]?\d|2[0-3])([0-5]\d)(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SpecialEtTimeRegex = new(@"\uE0D2\s*([01]?\d|2[0-3])([0-5]\d)(?!\d)", RegexOptions.Compiled);
    private static readonly string[] Keywords = { "開始", "スタート", "start", "pull", "et" };

    public static bool TryParseAnnouncement(string text, out int hour, out int minute)
    {
        var normalized = Normalize(text);
        if (!TryParseTime(normalized, out hour, out minute) || !ContainsKeyword(normalized))
            return false;

        return true;
    }

    internal static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c >= '０' && c <= '９')
                builder.Append((char)('0' + c - '０'));
            else if (c >= 'Ａ' && c <= 'Ｚ')
                builder.Append((char)('A' + c - 'Ａ'));
            else if (c >= 'ａ' && c <= 'ｚ')
                builder.Append((char)('a' + c - 'ａ'));
            else if (c == '：')
                builder.Append(':');
            else
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool TryParseTime(string text, out int hour, out int minute)
    {
        var match = ColonTimeRegex.Match(text);
        if (!match.Success)
            match = CompactEtTimeRegex.Match(text);
        if (!match.Success)
            match = SpecialEtTimeRegex.Match(text);

        if (!match.Success)
        {
            hour = 0;
            minute = 0;
            return false;
        }

        hour = int.Parse(match.Groups[1].Value);
        minute = int.Parse(match.Groups[2].Value);
        return true;
    }

    private static bool ContainsKeyword(string text)
    {
        for (var i = 0; i < Keywords.Length; i++)
        {
            if (text.Contains(Keywords[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return text.Contains('\uE0D2');
    }
}
