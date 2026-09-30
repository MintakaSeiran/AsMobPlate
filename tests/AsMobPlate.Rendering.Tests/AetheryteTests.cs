using System.Numerics;
using System.Text;
using AsMobPlate.Hunts;
using AsMobPlate.Localization;
using AsMobPlate.Overlay;
using Dalamud.Game;
using Lumina;
using Lumina.Excel.Sheets;

internal static class AetheryteTests
{
    internal static void Run()
    {
        NearestAetheryte.Destination[] candidates =
        [
            new(2, 100, "Far", new Vector2(100, 100)),
            new(3, 100, "Near", new Vector2(10, 0)),
            new(1, 200, "Other territory", Vector2.Zero),
            new(4, 100, "", Vector2.Zero),
            new(5, 100, "Invalid", new Vector2(float.NaN)),
        ];
        Check(NearestAetheryte.Find(candidates, 100, Vector2.Zero)?.Id == 3, "Nearest hunt destination");
        Check(NearestAetheryte.Find(candidates, 100, new Vector2(99, 99))?.Id == 2, "Moved hunt destination");
        Check(NearestAetheryte.Find(candidates, 999, Vector2.Zero) == null, "Cross-territory fallback");
        Check(NearestAetheryte.Find(candidates, 100, new Vector2(float.NaN)) == null, "Invalid hunt position");
        Check(NearestAetheryte.Find([], 100, Vector2.Zero) == null, "Empty destinations");
        NearestAetheryte.Destination[] tied = [new(9, 100, "Nine", Vector2.One), new(8, 100, "Eight", Vector2.One)];
        Check(NearestAetheryte.Find(tied, 100, Vector2.Zero)?.Id == 8, "Stable tie break");
        Check(NearestAetheryte.MarkerToWorld(1124, 924, 200, 30, -40) == new Vector2(20, -10), "Map scale and offsets");
        Check(!float.IsFinite(NearestAetheryte.MarkerToWorld(100, 100, 0, 0, 0).X), "Invalid map scale");
        Check(NearestAetheryte.Language(UiLanguage.JP) == ClientLanguage.Japanese, "Japanese sheet language");
        Check(NearestAetheryte.Language(UiLanguage.DE) == ClientLanguage.German, "German sheet language");
        Check(NearestAetheryte.Language(UiLanguage.FR) == ClientLanguage.French, "French sheet language");
        Check(NearestAetheryte.Language((UiLanguage)999) == ClientLanguage.English, "Invalid language fallback");
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            var destination = "Destination";
            var comment = RecruitmentComment.Build(language, HuntRank.S, "Hunt", "Area", new Vector2(12.3f, 4.5f), "11:20", destination);
            Check(comment == $"[S] Hunt / Area / {UiText.Get("Recruitment nearest", language)}: Destination / {UiText.Get("Recruitment start", language)} ET 11:20", "Localized destination format");
            Check(!comment.Contains("12.3"), "Coordinates not replaced");
            var withoutEt = RecruitmentComment.Build(language, HuntRank.A, "Hunt", "Area", Vector2.One, null, destination);
            Check(!withoutEt.Contains("ET"), "Unannounced start omitted");
            var fallback = RecruitmentComment.Build(language, HuntRank.A, "Hunt", "Area", Vector2.One, null, " ");
            Check(fallback == "[A] Hunt / Area (1.0, 1.0)", "Coordinate fallback");
            var longName = string.Concat(Enumerable.Repeat("\u72e9\u308a\U0001F600", 100));
            var bounded = RecruitmentComment.Build(language, HuntRank.S, longName, longName, Vector2.One, "11:20", destination);
            Check(Encoding.UTF8.GetByteCount(bounded) <= 190, "UTF-8 budget");
            Check(bounded.Contains(destination) && bounded.EndsWith("ET 11:20"), "Destination and ET preserved");
            var veryLong = RecruitmentComment.Build(language, HuntRank.S, longName, longName, Vector2.One, "11:20", longName);
            Check(Encoding.UTF8.GetByteCount(veryLong) <= 190 && veryLong.EndsWith("ET 11:20"), "Long destination budget");
            new UTF8Encoding(false, true).GetBytes(veryLong);
        }
        Console.WriteLine("Nearest aetheryte tests passed: territory, distance, map scaling/offsets, fallback and four-language bounded comments.");
    }

    internal static void ValidateGameData(string path)
    {
        using var game = new GameData(path);
        var markers = game.GetSubrowExcelSheet<MapMarker>() ?? throw new Exception("MapMarker sheet unavailable");
        var languages = new[] { Lumina.Data.Language.English, Lumina.Data.Language.Japanese, Lumina.Data.Language.German, Lumina.Data.Language.French };
        var names = new HashSet<string>();
        foreach (var language in languages)
        {
            var sheet = game.GetExcelSheet<Aetheryte>(language) ?? throw new Exception("Aetheryte sheet unavailable");
            var territories = sheet.Where(a => a.IsAetheryte && a.Map.RowId != 0 && a.Territory.RowId != 0)
                .Select(a => a.Territory.RowId).Distinct().ToArray();
            var count = 0;
            foreach (var territory in territories)
            {
                var destinations = NearestAetheryte.Read(sheet, markers, territory);
                var expected = sheet.Count(a => a.IsAetheryte && !a.Invisible && a.Map.RowId != 0 && a.Territory.RowId == territory);
                Check(destinations.Select(d => d.Id).Distinct().Count() == expected, $"Missing markers: {language}, territory={territory}");
                foreach (var destination in destinations)
                    Check(NearestAetheryte.Find(destinations, territory, destination.Position)?.Id == destination.Id, "Self-location lookup");
                count += destinations.Count;
            }
            var bentbranch = NearestAetheryte.Read(sheet, markers, 148).Single();
            names.Add(bentbranch.Name);
            var map = sheet.GetRow(3).Map.Value;
            var mapPosition = Dalamud.Utility.MapUtil.WorldToMap(bentbranch.Position, map);
            Check(Math.Abs(mapPosition.X - 21.74f) < 0.001f && Math.Abs(mapPosition.Y - 22.18f) < 0.001f, $"Bentbranch SDK map coordinates: {mapPosition}; world={bentbranch.Position}");
            Check(NearestAetheryte.Read(sheet, markers, 399).Count == 0, "Hinterlands must fall back instead of selecting Idyllshire");
            Console.WriteLine($"Game data {language}: {count} destinations across {territories.Length} territories; name={bentbranch.Name}");
        }
        Check(names.Count == 4, "Localized names must follow requested sheet language");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
