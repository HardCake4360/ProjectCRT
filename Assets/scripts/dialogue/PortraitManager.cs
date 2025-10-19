using UnityEngine;
using System.Collections.Generic;

public class PortraitManager : MonoBehaviour
{
    [System.Serializable]
    public struct PortraitSet
    {
        public string character;
        public SpriteSheetObject[] expressions; // 인덱스로 접근 (ExpressionType 순서)
    }

    public PortraitSet[] portraits;

    private Dictionary<(string, PortraitType), SpriteSheetObject> portraitDict;

    void Awake()
    {
        portraitDict = new Dictionary<(string, PortraitType), SpriteSheetObject>();
        foreach (var set in portraits)
        {
            for (int i = 0; i < set.expressions.Length; i++)
            {
                portraitDict[(set.character, (PortraitType)i)] = set.expressions[i];
            }
        }
    }

    public SpriteSheetObject GetPortrait(string name, PortraitType idx)
    {
        if (portraitDict.TryGetValue((name, idx), out var spriteSheet))
            return spriteSheet;
        Debug.Log("접근한 인덱스가 유효하지 않음(인덱스: " + name + ", " + idx + ")");
        return null;
    }
}
