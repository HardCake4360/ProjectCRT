using UnityEngine;

[CreateAssetMenu(fileName = "InvestigationItem", menuName = "CRT/Investigation/Item Definition")]
public class InvestigationItemDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private InvestigationItemCategory category;
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string summary;
    [TextArea(4, 8)]
    [SerializeField] private string detail;
    [SerializeField] private Sprite icon;

    public string Id => id;
    public InvestigationItemCategory Category => category;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public string Summary => summary;
    public string Detail => detail;
    public Sprite Icon => icon;
}
