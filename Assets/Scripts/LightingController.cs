using UnityEngine;

// Built-in RP stand-in for URP 2D lights. The world is authored dark and the
// flashlight / glow / monster eye are additive sprites that restore brightness
// locally, so LightingController only has to drive the world's tint.
public class LightingController : MonoBehaviour
{
    public enum Mode { Normal, Blackout, Emergency }
    public static LightingController I;

    public Transform flashlight;
    public Transform glow;
    public Mode mode = Mode.Normal;

    [Header("Darkness")]
    public float normalTint = 0.85f;
    public float blackoutTint = 0.28f;
    public float emergencyTint = 0.35f;

    public float flickerChance = 0.02f;
    public float flickerDim = 0.5f;
    public float flickerBright = 1.2f;

    float currentTint;
    int flickerFrame;
    float flickerHold;

    void Awake() { I = this; }

    void Start() { Set(mode); }

    public void Set(Mode m)
    {
        mode = m;
        currentTint = m == Mode.Normal ? normalTint
                   : m == Mode.Blackout ? blackoutTint
                   : emergencyTint;
        SetLightEnabled(m != Mode.Normal);

        if (WorldTint.I != null) WorldTint.I.ApplyTint(m);
    }

    void SetLightEnabled(bool on)
    {
        if (flashlight) flashlight.gameObject.SetActive(on);
        if (glow) glow.gameObject.SetActive(on);
    }

    void Update()
    {
        // Small random flicker while the power is out: the light dies for a
        // few frames at a time, which is all the atmosphere this needs.
        if (mode == Mode.Blackout && flashlight != null && flashlight.gameObject.activeSelf)
        {
            if (flickerHold > 0f)
            {
                flickerHold -= Time.deltaTime;
                return;
            }
            if (Random.value < flickerChance)
            {
                flickerFrame++;
                flickerHold = 0.06f + Random.value * 0.09f;
                SetConeIntensity(flickerFrame % 2 == 0 ? flickerDim : flickerBright);
            }
            else
            {
                SetConeIntensity(flickerBright);
            }
        }
    }

    void SetConeIntensity(float mul)
    {
        var sr = flashlight != null ? flashlight.GetComponentInChildren<SpriteRenderer>() : null;
        if (sr == null) return;
        var c = sr.color;
        c.a = Mathf.Clamp01(mul);
        sr.color = c;
    }
}
