using UnityEngine;

[CreateAssetMenu(fileName = "TipObj", menuName = "Scriptable Objects/TipObj")]
public class TipObj : ScriptableObject
{
    public string TipName;
    [System.Serializable]
    public class TipLine
    {
        public Sprite TipImage;
        public string TipText;
    }
    public TipLine[] lines;
}
