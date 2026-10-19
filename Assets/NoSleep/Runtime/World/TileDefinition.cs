using UnityEngine;

namespace NoSleep
{
    [CreateAssetMenu(fileName = "Tile", menuName = "No Sleep/Tile")]
    public sealed class TileDefinition : ScriptableObject
    {
        public string displayName = "Tile";
        public GameObject prefab;
        public Material material;
        public bool walkable = true;
        [Min(0.01f)] public float height = 0.15f;
        public Color fallbackColor = new Color(0.4f, 0.5f, 0.55f);
    }
}
