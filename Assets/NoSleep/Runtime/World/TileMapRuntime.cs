using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public sealed class TileMapRuntime : MonoBehaviour
    {
        [SerializeField] private MapDefinition map;
        private GameSession session;
        private GameObject generatedRoot;
        private readonly Dictionary<Color, Material> fallbackMaterials = new Dictionary<Color, Material>();
        private readonly List<EventObject> eventObjects = new List<EventObject>();
        private readonly List<NpcActor> npcActors = new List<NpcActor>();
        public MapDefinition Map => map;
        public IReadOnlyList<EventObject> EventObjects => eventObjects;
        public IReadOnlyList<NpcActor> NpcActors => npcActors;

        public void Configure(GameSession gameSession) => session = gameSession;

        public void Load(MapDefinition definition)
        {
            ClearGenerated();
            map = definition;
            if (map == null) return;
            generatedRoot = new GameObject("Generated Map: " + map.mapId);
            generatedRoot.transform.SetParent(transform, false);
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    BuildTile(cell, TileLayer.Ground);
                    BuildTile(cell, TileLayer.Decoration);
                }
            if (map.events != null)
                foreach (MapEventPlacement placement in map.events)
                    if (placement != null && (placement.definition != null || placement.npc != null) && map.IsInBounds(placement.cell)) BuildEvent(placement);
            session?.Npcs?.BindMap(this, npcActors);
        }

        public Vector3 CellToWorld(Vector2Int cell) => map == null ? transform.position : transform.TransformPoint(map.CellToWorld(cell));
        public Vector2Int WorldToCell(Vector3 position) => map == null ? Vector2Int.zero : map.WorldToCell(transform.InverseTransformPoint(position));
        public bool IsWalkable(Vector2Int cell) => IsWalkable(cell, null);
        public bool IsWalkable(Vector2Int cell, NpcActor exclude) => map != null && map.IsWalkable(cell) &&
            !HasBlockingStaticEvent(cell, exclude?.Event) && !(session?.Npcs?.IsCellOccupied(cell, exclude, true) ?? false);

        public bool CanOccupy(Vector3 position, float radius, NpcActor exclude = null)
        {
            if (map == null) return false;
            Vector3 local = transform.InverseTransformPoint(position);
            // Tilemap roots use identity scale. Account for scale defensively for callers.
            float scale = Mathf.Min(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            radius = Mathf.Max(0f, radius) / Mathf.Max(0.0001f, scale);
            float size = Mathf.Max(0.1f, map.cellSize);
            int minX = Mathf.FloorToInt((local.x - radius) / size + 0.5f);
            int maxX = Mathf.FloorToInt((local.x + radius) / size + 0.5f);
            int minY = Mathf.FloorToInt((local.z - radius) / size + 0.5f);
            int maxY = Mathf.FloorToInt((local.z + radius) / size + 0.5f);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float nearX = Mathf.Clamp(local.x, (x - 0.5f) * size, (x + 0.5f) * size);
                    float nearY = Mathf.Clamp(local.z, (y - 0.5f) * size, (y + 0.5f) * size);
                    float dx = local.x - nearX, dz = local.z - nearY;
                    if (dx * dx + dz * dz <= radius * radius + 0.000001f && !IsWalkable(new Vector2Int(x, y), exclude)) return false;
                }
            return true;
        }

        public bool HasBlockingStaticEvent(Vector2Int cell, EventObject exclude = null)
        {
            foreach (EventObject obj in eventObjects)
                if (obj != null && obj != exclude && obj.GetComponent<NpcActor>() == null && obj.BlocksMovement && WorldToCell(obj.transform.position) == cell) return true;
            return false;
        }

        private void BuildTile(Vector2Int cell, TileLayer layer)
        {
            TileDefinition tile = map.GetTile(cell, layer);
            if (tile == null) return;
            var holder = new GameObject($"{layer} [{cell.x},{cell.y}] {tile.displayName}");
            holder.transform.SetParent(generatedRoot.transform, false);
            holder.transform.localPosition = map.CellToWorld(cell);
            holder.isStatic = true;
            float tileHeight = Mathf.Max(0.01f, tile.height);
            bool ground = layer == TileLayer.Ground;
            GameObject visual;
            if (tile.prefab != null)
            {
                visual = Instantiate(tile.prefab, holder.transform, false);
                visual.name = "Visual";
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(holder.transform, false);
                visual.transform.localPosition = new Vector3(0f, ground && tile.walkable ? -tileHeight * 0.5f : tileHeight * 0.5f, 0f);
                visual.transform.localScale = new Vector3(map.cellSize, tileHeight, map.cellSize);
                visual.GetComponent<Renderer>().sharedMaterial = tile.material != null ? tile.material : GetFallbackMaterial(tile.fallbackColor);
                RemoveObject(visual.GetComponent<Collider>());
            }
            // Only the model moves. Grid anchors and physical floor tops stay at Y=0.
            CellVisualTransform cellVisual = map.GetCellVisual(cell, layer);
            visual.transform.localPosition += Vector3.up * cellVisual.heightOffset;
            visual.transform.localRotation = Quaternion.Euler(0f, cellVisual.quarterTurns * 90f, 0f) * visual.transform.localRotation;
            visual.isStatic = true;
            foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true)) visualCollider.enabled = false;
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
            // Grid collision is independent of a model's pivot and its imported colliders.
            if (ground || !tile.walkable)
            {
                BoxCollider collider = holder.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, ground && tile.walkable ? -tileHeight * 0.5f : tileHeight * 0.5f, 0f);
                collider.size = new Vector3(map.cellSize, tileHeight, map.cellSize);
            }
        }

        private void BuildEvent(MapEventPlacement placement)
        {
            var root = new GameObject("Event: " + placement.id);
            root.transform.SetParent(generatedRoot.transform, false);
            root.transform.localPosition = map.CellToWorld(placement.cell);
            root.transform.localRotation = Quaternion.Euler(0f, placement.yaw, 0f);
            GameObject visual;
            GameObject prefab = placement.npc != null && placement.npc.prefab != null ? placement.npc.prefab : placement.definition?.prefab;
            if (prefab != null)
            {
                visual = Instantiate(prefab, root.transform, false);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                visual.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
                visual.GetComponent<Renderer>().sharedMaterial = GetFallbackMaterial(placement.npc != null ? placement.npc.fallbackColor : placement.definition.fallbackColor);
                RemoveObject(visual.GetComponent<Collider>());
            }
            visual.name = "Visual";
            // Placement collision follows the active event page, even for prefab visuals.
            foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true)) visualCollider.enabled = false;
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
            if (session == null) return; // Scene preview renders data without running events.
            EventObject obj = null;
            if (placement.definition != null)
            {
                SphereCollider touch = root.AddComponent<SphereCollider>();
                touch.center = new Vector3(0f, 0.6f, 0f);
                touch.radius = map.cellSize * 0.75f;
                touch.isTrigger = true;
                obj = root.AddComponent<EventObject>();
                obj.SetVisualRoot(visual);
                obj.Configure(session, placement.definition, map.mapId + "/" + placement.id);
                eventObjects.Add(obj);
            }
            if (placement.npc != null)
            {
                var actor = root.AddComponent<NpcActor>();
                actor.Configure(map, placement, obj);
                npcActors.Add(actor);
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private Material GetFallbackMaterial(Color color)
        {
            if (fallbackMaterials.TryGetValue(color, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "No Sleep Fallback", hideFlags = HideFlags.DontSave };
            material.color = color;
            fallbackMaterials[color] = material;
            return material;
        }

        private void ClearGenerated()
        {
            session?.Npcs?.OnMapChanging();
            eventObjects.Clear();
            npcActors.Clear();
            if (generatedRoot == null) return;
            generatedRoot.SetActive(false);
            RemoveObject(generatedRoot);
            generatedRoot = null;
        }

        private static void RemoveObject(Object obj)
        {
            if (obj == null) return;
            if (obj is Collider collider) collider.enabled = false;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        private void OnDestroy()
        {
            if (session != null && session.Map == this) session.Npcs?.OnMapChanging();
            foreach (Material material in fallbackMaterials.Values) RemoveObject(material);
            fallbackMaterials.Clear();
        }
    }
}
