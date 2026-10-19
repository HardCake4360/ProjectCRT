using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoSleep.Editor
{
    public sealed class MapRegionClipboard
    {
        internal struct Cell
        {
            public TileDefinition ground, decoration;
            public CellVisualTransform groundVisual, decorationVisual;
            public int regionId;
        }
        internal readonly Cell[] cells;
        internal readonly List<MapEventPlacement> events;
        public int Width { get; }
        public int Height { get; }
        public int EventCount => events.Count;
        internal MapRegionClipboard(int width, int height, Cell[] cells, List<MapEventPlacement> events)
        { Width = width; Height = height; this.cells = cells; this.events = events; }
    }

    public sealed class MapEventClipboard
    {
        internal readonly List<MapEventPlacement> events;
        public int Count => events.Count;
        internal MapEventClipboard(List<MapEventPlacement> events) { this.events = events; }
    }

    // Multi-cell operations build a detached candidate before creating one Undo record.
    public static class MapEditOperations
    {
        public static MapRegionClipboard CopyRegion(MapDefinition source, RectInt region, bool includeEvents = true)
        {
            RequireRegion(source, region);
            var cells = new MapRegionClipboard.Cell[region.width * region.height];
            for (int y = 0; y < region.height; y++)
                for (int x = 0; x < region.width; x++)
                {
                    var cell = new Vector2Int(region.x + x, region.y + y);
                    cells[y * region.width + x] = new MapRegionClipboard.Cell
                    {
                        ground = source.GetTile(cell), decoration = source.GetTile(cell, TileLayer.Decoration),
                        groundVisual = source.GetCellVisual(cell), decorationVisual = source.GetCellVisual(cell, TileLayer.Decoration),
                        regionId = source.GetRegionId(cell)
                    };
                }
            var events = new List<MapEventPlacement>();
            if (includeEvents && source.events != null)
                foreach (MapEventPlacement placement in source.events)
                    if (placement != null && region.Contains(placement.cell)) events.Add(CopyPlacement(placement, placement.cell - region.position));
            return new MapRegionClipboard(region.width, region.height, cells, events);
        }

        public static List<string> PasteRegion(MapDefinition target, MapRegionClipboard clipboard, Vector2Int origin, bool includeEvents = true)
        {
            if (clipboard == null) throw new ArgumentNullException(nameof(clipboard));
            RequireRegion(target, new RectInt(origin, new Vector2Int(clipboard.Width, clipboard.Height)));
            var ids = new List<string>();
            Apply(target, "Paste map region", candidate =>
            {
                for (int y = 0; y < clipboard.Height; y++)
                    for (int x = 0; x < clipboard.Width; x++)
                    {
                        var cell = new Vector2Int(origin.x + x, origin.y + y);
                        MapRegionClipboard.Cell saved = clipboard.cells[y * clipboard.Width + x];
                        candidate.SetTile(cell, saved.ground);
                        candidate.SetTile(cell, saved.decoration, TileLayer.Decoration);
                        candidate.SetCellVisual(cell, saved.groundVisual);
                        candidate.SetCellVisual(cell, saved.decorationVisual, TileLayer.Decoration);
                        candidate.SetRegionId(cell, saved.regionId);
                    }
                if (includeEvents) AppendEvents(candidate, clipboard.events, origin, ids);
            });
            return ids;
        }

        public static void PaintRegion(MapDefinition target, RectInt region, TileDefinition tile, TileLayer layer, CellVisualTransform visual = default)
        {
            RequireRegion(target, region);
            Apply(target, tile == null ? "Erase map region" : "Paint map region", candidate =>
            {
                foreach (Vector2Int cell in region.allPositionsWithin)
                { candidate.SetTile(cell, tile, layer); candidate.SetCellVisual(cell, tile == null ? default : visual, layer); }
            });
        }

        public static void SetRegionIds(MapDefinition target, RectInt region, int regionId)
        {
            RequireRegion(target, region);
            if (regionId < 0) throw new ArgumentOutOfRangeException(nameof(regionId));
            Apply(target, "Paint region IDs", candidate =>
            { foreach (Vector2Int cell in region.allPositionsWithin) candidate.SetRegionId(cell, regionId); });
        }

        public static void SetVisualTransforms(MapDefinition target, RectInt region, TileLayer layer, CellVisualTransform visual)
        {
            RequireRegion(target, region);
            Apply(target, "Edit tile visuals", candidate =>
            { foreach (Vector2Int cell in region.allPositionsWithin) candidate.SetCellVisual(cell, visual, layer); });
        }

        public static MapEventClipboard CopyEvents(MapDefinition source, IReadOnlyList<string> placementIds, Vector2Int anchor)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (placementIds == null || placementIds.Count == 0) throw new ArgumentException("Select at least one event placement.", nameof(placementIds));
            if (!source.IsInBounds(anchor)) throw new ArgumentOutOfRangeException(nameof(anchor));
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var events = new List<MapEventPlacement>();
            foreach (string id in placementIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id)) throw new ArgumentException("Placement selection contains an empty or duplicate ID.");
                List<MapEventPlacement> matches = source.events?.FindAll(value => value != null && value.id == id);
                if (matches == null || matches.Count != 1) throw new ArgumentException("Unknown or ambiguous placement ID: " + id);
                if (!source.IsInBounds(matches[0].cell)) throw new ArgumentException("Selected placement is outside the source map: " + id);
                events.Add(CopyPlacement(matches[0], matches[0].cell - anchor));
            }
            return new MapEventClipboard(events);
        }

        public static List<string> PasteEvents(MapDefinition target, MapEventClipboard clipboard, Vector2Int origin)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (clipboard == null) throw new ArgumentNullException(nameof(clipboard));
            var ids = new List<string>();
            Apply(target, "Paste map events", candidate => AppendEvents(candidate, clipboard.events, origin, ids));
            return ids;
        }

        public static List<string> CloneEvents(MapDefinition target, IReadOnlyList<string> placementIds, Vector2Int offset)
            => PasteEvents(target, CopyEvents(target, placementIds, Vector2Int.zero), offset);

        public static List<string> BatchPlaceEvents(MapDefinition target, IEnumerable<Vector2Int> cells, EventDefinition definition, float yaw = 0, NpcDefinition npc = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (definition == null && npc == null) throw new ArgumentException("A placement needs an event definition or NPC profile.", nameof(definition));
            if (!float.IsFinite(yaw)) throw new ArgumentException("Placement yaw must be finite.", nameof(yaw));
            var unique = new HashSet<Vector2Int>();
            var placements = new List<MapEventPlacement>();
            foreach (Vector2Int cell in cells)
            {
                if (!target.IsInBounds(cell) || !unique.Add(cell)) throw new ArgumentException("Batch placement contains an outside or duplicate cell.");
                placements.Add(new MapEventPlacement { cell = cell, definition = definition, yaw = yaw, npc = npc });
            }
            if (placements.Count == 0) throw new ArgumentException("Batch placement needs at least one cell.", nameof(cells));
            var ids = new List<string>();
            Apply(target, "Place map events", candidate => AppendEvents(candidate, placements, Vector2Int.zero, ids));
            return ids;
        }

        public static List<string> BatchPlaceEvents(MapDefinition target, RectInt region, EventDefinition definition, float yaw = 0, NpcDefinition npc = null)
        {
            RequireRegion(target, region);
            return BatchPlaceEvents(target, CellsIn(region), definition, yaw, npc);
        }

        static IEnumerable<Vector2Int> CellsIn(RectInt region)
        { foreach (Vector2Int cell in region.allPositionsWithin) yield return cell; }

        public static MapDefinition CloneMap(MapDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            MapDefinition copy = Object.Instantiate(source);
            try
            {
                copy.name = source.name + " Copy";
                copy.mapId = Guid.NewGuid().ToString("N");
                copy.displayName = source.displayName + " 복사본";
                copy.events = new List<MapEventPlacement>();
                if (source.events != null)
                    foreach (MapEventPlacement placement in source.events)
                        if (placement == null) copy.events.Add(null);
                        else
                        {
                            MapEventPlacement cloned = CopyPlacement(placement, placement.cell);
                            cloned.id = Guid.NewGuid().ToString("N"); copy.events.Add(cloned);
                        }
                return copy;
            }
            catch { Object.DestroyImmediate(copy); throw; }
        }

        static void AppendEvents(MapDefinition target, List<MapEventPlacement> placements, Vector2Int origin, List<string> ids)
        {
            target.events ??= new List<MapEventPlacement>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (MapEventPlacement existing in target.events) if (existing != null) used.Add(existing.id);
            foreach (MapEventPlacement placement in placements)
            {
                long x = (long)origin.x + placement.cell.x, y = (long)origin.y + placement.cell.y;
                if (x < 0 || y < 0 || x >= target.width || y >= target.height) throw new ArgumentException("An event would be pasted outside the map.");
                MapEventPlacement copy = CopyPlacement(placement, new Vector2Int((int)x, (int)y));
                do { copy.id = Guid.NewGuid().ToString("N"); } while (!used.Add(copy.id));
                target.events.Add(copy); ids.Add(copy.id);
            }
        }

        static MapEventPlacement CopyPlacement(MapEventPlacement source, Vector2Int cell)
        {
            if (!float.IsFinite(source.yaw)) throw new ArgumentException("Placement yaw must be finite.");
            return new MapEventPlacement { id = source.id, cell = cell, definition = source.definition, yaw = source.yaw, npc = source.npc };
        }

        static void RequireRegion(MapDefinition map, RectInt region)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (region.width <= 0 || region.height <= 0 || region.x < 0 || region.y < 0 ||
                (long)region.x + region.width > map.width || (long)region.y + region.height > map.height)
                throw new ArgumentException("The whole selected rectangle must fit inside the map.", nameof(region));
        }

        static void Apply(MapDefinition target, string name, Action<MapDefinition> edit)
        {
            MapDefinition candidate = Object.Instantiate(target);
            try
            {
                candidate.name = target.name;
                edit(candidate);
                Undo.IncrementCurrentGroup();
                Undo.RegisterCompleteObjectUndo(target, name);
                EditorUtility.CopySerialized(candidate, target);
                EditorUtility.SetDirty(target);
                Undo.IncrementCurrentGroup();
            }
            finally { Object.DestroyImmediate(candidate); }
        }
    }
}
