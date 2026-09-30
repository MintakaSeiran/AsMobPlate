using System;
using System.Collections.Generic;
using System.Numerics;
using AsMobPlate.Localization;
using Dalamud.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AsMobPlate.Overlay;

internal static class NearestAetheryte
{
    internal readonly record struct Destination(uint Id, uint Territory, string Name, Vector2 Position);

    internal static ClientLanguage Language(UiLanguage language) => language switch
    {
        UiLanguage.JP => ClientLanguage.Japanese,
        UiLanguage.DE => ClientLanguage.German,
        UiLanguage.FR => ClientLanguage.French,
        _ => ClientLanguage.English,
    };

    internal static List<Destination> Read(ExcelSheet<Aetheryte> aetherytes, SubrowExcelSheet<MapMarker> markers, uint territory)
    {
        var result = new List<Destination>();
        foreach (var aetheryte in aetherytes)
        {
            if (!aetheryte.IsAetheryte || aetheryte.Invisible || aetheryte.Territory.RowId != territory
                || aetheryte.Map.RowId == 0 || !aetheryte.Map.IsValid || !aetheryte.PlaceName.IsValid)
                continue;
            var map = aetheryte.Map.Value;
            var name = aetheryte.PlaceName.Value.Name.ExtractText();
            if (map.SizeFactor == 0 || string.IsNullOrWhiteSpace(name) || !markers.HasRow(map.MapMarkerRange))
                continue;
            foreach (var marker in markers.GetRow(map.MapMarkerRange))
            {
                // DataType 3 identifies an Aetheryte row, not a place-name or aethernet marker.
                if (marker.DataType != 3 || marker.DataKey.RowId != aetheryte.RowId)
                    continue;
                result.Add(new Destination(aetheryte.RowId, territory, name,
                    MarkerToWorld(marker.X, marker.Y, map.SizeFactor, map.OffsetX, map.OffsetY)));
            }
        }
        return result;
    }

    internal static Vector2 MarkerToWorld(short x, short y, ushort sizeFactor, short offsetX, short offsetY)
    {
        if (sizeFactor == 0) return new Vector2(float.NaN);
        // Marker pixels are centered at 1024. Undo map scaling and offsets before comparing world X/Z.
        var scale = sizeFactor / 100f;
        return new Vector2((x - 1024) / scale - offsetX, (y - 1024) / scale - offsetY);
    }

    internal static Destination? Find(IEnumerable<Destination> candidates, uint territory, Vector2 huntPosition)
    {
        if (!float.IsFinite(huntPosition.X) || !float.IsFinite(huntPosition.Y)) return null;
        Destination? nearest = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            if (candidate.Territory != territory || string.IsNullOrWhiteSpace(candidate.Name)) continue;
            var distance = Vector2.DistanceSquared(huntPosition, candidate.Position);
            if (!float.IsFinite(distance) || distance > bestDistance) continue;
            if (distance == bestDistance && nearest is { } previous && candidate.Id >= previous.Id) continue;
            nearest = candidate;
            bestDistance = distance;
        }
        return nearest;
    }
}
