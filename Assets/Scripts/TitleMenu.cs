using UnityEngine;
using UnityEngine.UI;

// Title-scene menu controller. Holds the Settings overlay and its master
// volume slider. Button hooks are wired in SchoolBuilder via
// UnityEventTools.AddPersistentListener; the slider listener is added here so
// the dynamic value comes from the control itself.
public class TitleMenu : MonoBehaviour
{
    public GameObject settingsPanel;
    public Slider volumeSlider;

    void Awake()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // AudioListener.volume is a global static, so the choice carries into School.
    public void SetVolume(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
    }
}
