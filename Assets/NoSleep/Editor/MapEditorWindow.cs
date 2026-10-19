using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum PaintTool { Brush, Erase, Spawn, Select, Region, Visual }
        private const float CellPixels = 30f;
        private const string PreviewRootName = "[No Sleep] Generated Map Preview";
        [SerializeField] private MapDefinition map;
        [SerializeField] private TileLayer layer;
        [SerializeField] private PaintTool tool;
        [SerializeField] private int brushIndex;
        [SerializeField] private Vector2Int selectedCell;
        [SerializeField] private RectInt selectedRegion = new RectInt(0, 0, 1, 1);
        [SerializeField] private string paletteSearch = "";
        [SerializeField] private bool showRegions, showCollision = true, includeCopiedEvents = true;
        [SerializeField] private int regionBrush, quarterTurns;
        [SerializeField] private float heightOffset;
        private Vector2Int selectionAnchor, cloneOffset = Vector2Int.right;
        private bool selecting;
        private string operationError;
        private EventDefinition placementBrush;
        private NpcDefinition npcBrush;
        private MapRegionClipboard regionClipboard;
        private MapEventClipboard eventClipboard;
        private readonly HashSet<string> selectedEvents = new HashSet<string>(StringComparer.Ordinal);
        private int newWidth, newHeight;
        private Vector2 contentScroll, gridScroll;
        private bool painting;
        private bool showPalette = true, showEvents = true, showValidation;
        private GameObject previewRoot;
        private List<string> validationErrors = new List<string>();
        private static bool AuthoringLocked => EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("No Sleep/Map Editor")]
        public static void Open() => GetWindow<MapEditorWindow>("No Sleep Map");

        public static void Open(MapDefinition definition, Vector2Int cell)
        {
            var window = GetWindow<MapEditorWindow>("No Sleep Map");
            window.RemovePreview(); window.map = definition; window.SyncDimensions(); window.selectedEvents.Clear();
            if (definition != null)
            {
                window.selectedCell = new Vector2Int(Mathf.Clamp(cell.x, 0, definition.width - 1), Mathf.Clamp(cell.y, 0, definition.height - 1));
                window.selectedRegion = new RectInt(window.selectedCell, Vector2Int.one);
                window.gridScroll = new Vector2(Mathf.Max(0, (window.selectedCell.x - 3) * CellPixels), Mathf.Max(0, (definition.height - 1 - window.selectedCell.y - 3) * CellPixels));
                window.tool = PaintTool.Select;
            }
            window.Repaint(); window.Focus();
        }

        private void OnEnable()
        {
            minSize = new Vector2(540f, 620f);
            SyncDimensions();
            Undo.undoRedoPerformed += OnUndo;
            SceneView.duringSceneGui += DrawSceneOverlay;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            SceneView.duringSceneGui -= DrawSceneOverlay;
            RemovePreview();
        }

        private void OnUndo()
        {
            SyncDimensions();
            selectedEvents.RemoveWhere(id => map == null || map.events == null || !map.events.Exists(value => value != null && value.id == id));
            if (previewRoot != null && map != null) previewRoot.GetComponent<TileMapRuntime>().Load(map);
            Repaint();
        }

        private void OnGUI()
        {
            if (AuthoringLocked) EditorGUILayout.HelpBox("Play 중에는 제작 에셋 편집을 잠급니다. 상태 조작은 이벤트 디버거를 사용하세요.", MessageType.Info);
            using var editScope = new EditorGUI.DisabledScope(AuthoringLocked);
            EditorGUILayout.BeginHorizontal();
            var selected = (MapDefinition)EditorGUILayout.ObjectField("맵", map, typeof(MapDefinition), false);
            if (selected != map) { RemovePreview(); map = selected; SyncDimensions(); selectedCell = Vector2Int.zero; selectedRegion = new RectInt(0, 0, 1, 1); selectedEvents.Clear(); validationErrors.Clear(); }
            if (GUILayout.Button("새 맵", GUILayout.Width(70f))) CreateMap();
            using (new EditorGUI.DisabledScope(map == null)) if (GUILayout.Button("맵 복제", GUILayout.Width(70f))) CloneMap();
            if (GUILayout.Button("선택", GUILayout.Width(55f)) && map != null) Selection.activeObject = map;
            EditorGUILayout.EndHorizontal();
            if (map == null)
            {
                EditorGUILayout.HelpBox("맵 에셋을 선택하거나 새 맵을 만드세요. 타일은 Create > No Sleep > Tile 메뉴에서 만들 수 있습니다.", MessageType.Info);
                return;
            }

            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
            DrawSettings();
            DrawPalette();
            DrawTools();
            DrawGrid();
            DrawEventPlacements();
            DrawActions();
            if (!string.IsNullOrEmpty(operationError)) EditorGUILayout.HelpBox(operationError, MessageType.Error);
            EditorGUILayout.EndScrollView();
            if (Event.current.rawType == EventType.MouseUp) { painting = false; selecting = false; }
            HandleClipboardKeys();
        }

        private void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            string id = EditorGUILayout.TextField("맵 ID", map.mapId);
            string title = EditorGUILayout.TextField("표시 이름", map.displayName);
            float size = EditorGUILayout.FloatField("타일 크기", map.cellSize);
            Vector2Int spawn = EditorGUILayout.Vector2IntField("시작 지점", map.spawnCell);
            if (EditorGUI.EndChangeCheck())
            {
                Record("Edit map settings");
                map.mapId = id;
                map.displayName = title;
                map.cellSize = float.IsFinite(size) ? Mathf.Max(0.1f, size) : 1f;
                map.spawnCell = spawn;
                Changed();
            }
            EditorGUILayout.BeginHorizontal();
            newWidth = EditorGUILayout.IntField("너비", newWidth);
            newHeight = EditorGUILayout.IntField("높이", newHeight);
            using (new EditorGUI.DisabledScope(newWidth < 1 || newHeight < 1 || newWidth > MapDefinition.MaximumSize || newHeight > MapDefinition.MaximumSize))
                if (GUILayout.Button("크기 적용", GUILayout.Width(85f)))
                {
                    Record("Resize map");
                    map.Resize(newWidth, newHeight);
                    selectedCell = new Vector2Int(Mathf.Clamp(selectedCell.x, 0, map.width - 1), Mathf.Clamp(selectedCell.y, 0, map.height - 1));
                    selectedRegion = new RectInt(selectedCell, Vector2Int.one);
                    Changed();
                }
            EditorGUILayout.EndHorizontal();
            if (newWidth < 1 || newHeight < 1 || newWidth > MapDefinition.MaximumSize || newHeight > MapDefinition.MaximumSize)
                EditorGUILayout.HelpBox("맵 크기는 1~256 사이로 입력하세요.", MessageType.Error);
            EditorGUILayout.LabelField("크기 축소 시 범위 밖 타일이 제거됩니다. 이벤트 좌표는 검사 결과로 확인하세요.", EditorStyles.miniLabel);
        }

        private void DrawPalette()
        {
            showPalette = EditorGUILayout.Foldout(showPalette, "타일 팔레트 · 검색 / 프리팹 미리보기", true);
            if (!showPalette) return;
            paletteSearch = EditorGUILayout.TextField("검색", paletteSearch);
            if (map.palette == null) { if (AuthoringLocked) return; map.palette = new List<TileDefinition>(); }
            for (int i = 0; i < map.palette.Count; i++)
            {
                TileDefinition paletteTile = map.palette[i];
                string searchable = paletteTile == null ? "빈 슬롯" : paletteTile.displayName + " " + paletteTile.name + " " + (paletteTile.prefab == null ? "" : paletteTile.prefab.name);
                if (!string.IsNullOrEmpty(paletteSearch) && searchable.IndexOf(paletteSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = i == brushIndex ? new Color(0.6f, 0.85f, 1f) : Color.white;
                if (GUILayout.Button((i + 1).ToString(), GUILayout.Width(30f))) { brushIndex = i; tool = PaintTool.Brush; }
                GUI.backgroundColor = Color.white;
                UnityEngine.Object thumbnailSource = paletteTile != null && paletteTile.prefab != null ? (UnityEngine.Object)paletteTile.prefab : paletteTile;
                Texture thumbnail = thumbnailSource == null ? null : AssetPreview.GetAssetPreview(thumbnailSource) ?? AssetPreview.GetMiniThumbnail(thumbnailSource);
                GUILayout.Label(thumbnail, GUILayout.Width(40f), GUILayout.Height(40f));
                var tile = (TileDefinition)EditorGUILayout.ObjectField(map.palette[i], typeof(TileDefinition), false);
                if (tile != map.palette[i])
                {
                    Record("Edit tile palette");
                    map.palette[i] = tile;
                    map.SynchronizePalette();
                    Changed();
                }
                if (GUILayout.Button("삭제", GUILayout.Width(45f)))
                {
                    Record("Remove tile from palette");
                    map.palette.RemoveAt(i);
                    map.SynchronizePalette();
                    brushIndex = Mathf.Clamp(brushIndex, 0, map.palette.Count - 1);
                    Changed();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("팔레트 슬롯 추가")) { Record("Add tile palette slot"); map.palette.Add(null); map.SynchronizePalette(); Changed(); }
            EditorGUILayout.HelpBox("팔레트에서 타일을 제거하거나 교체하면 기존 타일의 참조를 유지하고, 팔레트에서 없어진 타일의 칸을 비웁니다.", MessageType.Info);
        }

        private void DrawTools()
        {
            EditorGUILayout.Space(4f);
            layer = (TileLayer)GUILayout.Toolbar((int)layer, new[] { "지면", "장식" });
            tool = (PaintTool)GUILayout.Toolbar((int)tool, new[] { "브러시", "지우개", "시작", "사각 선택", "영역 ID", "시각 변형" });
            TileDefinition brush = CurrentBrush();
            EditorGUILayout.BeginHorizontal();
            showCollision = GUILayout.Toggle(showCollision, "충돌 표시"); showRegions = GUILayout.Toggle(showRegions, "영역 표시");
            includeCopiedEvents = GUILayout.Toggle(includeCopiedEvents, "이벤트 함께 복사");
            EditorGUILayout.EndHorizontal();
            quarterTurns = EditorGUILayout.Popup("셀 회전", quarterTurns, new[] { "0°", "90°", "180°", "270°" });
            heightOffset = EditorGUILayout.FloatField("시각적 높이", heightOffset);
            regionBrush = Mathf.Max(0, EditorGUILayout.IntField("영역 ID (0=없음)", regionBrush));
            EditorGUILayout.LabelField("회전·높이는 모델만 변경하며 이동 평면과 충돌은 Y=0 격자를 유지합니다.", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"선택 ({selectedRegion.x},{selectedRegion.y}) · {selectedRegion.width}×{selectedRegion.height} / Shift+드래그: 사각 선택");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(brush == null))
                if (GUILayout.Button("선택 칠하기")) RunEdit(() => MapEditOperations.PaintRegion(map, selectedRegion, brush, layer, BrushVisual()));
            if (GUILayout.Button("선택 지우기")) RunEdit(() => MapEditOperations.PaintRegion(map, selectedRegion, null, layer));
            if (GUILayout.Button("선택 영역 ID")) RunEdit(() => MapEditOperations.SetRegionIds(map, selectedRegion, regionBrush));
            if (GUILayout.Button("선택 시각 변형")) RunEdit(() => MapEditOperations.SetVisualTransforms(map, selectedRegion, layer, BrushVisual()));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("영역 복사")) CopyRegion();
            using (new EditorGUI.DisabledScope(regionClipboard == null)) if (GUILayout.Button("선택 칸에 붙여넣기")) PasteRegion();
            EditorGUILayout.EndHorizontal();
            if (regionClipboard != null) EditorGUILayout.LabelField($"클립보드 {regionClipboard.Width}×{regionClipboard.Height}, 이벤트 {regionClipboard.EventCount}개 · 양 레이어/시각 변형/영역 포함", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(brush == null))
                if (GUILayout.Button("레이어 채우기")) RunEdit(() => MapEditOperations.PaintRegion(map, new RectInt(0, 0, map.width, map.height), brush, layer, BrushVisual()));
            if (GUILayout.Button("레이어 비우기")) RunEdit(() => MapEditOperations.PaintRegion(map, new RectInt(0, 0, map.width, map.height), null, layer));
            EditorGUILayout.EndHorizontal();
            if (tool == PaintTool.Brush && brush == null) EditorGUILayout.HelpBox("팔레트에 타일을 지정하고 사용할 번호를 누르세요.", MessageType.Warning);
        }

        private void DrawGrid()
        {
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(Mathf.Clamp(position.height * 0.38f, 200f, 420f)));
            Rect rect = GUILayoutUtility.GetRect(map.width * CellPixels, map.height * CellPixels, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            int firstX = Mathf.Max(0, Mathf.FloorToInt(gridScroll.x / CellPixels) - 1);
            int lastX = Mathf.Min(map.width - 1, firstX + Mathf.CeilToInt(position.width / CellPixels) + 2);
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(gridScroll.y / CellPixels) - 1);
            int lastRow = Mathf.Min(map.height - 1, firstRow + Mathf.CeilToInt(420f / CellPixels) + 2);
            for (int row = firstRow; row <= lastRow; row++)
                for (int x = firstX; x <= lastX; x++)
                {
                    var cell = new Vector2Int(x, map.height - 1 - row);
                    Rect cellRect = new Rect(rect.x + x * CellPixels, rect.y + row * CellPixels, CellPixels - 1f, CellPixels - 1f);
                    TileDefinition tile = map.GetTile(cell, layer);
                    Color color = tile == null ? new Color(0.18f, 0.2f, 0.24f) : tile.fallbackColor;
                    int regionId = map.GetRegionId(cell);
                    if (showRegions && regionId > 0) color = Color.Lerp(color, RegionColor(regionId), 0.65f);
                    EditorGUI.DrawRect(cellRect, color);
                    if (showRegions && regionId > 0) GUI.Label(cellRect, regionId.ToString(), CenterStyle(Color.white));
                    if (showCollision && !map.IsWalkable(cell)) GUI.Label(showRegions ? new Rect(cellRect.x, cellRect.y, 14f, 14f) : cellRect, "×", CenterStyle(Color.white));
                    if (map.spawnCell == cell) GUI.Label(cellRect, "S", CenterStyle(Color.green));
                    if (HasEvent(cell)) GUI.Label(new Rect(cellRect.x + 15f, cellRect.y, 13f, 15f), "E", CenterStyle(Color.yellow));
                    if (selectedRegion.Contains(cell))
                    {
                        Handles.color = Color.cyan;
                        Handles.DrawAAPolyLine(2f, new Vector3(cellRect.x, cellRect.y), new Vector3(cellRect.xMax, cellRect.y), new Vector3(cellRect.xMax, cellRect.yMax), new Vector3(cellRect.x, cellRect.yMax), new Vector3(cellRect.x, cellRect.y));
                    }
                }
            Event evt = Event.current;
            if (!AuthoringLocked && (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                var cell = new Vector2Int(Mathf.FloorToInt((evt.mousePosition.x - rect.x) / CellPixels), map.height - 1 - Mathf.FloorToInt((evt.mousePosition.y - rect.y) / CellPixels));
                if (map.IsInBounds(cell))
                {
                    if (evt.type == EventType.MouseDown)
                    {
                        painting = false; selecting = tool == PaintTool.Select || evt.shift; selectionAnchor = cell;
                    }
                    selectedCell = selecting ? selectionAnchor : cell;
                    selectedRegion = selecting ? new RectInt(Mathf.Min(selectionAnchor.x, cell.x), Mathf.Min(selectionAnchor.y, cell.y), Mathf.Abs(cell.x - selectionAnchor.x) + 1, Mathf.Abs(cell.y - selectionAnchor.y) + 1) : new RectInt(cell, Vector2Int.one);
                    if (!selecting) PaintCell(cell);
                    Repaint();
                    evt.Use();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("좌측 하단=(0,0) / S=시작 / E=이벤트 / ×=이동 불가 / Ctrl+C,V=영역 복사·붙여넣기", EditorStyles.miniLabel);
        }

        private void DrawEventPlacements()
        {
            showEvents = EditorGUILayout.Foldout(showEvents, "이벤트 배치", true);
            if (!showEvents) return;
            if (map.events == null) { if (AuthoringLocked) return; map.events = new List<MapEventPlacement>(); }
            placementBrush = (EventDefinition)EditorGUILayout.ObjectField("일괄 배치할 정의", placementBrush, typeof(EventDefinition), false);
            npcBrush = (NpcDefinition)EditorGUILayout.ObjectField("선택적 NPC 프로필", npcBrush, typeof(NpcDefinition), false);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(placementBrush == null && npcBrush == null))
                if (GUILayout.Button("사각형 전체 배치")) RunEdit(() => SelectEvents(MapEditOperations.BatchPlaceEvents(map, selectedRegion, placementBrush, 0f, npcBrush)));
            if (GUILayout.Button("사각형 내 이벤트 선택"))
            {
                selectedEvents.Clear();
                foreach (var placement in map.events) if (placement != null && selectedRegion.Contains(placement.cell)) selectedEvents.Add(placement.id);
            }
            EditorGUILayout.EndHorizontal();
            cloneOffset = EditorGUILayout.Vector2IntField("복제 이동량", cloneOffset);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedEvents.Count == 0))
            {
                if (GUILayout.Button($"선택 {selectedEvents.Count}개 복제")) RunEdit(() => SelectEvents(MapEditOperations.CloneEvents(map, SelectedPlacementIds(), cloneOffset)));
                if (GUILayout.Button("이벤트 복사")) RunEdit(() => eventClipboard = MapEditOperations.CopyEvents(map, SelectedPlacementIds(), selectedCell));
                if (GUILayout.Button("선택 삭제")) { Record("Remove selected events"); map.events.RemoveAll(value => value != null && selectedEvents.Contains(value.id)); selectedEvents.Clear(); Changed(); }
            }
            using (new EditorGUI.DisabledScope(eventClipboard == null))
                if (GUILayout.Button("이벤트 붙여넣기")) RunEdit(() => SelectEvents(MapEditOperations.PasteEvents(map, eventClipboard, selectedCell)));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button($"선택 칸 ({selectedCell.x},{selectedCell.y})에 이벤트 추가"))
            {
                Record("Add map event");
                string id = "event_" + (map.events.Count + 1);
                int suffix = 1;
                while (map.events.Exists(p => p != null && p.id == id)) id = "event_" + (map.events.Count + 1) + "_" + suffix++;
                map.events.Add(new MapEventPlacement { id = id, cell = selectedCell, definition = placementBrush, npc = npcBrush });
                Changed();
            }
            for (int i = 0; i < map.events.Count; i++)
            {
                MapEventPlacement placement = map.events[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (placement == null)
                {
                    if (GUILayout.Button($"잘못된 이벤트 #{i + 1} 제거")) { Record("Remove invalid event"); map.events.RemoveAt(i); Changed(); EditorGUILayout.EndVertical(); break; }
                    EditorGUILayout.EndVertical();
                    continue;
                }
                bool chosen = EditorGUILayout.ToggleLeft($"{placement.id} ({placement.cell.x},{placement.cell.y})", selectedEvents.Contains(placement.id));
                if (chosen) selectedEvents.Add(placement.id); else selectedEvents.Remove(placement.id);
                EditorGUI.BeginChangeCheck();
                string id = EditorGUILayout.TextField("ID", placement.id);
                var definition = (EventDefinition)EditorGUILayout.ObjectField("이벤트 정의", placement.definition, typeof(EventDefinition), false);
                var npc = (NpcDefinition)EditorGUILayout.ObjectField("NPC 프로필 (선택)", placement.npc, typeof(NpcDefinition), false);
                Vector2Int cell = EditorGUILayout.Vector2IntField("좌표", placement.cell);
                float yaw = EditorGUILayout.FloatField("회전 Y", placement.yaw);
                if (EditorGUI.EndChangeCheck())
                {
                    Record("Edit map event");
                    selectedEvents.Remove(placement.id); if (chosen) selectedEvents.Add(id);
                    placement.id = id;
                    placement.definition = definition;
                    placement.npc = npc;
                    placement.cell = cell;
                    placement.yaw = float.IsFinite(yaw) ? yaw : 0f;
                    Changed();
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("선택 칸으로 이동")) { Record("Move map event"); placement.cell = selectedCell; Changed(); }
                if (GUILayout.Button("정의 편집") && definition != null) Selection.activeObject = definition;
                if (npc != null && GUILayout.Button("NPC 편집")) Selection.activeObject = npc;
                bool remove = GUILayout.Button("배치 삭제");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                if (remove) { Record("Remove map event"); selectedEvents.Remove(placement.id); map.events.RemoveAt(i); Changed(); break; }
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("맵 검사")) { validationErrors = map.Validate(); showValidation = true; }
            if (GUILayout.Button("에셋 저장")) AssetDatabase.SaveAssets();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("씬 미리보기")) GeneratePreview();
                if (GUILayout.Button("미리보기 제거")) RemovePreview();
            }
            EditorGUILayout.EndHorizontal();
            if (showValidation)
            {
                if (validationErrors.Count == 0) EditorGUILayout.HelpBox("맵 데이터 검사 통과. 이벤트 페이지와 플레이 동작은 씬에서 별도로 확인하세요.", MessageType.Info);
                foreach (string error in validationErrors) EditorGUILayout.HelpBox(error, MessageType.Warning);
            }
        }

        private void CreateMap()
        {
            if (AuthoringLocked) return;
            string path = EditorUtility.SaveFilePanelInProject("새 No Sleep 맵", "NewMap", "asset", "맵 에셋을 저장할 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<MapDefinition>();
            asset.mapId = Guid.NewGuid().ToString("N");
            asset.displayName = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.Resize(12, 10);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            map = asset;
            SyncDimensions();
            Selection.activeObject = asset;
        }

        private void CloneMap()
        {
            if (AuthoringLocked) return;
            string path = EditorUtility.SaveFilePanelInProject("맵 복제", map.name + "Copy", "asset", "새 맵/배치 ID를 가진 복사본을 저장합니다. 이벤트 정의는 공유합니다.");
            if (string.IsNullOrEmpty(path)) return;
            MapDefinition asset = MapEditOperations.CloneMap(map);
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets();
            RemovePreview(); map = asset; selectedEvents.Clear(); SyncDimensions(); Selection.activeObject = asset;
        }

        private void PaintCell(Vector2Int cell)
        {
            if (AuthoringLocked) return;
            if (!float.IsFinite(heightOffset)) { operationError = "시각적 높이는 유한한 수여야 합니다."; return; }
            if (tool == PaintTool.Brush && CurrentBrush() == null) return;
            if (!painting) { Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(map, "Paint map stroke"); painting = true; }
            if (tool == PaintTool.Spawn) map.spawnCell = cell;
            else if (tool == PaintTool.Region) map.SetRegionId(cell, regionBrush);
            else if (tool == PaintTool.Visual) map.SetCellVisual(cell, BrushVisual(), layer);
            else
            {
                map.SetTile(cell, tool == PaintTool.Erase ? null : CurrentBrush(), layer);
                map.SetCellVisual(cell, tool == PaintTool.Erase ? default : BrushVisual(), layer);
            }
            operationError = null; Changed();
        }

        private CellVisualTransform BrushVisual() => new CellVisualTransform { quarterTurns = quarterTurns, heightOffset = heightOffset };
        private void CopyRegion() => RunEdit(() => regionClipboard = MapEditOperations.CopyRegion(map, selectedRegion, includeCopiedEvents));
        private void PasteRegion() => RunEdit(() =>
        {
            SelectEvents(MapEditOperations.PasteRegion(map, regionClipboard, selectedCell, includeCopiedEvents));
            selectedRegion = new RectInt(selectedCell, new Vector2Int(regionClipboard.Width, regionClipboard.Height));
        });
        private void HandleClipboardKeys()
        {
            if (AuthoringLocked) return;
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || !(evt.control || evt.command) || EditorGUIUtility.editingTextField) return;
            if (evt.keyCode == KeyCode.C) { CopyRegion(); evt.Use(); }
            else if (evt.keyCode == KeyCode.V && regionClipboard != null) { PasteRegion(); evt.Use(); }
        }
        private void RunEdit(Action action)
        {
            if (AuthoringLocked) return;
            painting = false;
            try { action(); operationError = null; Changed(); }
            catch (ArgumentException error) { operationError = error.Message; Repaint(); }
        }
        private List<string> SelectedPlacementIds()
        {
            var ids = new List<string>();
            if (map.events != null) foreach (var placement in map.events) if (placement != null && selectedEvents.Contains(placement.id)) ids.Add(placement.id);
            return ids;
        }
        private void SelectEvents(List<string> ids) { selectedEvents.Clear(); foreach (string id in ids) selectedEvents.Add(id); }
        private void DrawSceneOverlay(SceneView view)
        {
            if (previewRoot == null || map == null || (!showRegions && !showCollision)) return;
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var cell = new Vector2Int(x, y); int regionId = map.GetRegionId(cell);
                    bool blocked = showCollision && !map.IsWalkable(cell);
                    if (!blocked && (!showRegions || regionId == 0)) continue;
                    Vector3 center = previewRoot.transform.TransformPoint(map.CellToWorld(cell)) + Vector3.up * 0.02f;
                    float half = map.cellSize * 0.48f;
                    Handles.color = blocked ? new Color(1, 0.2f, 0.2f, 0.8f) : RegionColor(regionId);
                    var corners = new[] { center + new Vector3(-half, 0, -half), center + new Vector3(-half, 0, half), center + new Vector3(half, 0, half), center + new Vector3(half, 0, -half) };
                    Handles.DrawSolidRectangleWithOutline(corners, new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.15f), Handles.color);
                    if (showRegions && regionId > 0) Handles.Label(center, regionId.ToString());
                }
        }
        private static Color RegionColor(int id) => Color.HSVToRGB((id * 0.618034f) % 1f, 0.65f, 0.9f);

        private void GeneratePreview()
        {
            RemovePreview();
            previewRoot = new GameObject(PreviewRootName) { hideFlags = HideFlags.DontSave };
            TileMapRuntime runtime = previewRoot.AddComponent<TileMapRuntime>();
            runtime.Load(map);
            Selection.activeGameObject = previewRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            SceneView.RepaintAll();
        }

        private void RemovePreview()
        {
            // Only remove the root this window created; never search by name and delete user objects.
            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewRoot = null;
            SceneView.RepaintAll();
        }

        private void SyncDimensions()
        {
            newWidth = map != null ? map.width : 12;
            newHeight = map != null ? map.height : 10;
        }

        private TileDefinition CurrentBrush() => map != null && map.palette != null && brushIndex >= 0 && brushIndex < map.palette.Count ? map.palette[brushIndex] : null;
        private bool HasEvent(Vector2Int cell) => map.events != null && map.events.Exists(p => p != null && p.cell == cell);
        private void Record(string name) { painting = false; Undo.IncrementCurrentGroup(); Undo.RegisterCompleteObjectUndo(map, name); }
        private void Changed() { EditorUtility.SetDirty(map); if (showValidation) validationErrors = map.Validate(); Repaint(); SceneView.RepaintAll(); }
        private static GUIStyle CenterStyle(Color color) => new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = color } };
    }
}
