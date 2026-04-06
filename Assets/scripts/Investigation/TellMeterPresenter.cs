using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TellMeterPresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TellPulseGraphic pulseGraphic;
    [SerializeField] private Image panelBackground;
    [SerializeField] private float minimumPulseBpm = 48f;
    [SerializeField] private float maximumPulseBpm = 132f;
    [SerializeField] private float pulseDurationSeconds = 0.26f;
    [SerializeField] private float colorReturnDuration = 0.45f;

    private bool isBuilt;
    private float currentTell;
    private float colorFlashTimer;

    public void EnsureBuilt(float minPulseBpm, float maxPulseBpm, float pulseDuration)
    {
        if (isBuilt)
        {
            return;
        }

        if (pulseGraphic != null)
        {
            minimumPulseBpm = minPulseBpm;
            maximumPulseBpm = Mathf.Max(minPulseBpm, maxPulseBpm);
            pulseDurationSeconds = Mathf.Max(0.08f, pulseDuration);
            pulseGraphic.MinPulseBpm = minimumPulseBpm;
            pulseGraphic.MaxPulseBpm = maximumPulseBpm;
            pulseGraphic.PulseDurationSeconds = pulseDurationSeconds;
            pulseGraphic.ResetSignal(0f);
        }

        ApplyTellVisuals(0f);
        isBuilt = valueText != null && pulseGraphic != null;
    }

    public void SetTell(float tell)
    {
        if (!isBuilt)
        {
            return;
        }

        float clamped = Mathf.Clamp01(tell);
        currentTell = clamped;
        colorFlashTimer = Mathf.Max(0.01f, colorReturnDuration);
        pulseGraphic.SetPendingNoise(false);
        ApplyTellVisuals(clamped);
    }

    public void ResetTell(float tell = 0f)
    {
        if (!isBuilt)
        {
            return;
        }

        float clamped = Mathf.Clamp01(tell);
        currentTell = clamped;
        colorFlashTimer = 0f;
        pulseGraphic.ResetSignal(clamped);
        ApplyTellVisuals(clamped);
    }

    public void SetPendingState(bool pending)
    {
        if (!isBuilt)
        {
            return;
        }

        if (pending)
        {
            if (valueText != null)
            {
                valueText.text = "TELL\n...";
                valueText.color = new Color(0.34f, 0.36f, 0.39f, 1f);
            }

            if (panelBackground != null)
            {
                panelBackground.color = new Color(0.05f, 0.06f, 0.07f, 0.92f);
            }

            if (pulseGraphic != null)
            {
                pulseGraphic.color = new Color(0.22f, 0.24f, 0.26f, 1f);
                pulseGraphic.SetPendingNoise(true);
            }

            return;
        }

        pulseGraphic?.SetPendingNoise(false);
        ApplyTellVisuals(currentTell);
    }

    private void Update()
    {
        if (!isBuilt || colorFlashTimer <= 0f)
        {
            return;
        }

        colorFlashTimer = Mathf.Max(0f, colorFlashTimer - Time.unscaledDeltaTime);
        ApplyTellVisuals(currentTell);
    }

    private void ApplyTellVisuals(float tell)
    {
        Color signalColor = EvaluateTellColor(tell);
        float flashLerp = colorReturnDuration <= 0f
            ? 1f
            : 1f - (colorFlashTimer / colorReturnDuration);
        flashLerp = Mathf.Clamp01(flashLerp);

        Color flashedValueColor = Color.Lerp(Color.white, signalColor, flashLerp);
        Color flashedPanelColor = Color.Lerp(
            new Color(1f, 1f, 1f, 0.92f),
            new Color(signalColor.r * 0.13f, signalColor.g * 0.13f, signalColor.b * 0.13f, 0.88f),
            flashLerp);

        if (valueText != null)
        {
            valueText.text = string.Format("TELL\n{0:0.00}", tell);
            valueText.color = flashedValueColor;
        }

        if (panelBackground != null)
        {
            panelBackground.color = flashedPanelColor;
        }

        if (pulseGraphic != null)
        {
            pulseGraphic.color = flashedValueColor;
            pulseGraphic.SetTellNormalized(tell);
            pulseGraphic.SetVerticesDirty();
        }
    }

    private Color EvaluateTellColor(float tell)
    {
        if (tell <= 0.5f)
        {
            return Color.Lerp(new Color(0.15f, 0.95f, 0.35f), new Color(1f, 0.87f, 0.20f), tell / 0.5f);
        }

        return Color.Lerp(new Color(1f, 0.87f, 0.20f), new Color(0.98f, 0.20f, 0.18f), (tell - 0.5f) / 0.5f);
    }
}
