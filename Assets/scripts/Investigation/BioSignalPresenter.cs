using TMPro;
using UnityEngine;

public class BioSignalPresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text debugSignalText;

    public void SetDebugSignalText(TMP_Text target)
    {
        debugSignalText = target;
    }

    public void Present(BioSignalPayload signal)
    {
        if (debugSignalText == null)
        {
            return;
        }

        signal ??= BioSignalPayload.Default();
        debugSignalText.text =
            $"Stress  {signal.stress:0.00}\n" +
            $"Distort {signal.distortion:0.00}\n" +
            $"Focus   {signal.focus:0.00}";
    }
}
