using System;
using System.Collections.Generic;

namespace AsMobPlate.Overlay;

public enum SsSystemEvent { None, Start, Returned }

public sealed class SsSystemMessages
{
    // Official LogMessage rows 9332/9334, verified against ja/en/de/fr game-data
    // extracts on 2026-09-28. Keep all languages independent of the settings UI.
    private readonly HashSet<string> starts = new(StringComparer.Ordinal)
    {
        "特殊なリスキーモブの配下が、偵察活動を開始したようだ……",
        "The minions of an extraordinarily powerful mark are on the hunt for prey...",
        "Die Helfer eines besonderen Hochwilds beginnen ihre Erkundung ...",
        "Les sous-fifres du monstre d'élite ont commencé à vous espionner...",
    };
    private readonly HashSet<string> returns = new(StringComparer.Ordinal)
    {
        "特殊なリスキーモブの配下が、偵察活動を終えて帰還したようだ……",
        "The minions of an extraordinarily powerful mark have withdrawn...",
        "Die Helfer eines besonderen Hochwilds haben ihre Erkundung beendet.",
        "Les sous-fifres du monstre d'élite ont arrêté leur mission d'espionnage et ont déserté les lieux...",
    };

    public SsSystemMessages(string localStart = "", string localReturn = "")
    {
        if (!string.IsNullOrWhiteSpace(localStart)) this.starts.Add(Normalize(localStart));
        if (!string.IsNullOrWhiteSpace(localReturn)) this.returns.Add(Normalize(localReturn));
    }

    public SsSystemEvent Match(int logKind, string text)
    {
        if (logKind != 57) return SsSystemEvent.None;
        var normalized = Normalize(text);
        if (this.starts.Contains(normalized)) return SsSystemEvent.Start;
        return this.returns.Contains(normalized) ? SsSystemEvent.Returned : SsSystemEvent.None;
    }

    private static string Normalize(string text) => text.Trim().Replace('\u00A0', ' ');
}
