using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class MapTests
    {
        private MapDefinition map;
        private TileDefinition floor;
        private TileDefinition wall;

        [SetUp]
        public void SetUp()
        {
            map = ScriptableObject.CreateInstance<MapDefinition>();
            map.Resize(3, 2);
            floor = ScriptableObject.CreateInstance<TileDefinition>();
            wall = ScriptableObject.CreateInstance<TileDefinition>();
            wall.walkable = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(map);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void Resize_PreservesCoordinatesAcrossStrideChanges_AndCreatesEmptyCells()
        {
            map.SetTile(new Vector2Int(2, 1), floor);
            map.SetTile(new Vector2Int(1, 1), wall, TileLayer.Decoration);
            map.Resize(5, 4);
            Assert.That(map.GetTile(new Vector2Int(2, 1)), Is.SameAs(floor));
            Assert.That(map.GetTile(new Vector2Int(1, 1), TileLayer.Decoration), Is.SameAs(wall));
            Assert.That(map.GetTile(new Vector2Int(3, 1)), Is.Null);
            Assert.That(map.GetTile(new Vector2Int(2, 0)), Is.Null);
            map.Resize(2, 2);
            map.Resize(5, 4);
            Assert.That(map.GetTile(new Vector2Int(2, 1)), Is.Null, "Clipped tiles must not reappear after a later expansion.");
        }

        [Test]
        public void Walkability_RequiresFloor_AndChecksDecoration()
        {
            var cell = new Vector2Int(1, 1);
            Assert.That(map.IsWalkable(cell), Is.False);
            map.SetTile(cell, floor);
            Assert.That(map.IsWalkable(cell), Is.True);
            map.SetTile(cell, wall, TileLayer.Decoration);
            Assert.That(map.IsWalkable(cell), Is.False);
            map.SetTile(cell, null, TileLayer.Decoration);
            Assert.That(map.IsWalkable(cell), Is.True);
            Assert.That(map.IsWalkable(new Vector2Int(-1, 0)), Is.False);
        }

        [Test]
        public void Coordinates_UseCellCentersAndNegativeBoundaryCorrectly()
        {
            map.cellSize = 2f;
            var cell = new Vector2Int(2, 1);
            Assert.That(map.CellToWorld(cell), Is.EqualTo(new Vector3(4f, 0f, 2f)));
            Assert.That(map.WorldToCell(map.CellToWorld(cell)), Is.EqualTo(cell));
            Assert.That(map.WorldToCell(new Vector3(-0.99f, 100f, 0f)), Is.EqualTo(Vector2Int.zero));
            Assert.That(map.WorldToCell(new Vector3(-1.01f, 0f, 0f)), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void PaletteReorder_PreservesPaintedTileReferences_AndRemovalClearsOnlyThatTile()
        {
            map.SetTile(Vector2Int.zero, floor);
            map.SetTile(Vector2Int.right, wall);
            map.palette.Reverse();
            map.SynchronizePalette();
            Assert.That(map.GetTile(Vector2Int.zero), Is.SameAs(floor));
            Assert.That(map.GetTile(Vector2Int.right), Is.SameAs(wall));
            map.palette.Remove(wall);
            map.SynchronizePalette();
            Assert.That(map.GetTile(Vector2Int.zero), Is.SameAs(floor));
            Assert.That(map.GetTile(Vector2Int.right), Is.Null);
        }

        [Test]
        public void CanOccupy_ChecksPlayerRadiusAtMapEdges_AndIgnoresDistantCorner()
        {
            map.Resize(3, 3);
            map.Fill(floor);
            var root = new GameObject("Map test");
            try
            {
                TileMapRuntime runtime = root.AddComponent<TileMapRuntime>();
                runtime.Load(map);
                Assert.That(runtime.CanOccupy(Vector3.zero, 0.23f), Is.True);
                Assert.That(runtime.CanOccupy(new Vector3(-0.4f, 0f, 0f), 0.23f), Is.False);
                map.SetTile(new Vector2Int(2, 2), wall);
                Assert.That(runtime.CanOccupy(new Vector3(1.25f, 0f, 1.25f), 0.3f), Is.True, "A blocked bounding-box corner outside the circle must not block movement.");
                Assert.That(runtime.CanOccupy(new Vector3(1.4f, 0f, 1.4f), 0.3f), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PrefabVisualCollision_DoesNotChangeFloorTopOrWalkableDecoration_OrSource()
        {
            map.Resize(1, 1);
            var prefabSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefabSource.GetComponent<BoxCollider>().center = Vector3.up * 3f;
            floor.prefab = prefabSource;
            map.SetTile(Vector2Int.zero, floor);
            map.SetTile(Vector2Int.zero, floor, TileLayer.Decoration);
            var root = new GameObject("Map collision test");
            try
            {
                root.AddComponent<TileMapRuntime>().Load(map);
                int enabledColliders = 0;
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                    if (collider.enabled)
                    {
                        enabledColliders++;
                        var box = collider as BoxCollider;
                        Assert.That(box, Is.Not.Null);
                        Assert.That(box.center.y + box.size.y * 0.5f, Is.EqualTo(0f).Within(0.0001f));
                    }
                Assert.That(enabledColliders, Is.EqualTo(1), "Only the ground holder supplies physical collision.");
                Assert.That(prefabSource.GetComponent<BoxCollider>().enabled, Is.True);
                Assert.That(prefabSource.GetComponent<BoxCollider>().center.y, Is.EqualTo(3f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefabSource);
            }
        }
    }
}
