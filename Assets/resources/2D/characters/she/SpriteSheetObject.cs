using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetObject", menuName = "Scriptable Objects/SpriteSheetObject")]
public class SpriteSheetObject : ScriptableObject
{
    public string Name;
    public Sprite[] Sprites;
}
