using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public enum TileLayer { Ground, Decoration }

    [Serializable]
    public struct CellVisualTransform
    {
        public int quarterTurns;
        public float heightOffset;
    }

    [Serializable]
    public sealed class MapEventPlacement
    {
        public string id = "event";
        public Vector2Int cell;
        public EventDefinition definition;
        public NpcDefinition npc;
        public float yaw;
    }

    [CreateAssetMenu(fileName = "Map", menuName = "No Sleep/Map")]
    public sealed class MapDefinition : ScriptableObject
    {
        public const int MaximumSize = 256;
        public string mapId = "map";
        public string displayName = "Map";
        [Range(1, MaximumSize)] public int width = 12;
        [Range(1, MaximumSize)] public int height = 10;
        [Min(0.1f)] public float cellSize = 1f;
        public Vector2Int spawnCell = new Vector2Int(1, 1);
        public List<TileDefinition> palette = new List<TileDefinition>();
        public List<MapEventPlacement> events = new List<MapEventPlacement>();

        [SerializeField, HideInInspector] private int storedWidth;
        [SerializeField, HideInInspector] private int storedHeight;
        [SerializeField, HideInInspector] private List<int> groundTiles = new List<int>();
        [SerializeField, HideInInspector] private List<int> decorationTiles = new List<int>();
        [SerializeField, HideInInspector] private List<TileDefinition> indexedPalette = new List<TileDefinition>();
        [SerializeField, HideInInspector] private List<CellVisualTransform> groundVisuals = new List<CellVisualTransform>();
        [SerializeField, HideInInspector] private List<CellVisualTransform> decorationVisuals = new List<CellVisualTransform>();
        [SerializeField, HideInInspector] private List<int> regionIds = new List<int>();

        public bool IsInBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;

        public Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);

        public Vector2Int WorldToCell(Vector3 position)
        {
            float size = Mathf.Max(0.1f, cellSize);
            return new Vector2Int(Mathf.FloorToInt(position.x / size + 0.5f), Mathf.FloorToInt(position.z / size + 0.5f));
        }

        public TileDefinition GetTile(Vector2Int cell, TileLayer layer = TileLayer.Ground)
        {
            int index = GetTileIndex(cell, layer);
            return index >= 0 && palette != null && index < palette.Count ? palette[index] : null;
        }

        public int GetTileIndex(Vector2Int cell, TileLayer layer = TileLayer.Ground)
        {
            if (!IsInBounds(cell)) return -1;
            EnsureStorage();
            return GetStorage(layer)[cell.y * width + cell.x];
        }

        public void SetTile(Vector2Int cell, TileDefinition tile, TileLayer layer = TileLayer.Ground)
        {
            if (!IsInBounds(cell)) return;
            if (palette == null) palette = new List<TileDefinition>();
            int index = tile == null ? -1 : palette.IndexOf(tile);
            if (tile != null && index < 0)
            {
                palette.Add(tile);
                index = palette.Count - 1;
            }
            SetTileIndex(cell, index, layer);
        }

        public void SetTileIndex(Vector2Int cell, int paletteIndex, TileLayer layer = TileLayer.Ground)
        {
            if (!IsInBounds(cell)) return;
            EnsureStorage();
            GetStorage(layer)[cell.y * width + cell.x] = paletteIndex >= 0 && palette != null && paletteIndex < palette.Count ? paletteIndex : -1;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            if (!IsInBounds(cell)) return false;
            TileDefinition ground = GetTile(cell);
            TileDefinition decoration = GetTile(cell, TileLayer.Decoration);
            return ground != null && ground.walkable && (decoration == null || decoration.walkable);
        }

        public CellVisualTransform GetCellVisual(Vector2Int cell, TileLayer layer = TileLayer.Ground)
        {
            if (!IsInBounds(cell)) return default;
            EnsureStorage();
            return GetVisualStorage(layer)[cell.y * width + cell.x];
        }

        public void SetCellVisual(Vector2Int cell, CellVisualTransform visual, TileLayer layer = TileLayer.Ground)
        {
            if (!IsInBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            if (!float.IsFinite(visual.heightOffset)) throw new ArgumentException("Visual height must be finite.", nameof(visual));
            visual.quarterTurns = ((visual.quarterTurns % 4) + 4) % 4;
            EnsureStorage();
            GetVisualStorage(layer)[cell.y * width + cell.x] = visual;
        }

        public int GetRegionId(Vector2Int cell)
        {
            if (!IsInBounds(cell)) return 0;
            EnsureStorage();
            return regionIds[cell.y * width + cell.x];
        }

        public void SetRegionId(Vector2Int cell, int regionId)
        {
            if (!IsInBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            if (regionId < 0) throw new ArgumentOutOfRangeException(nameof(regionId));
            EnsureStorage();
            regionIds[cell.y * width + cell.x] = regionId;
        }

        public void Resize(int newWidth, int newHeight)
        {
            SynchronizePalette();
            newWidth = Mathf.Clamp(newWidth, 1, MaximumSize);
            newHeight = Mathf.Clamp(newHeight, 1, MaximumSize);
            groundTiles = ResizeLayer(groundTiles, storedWidth, storedHeight, newWidth, newHeight);
            decorationTiles = ResizeLayer(decorationTiles, storedWidth, storedHeight, newWidth, newHeight);
            groundVisuals = ResizeValues(groundVisuals, storedWidth, storedHeight, newWidth, newHeight);
            decorationVisuals = ResizeValues(decorationVisuals, storedWidth, storedHeight, newWidth, newHeight);
            regionIds = ResizeValues(regionIds, storedWidth, storedHeight, newWidth, newHeight);
            width = storedWidth = newWidth;
            height = storedHeight = newHeight;
            spawnCell = new Vector2Int(Mathf.Clamp(spawnCell.x, 0, width - 1), Mathf.Clamp(spawnCell.y, 0, height - 1));
        }

        public void Fill(TileDefinition tile, TileLayer layer = TileLayer.Ground)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    SetTile(new Vector2Int(x, y), tile, layer);
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(mapId)) errors.Add("맵 ID가 비어 있습니다. 저장과 셀프 스위치에 사용할 고유 ID를 입력하세요.");
            if (width < 1 || height < 1 || width > MaximumSize || height > MaximumSize) errors.Add("맵 크기는 1~256 사이여야 합니다.");
            if (!float.IsFinite(cellSize) || cellSize < 0.1f) errors.Add("타일 크기는 0.1 이상이어야 합니다.");
            int missing = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (GetTile(new Vector2Int(x, y)) == null) missing++;
            if (missing > 0) errors.Add($"지면 타일이 없는 칸이 {missing}개 있습니다. 해당 칸에는 이동할 수 없습니다.");
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (GetRegionId(cell) < 0) errors.Add($"영역 ID ({x},{y})는 0 이상이어야 합니다.");
                    for (int layerIndex = 0; layerIndex < 2; layerIndex++)
                    {
                        var layer = (TileLayer)layerIndex;
                        CellVisualTransform visual = GetCellVisual(cell, layer);
                        if (visual.quarterTurns < 0 || visual.quarterTurns > 3 || !float.IsFinite(visual.heightOffset))
                            errors.Add($"{layer} 시각 변형 ({x},{y})이 잘못되었습니다.");
                    }
                }
            if (!IsInBounds(spawnCell) || !IsWalkable(spawnCell)) errors.Add("플레이어 시작 지점이 이동 가능한 지면 위에 있어야 합니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var npcCells = new HashSet<Vector2Int>();
            if (events != null)
                for (int i = 0; i < events.Count; i++)
                {
                    MapEventPlacement placement = events[i];
                    if (placement == null) { errors.Add($"이벤트 #{i + 1}의 데이터가 없습니다."); continue; }
                    if (string.IsNullOrWhiteSpace(placement.id) || !ids.Add(placement.id)) errors.Add($"이벤트 #{i + 1}의 ID가 비어 있거나 중복입니다.");
                    if (!IsInBounds(placement.cell)) errors.Add($"이벤트 '{placement.id}'의 좌표가 맵 범위를 벗어났습니다.");
                    if (placement.definition == null && placement.npc == null) errors.Add($"이벤트 '{placement.id}'에 이벤트 또는 NPC 정의를 지정하세요.");
                    if (!float.IsFinite(placement.yaw)) errors.Add($"이벤트 '{placement.id}'의 방향이 유한한 값이어야 합니다.");
                    if (placement.npc != null)
                    {
                        foreach (string error in placement.npc.Validate()) errors.Add($"NPC '{placement.id}': {error}");
                        if (!IsWalkable(placement.cell)) errors.Add($"NPC '{placement.id}'의 배치가 이동 불가능한 칸입니다.");
                        if (!npcCells.Add(placement.cell)) errors.Add($"NPC '{placement.id}'의 초기 칸에 다른 NPC가 있습니다.");
                        var routes = new HashSet<NpcRouteDefinition>();
                        if (placement.npc.route != null) routes.Add(placement.npc.route);
                        if (placement.npc.schedules != null) foreach (var schedule in placement.npc.schedules)
                        {
                            if (schedule == null) continue;
                            if (schedule.route != null) routes.Add(schedule.route);
                            else if (!IsWalkable(schedule.stationCell)) errors.Add($"NPC '{placement.id}'의 일과 정지 칸이 이동 불가능합니다.");
                        }
                        foreach (var route in routes) if (route.points != null) foreach (var point in route.points)
                            if (point != null && !IsWalkable(point.cell)) errors.Add($"NPC '{placement.id}'의 경로 '{route.routeId}'에 이동 불가능한 칸 {point.cell}이 있습니다.");
                    }
                }
            return errors;
        }

        private List<int> GetStorage(TileLayer layer) => layer == TileLayer.Ground ? groundTiles : decorationTiles;
        private List<CellVisualTransform> GetVisualStorage(TileLayer layer) => layer == TileLayer.Ground ? groundVisuals : decorationVisuals;

        private void EnsureStorage()
        {
            SynchronizePalette();
            if (storedWidth != width || storedHeight != height || groundTiles == null || decorationTiles == null || groundTiles.Count != width * height || decorationTiles.Count != width * height ||
                groundVisuals == null || decorationVisuals == null || regionIds == null || groundVisuals.Count != width * height || decorationVisuals.Count != width * height || regionIds.Count != width * height)
                Resize(width, height);
        }

        // Palette editing never silently changes the meaning of tiles already painted.
        public void SynchronizePalette()
        {
            if (palette == null) palette = new List<TileDefinition>();
            if (indexedPalette == null) indexedPalette = new List<TileDefinition>();
            bool changed = indexedPalette.Count != palette.Count;
            if (!changed)
                for (int i = 0; i < palette.Count; i++)
                    if (indexedPalette[i] != palette[i]) { changed = true; break; }
            if (!changed) return;
            RemapLayer(groundTiles);
            RemapLayer(decorationTiles);
            indexedPalette = new List<TileDefinition>(palette);
        }

        private void RemapLayer(List<int> layer)
        {
            if (layer == null) return;
            for (int i = 0; i < layer.Count; i++)
            {
                int oldIndex = layer[i];
                layer[i] = oldIndex >= 0 && oldIndex < indexedPalette.Count && indexedPalette[oldIndex] != null ? palette.IndexOf(indexedPalette[oldIndex]) : -1;
            }
        }

        private static List<int> ResizeLayer(List<int> old, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var result = new List<int>(newWidth * newHeight);
            for (int i = 0; i < newWidth * newHeight; i++) result.Add(-1);
            if (old == null || oldWidth < 1 || oldHeight < 1) return result;
            for (int y = 0; y < Mathf.Min(oldHeight, newHeight); y++)
                for (int x = 0; x < Mathf.Min(oldWidth, newWidth); x++)
                {
                    int previous = y * oldWidth + x;
                    if (previous < old.Count) result[y * newWidth + x] = old[previous];
                }
            return result;
        }

        private static List<T> ResizeValues<T>(List<T> old, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var result = new List<T>(newWidth * newHeight);
            for (int i = 0; i < newWidth * newHeight; i++) result.Add(default);
            if (old == null || oldWidth < 1 || oldHeight < 1) return result;
            for (int y = 0; y < Mathf.Min(oldHeight, newHeight); y++)
                for (int x = 0; x < Mathf.Min(oldWidth, newWidth); x++)
                {
                    int previous = y * oldWidth + x;
                    if (previous < old.Count) result[y * newWidth + x] = old[previous];
                }
            return result;
        }

        private void OnValidate()
        {
            cellSize = float.IsFinite(cellSize) ? Mathf.Max(0.1f, cellSize) : 1f;
            EnsureStorage();
        }
    }
}
