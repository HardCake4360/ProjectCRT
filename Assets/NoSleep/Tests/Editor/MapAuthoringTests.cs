using System;
using System.Collections.Generic;
using NUnit.Framework;
using NoSleep.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class MapAuthoringTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        MapDefinition source, target;
        TileDefinition floor, decoration, other;
        EventDefinition eventDefinition;
        string ownedAssetFolder;

        [SetUp]
        public void SetUp()
        {
            floor = Asset<TileDefinition>(); decoration = Asset<TileDefinition>(); other = Asset<TileDefinition>();
            eventDefinition = Asset<EventDefinition>();
            eventDefinition.pages.Add(new EventPage { runOnce = true }); eventDefinition.EnsureStableIds();
            source = Map("source"); target = Map("target");
        }
        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in assets) if (asset != null && !AssetDatabase.Contains(asset)) Object.DestroyImmediate(asset);
            assets.Clear();
            if (ownedAssetFolder != null)
            {
                Assert.That(ownedAssetFolder.StartsWith("Assets/NoSleepMapAuthoringTests-", StringComparison.Ordinal), Is.True);
                Assert.That(ownedAssetFolder.Length, Is.EqualTo("Assets/NoSleepMapAuthoringTests-".Length + 32));
                AssetDatabase.DeleteAsset(ownedAssetFolder);
            }
        }

        [Test]
        public void OldMapSchemaDefaultsToZeroWithoutChangingTilesOrMovement()
        {
            var old = Asset<MapDefinition>();
            JsonUtility.FromJsonOverwrite("{\"mapId\":\"legacy\",\"width\":2,\"height\":2,\"storedWidth\":2,\"storedHeight\":2," +
                "\"groundTiles\":[-1,-1,-1,-1],\"decorationTiles\":[-1,-1,-1,-1]}", old);
            old.SetTile(Vector2Int.zero, floor); old.SetTile(Vector2Int.one, decoration, TileLayer.Decoration);
            Assert.That(old.GetCellVisual(Vector2Int.zero).quarterTurns, Is.Zero);
            Assert.That(old.GetCellVisual(Vector2Int.one, TileLayer.Decoration).heightOffset, Is.Zero);
            Assert.That(old.GetRegionId(Vector2Int.one), Is.Zero);
            Assert.That(old.GetTile(Vector2Int.zero), Is.SameAs(floor));
            Assert.That(old.GetTile(Vector2Int.one, TileLayer.Decoration), Is.SameAs(decoration));
            Assert.That(old.IsWalkable(Vector2Int.zero), Is.True);
            Assert.That(old.CellToWorld(Vector2Int.one).y, Is.Zero);
            Assert.That(SaveService.CurrentVersion, Is.EqualTo(4));
        }

        [Test]
        public void ResizePreservesBothVisualLayersAndRegionsAcrossStrideChangesAndClipsPermanently()
        {
            var cell = new Vector2Int(3, 2);
            source.SetCellVisual(cell, Visual(1, 1.75f));
            source.SetCellVisual(cell, Visual(3, -2), TileLayer.Decoration); source.SetRegionId(cell, 42);
            source.Resize(7, 6);
            Assert.That(source.GetTile(cell), Is.SameAs(floor));
            Assert.That(source.GetCellVisual(cell).heightOffset, Is.EqualTo(1.75f));
            Assert.That(source.GetCellVisual(cell, TileLayer.Decoration).quarterTurns, Is.EqualTo(3));
            Assert.That(source.GetRegionId(cell), Is.EqualTo(42));
            Assert.That(source.GetRegionId(new Vector2Int(6, 5)), Is.Zero);
            Assert.That(source.GetCellVisual(new Vector2Int(6, 5)).heightOffset, Is.Zero);
            source.Resize(2, 2); source.Resize(7, 6);
            Assert.That(source.GetTile(cell), Is.Null);
            Assert.That(source.GetRegionId(cell), Is.Zero);
            Assert.That(source.GetCellVisual(cell).quarterTurns, Is.Zero);
        }

        [Test]
        public void CopyAcrossDifferentPalettesPreservesAssetsBothLayersTransformsRegionsAndIndependentEvents()
        {
            var origin = Vector2Int.one;
            source.SetTile(origin, decoration, TileLayer.Decoration);
            source.SetCellVisual(origin, Visual(1, 0.8f)); source.SetCellVisual(origin, Visual(2, -0.2f), TileLayer.Decoration);
            source.SetRegionId(origin, 9);
            source.events.Add(new MapEventPlacement { id = "keeper", cell = origin, definition = eventDefinition, yaw = 45 });
            target.palette.Insert(0, other); target.SynchronizePalette();
            target.events.Add(new MapEventPlacement { id = "keep", cell = Vector2Int.zero, definition = eventDefinition });
            MapRegionClipboard clipboard = MapEditOperations.CopyRegion(source, new RectInt(1, 1, 2, 2));
            source.events[0].cell = new Vector2Int(4, 4); source.events[0].yaw = 180;
            source.SetCellVisual(origin, Visual(0, 7)); source.SetRegionId(origin, 21);
            List<string> ids = MapEditOperations.PasteRegion(target, clipboard, new Vector2Int(2, 2));
            Assert.That(ids.Count, Is.EqualTo(1));
            Assert.That(target.events.Count, Is.EqualTo(2), "Existing target placements must remain.");
            MapEventPlacement pasted = target.events[1];
            Assert.That(pasted.id, Is.Not.EqualTo("keeper")); Assert.That(pasted.id, Is.Not.EqualTo("keep"));
            Assert.That(pasted.definition, Is.SameAs(eventDefinition)); Assert.That(pasted.cell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(pasted.yaw, Is.EqualTo(45)); Assert.That(pasted, Is.Not.SameAs(source.events[0]));
            Assert.That(target.GetTile(new Vector2Int(2, 2)), Is.SameAs(floor));
            Assert.That(target.GetTile(new Vector2Int(2, 2), TileLayer.Decoration), Is.SameAs(decoration));
            Assert.That(target.GetTile(Vector2Int.zero), Is.SameAs(floor), "Remapping cannot alter previously painted tiles.");
            Assert.That(target.GetCellVisual(new Vector2Int(2, 2)).heightOffset, Is.EqualTo(0.8f));
            Assert.That(target.GetCellVisual(new Vector2Int(2, 2), TileLayer.Decoration).quarterTurns, Is.EqualTo(2));
            Assert.That(target.GetRegionId(new Vector2Int(2, 2)), Is.EqualTo(9));
            MapEditOperations.PasteRegion(target, clipboard, Vector2Int.zero);
            Assert.That(target.events[2].id, Is.Not.EqualTo(pasted.id));
            Assert.That(target.GetCellVisual(Vector2Int.zero).heightOffset, Is.EqualTo(0.8f), "Repeated paste must retain the captured snapshot.");
            Assert.That(source.GetRegionId(origin), Is.EqualTo(21));
        }

        [Test]
        public void TilesOnlyClipboardDoesNotCopyOrReplacePlacements()
        {
            source.events.Add(new MapEventPlacement { id = "keeper", cell = Vector2Int.zero, definition = eventDefinition });
            MapRegionClipboard clipboard = MapEditOperations.CopyRegion(source, new RectInt(0, 0, 1, 1), false);
            Assert.That(clipboard.EventCount, Is.Zero);
            Assert.That(MapEditOperations.PasteRegion(target, clipboard, Vector2Int.one).Count, Is.Zero);
            Assert.That(target.events, Is.Empty);
        }

        [Test]
        public void OutsidePasteRejectsWholeOperationIncludingPaletteAndEventChanges()
        {
            source.SetTile(Vector2Int.zero, decoration);
            source.events.Add(new MapEventPlacement { id = "keeper", cell = Vector2Int.one, definition = eventDefinition });
            var clipboard = MapEditOperations.CopyRegion(source, new RectInt(0, 0, 2, 2));
            string before = JsonUtility.ToJson(target);
            Assert.Throws<ArgumentException>(() => MapEditOperations.PasteRegion(target, clipboard, new Vector2Int(4, 4)));
            Assert.That(JsonUtility.ToJson(target), Is.EqualTo(before));
            Assert.That(target.palette, Has.No.Member(decoration));
        }

        [Test]
        public void InvalidVisualAfterPaintingCandidateDoesNotChangeLivePaletteOrTiles()
        {
            string before = JsonUtility.ToJson(target);
            Assert.Throws<ArgumentException>(() => MapEditOperations.PaintRegion(target, new RectInt(0, 0, 2, 2), decoration, TileLayer.Ground, Visual(1, float.NaN)));
            Assert.That(JsonUtility.ToJson(target), Is.EqualTo(before));
        }

        [Test]
        public void OneUndoAndRedoRestoreEntirePasteAndKeepGeneratedPlacementIdentity()
        {
            source.SetTile(Vector2Int.zero, decoration); source.SetRegionId(Vector2Int.zero, 27);
            source.SetCellVisual(Vector2Int.zero, Visual(3, 2));
            source.events.Add(new MapEventPlacement { id = "once", cell = Vector2Int.zero, definition = eventDefinition });
            var clipboard = MapEditOperations.CopyRegion(source, new RectInt(0, 0, 2, 2));
            string before = JsonUtility.ToJson(target);
            MapEditOperations.PasteRegion(target, clipboard, Vector2Int.one);
            string after = JsonUtility.ToJson(target);
            Assert.That(after, Is.Not.EqualTo(before));
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(target), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(JsonUtility.ToJson(target), Is.EqualTo(after));
        }

        [Test]
        public void SelectionPaintRegionAndVisualChangesStayWithinTheRectangle()
        {
            var rectangle = new RectInt(1, 2, 2, 2);
            MapEditOperations.PaintRegion(target, rectangle, decoration, TileLayer.Decoration, Visual(-1, 0.5f));
            MapEditOperations.SetRegionIds(target, rectangle, 12);
            Assert.That(target.GetTile(new Vector2Int(1, 2), TileLayer.Decoration), Is.SameAs(decoration));
            Assert.That(target.GetCellVisual(new Vector2Int(2, 3), TileLayer.Decoration).quarterTurns, Is.EqualTo(3));
            Assert.That(target.GetRegionId(new Vector2Int(2, 3)), Is.EqualTo(12));
            Assert.That(target.GetTile(Vector2Int.zero, TileLayer.Decoration), Is.Null);
            Assert.That(target.GetRegionId(new Vector2Int(3, 3)), Is.Zero);
            MapEditOperations.PaintRegion(target, rectangle, null, TileLayer.Decoration);
            Assert.That(target.GetCellVisual(new Vector2Int(2, 3), TileLayer.Decoration).heightOffset, Is.Zero);
            Assert.That(target.GetRegionId(new Vector2Int(2, 3)), Is.EqualTo(12), "Erasing one visual layer must preserve region IDs.");
        }

        [Test]
        public void MultiEventCloneIsIndependentHasFreshIDsAndOneUndo()
        {
            source.events.Add(new MapEventPlacement { id = "a", cell = Vector2Int.one, definition = eventDefinition, yaw = 30 });
            source.events.Add(new MapEventPlacement { id = "b", cell = new Vector2Int(1, 2), definition = eventDefinition, yaw = 90 });
            List<string> ids = MapEditOperations.CloneEvents(source, new[] { "a", "b" }, Vector2Int.right);
            Assert.That(ids.Count, Is.EqualTo(2)); Assert.That(new HashSet<string>(ids).Count, Is.EqualTo(2));
            Assert.That(ids, Does.Not.Contain("a")); Assert.That(ids, Does.Not.Contain("b"));
            Assert.That(source.events[2].cell, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(source.events[3].yaw, Is.EqualTo(90)); Assert.That(source.events[2].definition, Is.SameAs(eventDefinition));
            source.events[2].yaw = 180; Assert.That(source.events[0].yaw, Is.EqualTo(30));
            Undo.PerformUndo(); Assert.That(source.events.Count, Is.EqualTo(2));
        }

        [Test]
        public void BatchAndMultiCloneOutsideBoundsFailWithoutPartialPlacements()
        {
            string before = JsonUtility.ToJson(target);
            Assert.Throws<ArgumentException>(() => MapEditOperations.BatchPlaceEvents(target, new[] { Vector2Int.one, new Vector2Int(10, 10) }, eventDefinition));
            Assert.That(JsonUtility.ToJson(target), Is.EqualTo(before));
            source.events.Add(new MapEventPlacement { id = "a", cell = Vector2Int.one, definition = eventDefinition });
            source.events.Add(new MapEventPlacement { id = "b", cell = new Vector2Int(4, 4), definition = eventDefinition });
            before = JsonUtility.ToJson(source);
            Assert.Throws<ArgumentException>(() => MapEditOperations.CloneEvents(source, new[] { "a", "b" }, Vector2Int.right));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
        }

        [Test]
        public void WholeMapCloneKeepsDefinitionsAndTilesButSeparatesAllDurablePlacementKeys()
        {
            source.SetRegionId(Vector2Int.one, 17); source.SetCellVisual(Vector2Int.one, Visual(2, -1));
            source.events.Add(new MapEventPlacement { id = "once", cell = Vector2Int.one, definition = eventDefinition });
            MapDefinition copy = MapEditOperations.CloneMap(source); assets.Add(copy);
            Assert.That(copy.mapId, Is.Not.EqualTo(source.mapId)); Assert.That(copy.events[0].id, Is.Not.EqualTo("once"));
            Assert.That(copy.events[0].definition, Is.SameAs(eventDefinition));
            Assert.That(copy.GetTile(Vector2Int.one), Is.SameAs(floor)); Assert.That(copy.GetRegionId(Vector2Int.one), Is.EqualTo(17));
            copy.events[0].cell = Vector2Int.zero; copy.SetRegionId(Vector2Int.one, 2); copy.SetCellVisual(Vector2Int.one, Visual(0, 9)); copy.palette.Add(other);
            Assert.That(source.events[0].cell, Is.EqualTo(Vector2Int.one)); Assert.That(source.GetRegionId(Vector2Int.one), Is.EqualTo(17));
            Assert.That(source.GetCellVisual(Vector2Int.one).heightOffset, Is.EqualTo(-1)); Assert.That(source.palette, Has.No.Member(other));
            var state = new GameState(); state.SetSelfSwitch(source.mapId + "/once", "A", true);
            Assert.That(state.GetSelfSwitch(copy.mapId + "/" + copy.events[0].id, "A"), Is.False);
        }

        [Test]
        public void MapAssetReloadPreservesAuthoringDataReferencesAndItsMetaGUID()
        {
            ownedAssetFolder = "Assets/NoSleepMapAuthoringTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", ownedAssetFolder.Substring("Assets/".Length));
            string tilePath = ownedAssetFolder + "/Floor.asset", eventPath = ownedAssetFolder + "/Event.asset", mapPath = ownedAssetFolder + "/Map.asset";
            AssetDatabase.CreateAsset(floor, tilePath); AssetDatabase.CreateAsset(eventDefinition, eventPath); AssetDatabase.CreateAsset(source, mapPath);
            source.SetCellVisual(Vector2Int.one, Visual(3, 1.25f)); source.SetRegionId(Vector2Int.one, 81);
            MapEditOperations.BatchPlaceEvents(source, new[] { Vector2Int.one, new Vector2Int(2, 2) }, eventDefinition);
            AssetDatabase.SaveAssets(); string guid = AssetDatabase.AssetPathToGUID(mapPath);
            string id = source.mapId, eventId = source.events[0].id;
            assets.Remove(source); Resources.UnloadAsset(source);
            MapDefinition loaded = AssetDatabase.LoadAssetAtPath<MapDefinition>(mapPath);
            Assert.That(loaded.mapId, Is.EqualTo(id)); Assert.That(loaded.events[0].id, Is.EqualTo(eventId));
            Assert.That(loaded.events[0].definition, Is.SameAs(eventDefinition)); Assert.That(loaded.GetTile(Vector2Int.one), Is.SameAs(floor));
            Assert.That(loaded.GetCellVisual(Vector2Int.one).heightOffset, Is.EqualTo(1.25f)); Assert.That(loaded.GetCellVisual(Vector2Int.one).quarterTurns, Is.EqualTo(3));
            Assert.That(loaded.GetRegionId(Vector2Int.one), Is.EqualTo(81)); Assert.That(AssetDatabase.AssetPathToGUID(mapPath), Is.EqualTo(guid));
        }

        T Asset<T>() where T : ScriptableObject { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        MapDefinition Map(string id) { var map = Asset<MapDefinition>(); map.mapId = id; map.Resize(5, 5); map.Fill(floor); return map; }
        static CellVisualTransform Visual(int turns, float height) => new CellVisualTransform { quarterTurns = turns, heightOffset = height };
    }
}
