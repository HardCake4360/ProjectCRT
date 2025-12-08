using UnityEngine;

[CreateAssetMenu(fileName = "InterogationDataSet", menuName = "Scriptable Objects/InterogationDataSet")]
public class InterogationDataSet : ScriptableObject
{
    [SerializeField] InterogationDiaObject tesData;
    [SerializeField] ChoicesObj clues;
    [SerializeField] InterogationDiaObject paradoxData;

    public void SetInterogationProperties()
    {
        InterogationManager.Instance.button_tes.OnClick.AddListener(() =>
        {
            tesData.DetonateEvent();
        });
        InterogationManager.Instance.CUI.SetChoices(clues);
        
        InterogationManager.Instance.button_paradox.OnClick.AddListener(() =>
        {
            paradoxData.DetonateEvent();
        });
    }

}
