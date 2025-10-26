using UnityEngine;

public class SegmentationUI : MonoBehaviour
{
    [Header("Reference to the volume renderer (the object with VolumeDVR)")]
    public VolumeDVR volumeDVR;

    // =========================
    // 1. TOGGLES (Visibility)
    // =========================
    // These are meant to be hooked directly to Toggle.onValueChanged(bool)
    // You hardcode the label index INSIDE each method.

    // Example mapping:
    // 1 = Brain
    // 2 = Skull
    // 3 = Tumor
    // 4 = Vessels
    // Change indices to match YOUR labels.

    public void ToggleBrain(bool visible)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelVisible(1, visible);
    }

    public void ToggleSkull(bool visible)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelVisible(2, visible);
    }

    public void ToggleTumor(bool visible)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelVisible(3, visible);
    }

    public void ToggleVessels(bool visible)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelVisible(4, visible);
    }

    // =========================
    // 2. OPACITY SLIDERS
    // =========================
    // These are meant for Slider.onValueChanged(float) in [0..1]

    public void TumorOpacity(float alpha01)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelOpacity(3, alpha01); // tumor label index
    }

    public void SkullOpacity(float alpha01)
    {
        if (volumeDVR == null) return;
        volumeDVR.SetLabelOpacity(2, alpha01); // skull label index
    }

    // Add more if you need them.

    // =========================
    // 3. SOLO + SHOW ALL BUTTONS
    // =========================
    // These match what Unity showed you in the dropdown already.

    public void SoloTumor()
    {
        if (volumeDVR == null) return;
        volumeDVR.SoloLabel(3); // tumor label index
    }

    public void SoloSkull()
    {
        if (volumeDVR == null) return;
        volumeDVR.SoloLabel(2); // skull label index
    }

    public void ShowAll()
    {
        if (volumeDVR == null) return;
        volumeDVR.ShowAll();
    }
}
