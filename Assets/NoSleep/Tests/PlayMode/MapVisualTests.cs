using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class MapVisualTests
    {
        MapDefinition map;
        TileDefinition floor, wall;
        GameObject host, prefab;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            map = ScriptableObject.CreateInstance<MapDefinition>(); map.mapId = "visual_test"; map.Resize(3, 3);
            floor = ScriptableObject.CreateInstance<TileDefinition>(); floor.height = 0.2f;
            wall = ScriptableObject.CreateInstance<TileDefinition>(); wall.walkable = false; wall.height = 2;
            map.Fill(floor); map.SetTile(new Vector2Int(2, 2), wall, TileLayer.Decoration);
            host = new GameObject("No Sleep cell visual test");
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (prefab != null) Object.Destroy(prefab);
            if (map != null) Object.Destroy(map);
            if (floor != null) Object.Destroy(floor);
            if (wall != null) Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CubeVisualOffsetsAndQuarterTurnsDoNotChangeFloorCollisionOrGridOccupancy()
        {
            TileMapRuntime runtime = host.AddComponent<TileMapRuntime>(); runtime.Load(map);
            var cell = Vector2Int.one;
            Transform originalHolder = Holder(cell, TileLayer.Ground);
            BoxCollider originalCollider = originalHolder.GetComponent<BoxCollider>();
            Physics.SyncTransforms(); Bounds floorBounds = originalCollider.bounds;
            Transform originalWall = Holder(new Vector2Int(2, 2), TileLayer.Decoration);
            Bounds wallBounds = originalWall.GetComponent<BoxCollider>().bounds;
            map.SetCellVisual(cell, new CellVisualTransform { quarterTurns = 1, heightOffset = 1.5f });
            map.SetCellVisual(new Vector2Int(2, 2), new CellVisualTransform { quarterTurns = 3, heightOffset = -3 }, TileLayer.Decoration);
            map.SetRegionId(cell, 77);
            runtime.Load(map); yield return null; Physics.SyncTransforms();
            Transform holder = Holder(cell, TileLayer.Ground), visual = holder.Find("Visual");
            Assert.That(holder.localPosition.y, Is.Zero); Assert.That(holder.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localPosition.y, Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(0.001f));
            AssertBounds(holder.GetComponent<BoxCollider>().bounds, floorBounds);
            AssertBounds(Holder(new Vector2Int(2, 2), TileLayer.Decoration).GetComponent<BoxCollider>().bounds, wallBounds);
            Assert.That(holder.GetComponent<BoxCollider>().bounds.max.y, Is.EqualTo(0).Within(0.0001f));
            Assert.That(runtime.CellToWorld(cell), Is.EqualTo(new Vector3(1, 0, 1)));
            Assert.That(runtime.CanOccupy(new Vector3(1, 0, 1), 0.2f), Is.True);
            Assert.That(runtime.CanOccupy(new Vector3(2, 0, 2), 0.2f), Is.False);
            Assert.That(runtime.CanOccupy(new Vector3(-0.45f, 0, 0), 0.2f), Is.False);
        }

        [UnityTest]
        public IEnumerator PrefabTransformsComposeWithCellVisualsWhileImportedCollisionAndSourceStayUntouched()
        {
            prefab = new GameObject("Cell visual prefab source");
            prefab.transform.localRotation = Quaternion.Euler(0, 25, 0);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.transform.SetParent(prefab.transform, false);
            mesh.transform.localPosition = Vector3.up * 2.5f;
            BoxCollider sourceCollider = mesh.GetComponent<BoxCollider>(); sourceCollider.center = new Vector3(0, 3, 0);
            Rigidbody sourceBody = mesh.AddComponent<Rigidbody>(); sourceBody.isKinematic = true; sourceBody.useGravity = true;
            floor.prefab = prefab;
            map.SetTile(Vector2Int.one, floor, TileLayer.Decoration);
            map.SetCellVisual(Vector2Int.one, new CellVisualTransform { quarterTurns = 2, heightOffset = -0.75f });
            map.SetCellVisual(Vector2Int.one, new CellVisualTransform { quarterTurns = 3, heightOffset = 1.25f }, TileLayer.Decoration);
            TileMapRuntime runtime = host.AddComponent<TileMapRuntime>(); runtime.Load(map);
            yield return null; Physics.SyncTransforms();
            Transform ground = Holder(Vector2Int.one, TileLayer.Ground), decoration = Holder(Vector2Int.one, TileLayer.Decoration);
            Assert.That(ground.Find("Visual").localPosition.y, Is.EqualTo(-0.75f).Within(0.0001f));
            Assert.That(decoration.Find("Visual").localPosition.y, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(Quaternion.Angle(ground.Find("Visual").localRotation, Quaternion.Euler(0, 205, 0)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(decoration.Find("Visual").localRotation, Quaternion.Euler(0, 295, 0)), Is.LessThan(0.001f));
            foreach (Collider imported in ground.Find("Visual").GetComponentsInChildren<Collider>(true)) Assert.That(imported.enabled, Is.False);
            foreach (Collider imported in decoration.Find("Visual").GetComponentsInChildren<Collider>(true)) Assert.That(imported.enabled, Is.False);
            Assert.That(decoration.GetComponent<BoxCollider>(), Is.Null, "Walkable decoration adds no grid collision.");
            Assert.That(ground.GetComponent<BoxCollider>().bounds.max.y, Is.EqualTo(0).Within(0.0001f));
            Assert.That(runtime.CanOccupy(runtime.CellToWorld(Vector2Int.one), 0.2f), Is.True);
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(prefab.transform.localRotation, Quaternion.Euler(0, 25, 0)), Is.LessThan(0.001f));
            Assert.That(sourceCollider.enabled, Is.True); Assert.That(sourceCollider.center.y, Is.EqualTo(3));
            Assert.That(sourceBody.useGravity, Is.True, "Disabling cloned rigidbodies cannot modify the authored prefab source.");
        }

        Transform Holder(Vector2Int cell, TileLayer layer)
        {
            string prefix = layer + " [" + cell.x + "," + cell.y + "] ";
            foreach (Transform child in host.GetComponentsInChildren<Transform>())
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal)) return child;
            Assert.Fail("Missing generated tile holder: " + prefix); return null;
        }
        static void AssertBounds(Bounds actual, Bounds expected)
        {
            Assert.That(Vector3.Distance(actual.center, expected.center), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(actual.size, expected.size), Is.LessThan(0.0001f));
        }
    }
}
