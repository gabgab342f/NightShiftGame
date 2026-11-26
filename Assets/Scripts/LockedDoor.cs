using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LockedDoor : Interactable
{
    public string requiredFlag;           // flag needed to open, e.g. elecKey
    public string openFlag;               // flag saved once open, e.g. elecOpen
    public string keyLabel = "Key";       // shown in the prompt, e.g. "Electrical Key"
    [TextArea(2, 4)] public string[] lockedLines;
    [TextArea(2, 4)] public string[] unlockLines;

    public float openSeconds = 0.25f;     // shrink/fade time when it unlocks

    Collider2D col;
    SpriteRenderer sr;
    float baseAlpha = 1f;

    protected virtual void Awake()
    {
        bumpToInteract = true;
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseAlpha = sr.color.a;
    }

    void Start() { RefreshFromFlags(); }

    public override void Interact(PlayerController player)
    {
        if (Flags.Has(requiredFlag)) Open();
        else DialogueManager.I.Show(lockedLines);
    }

    public void Open()
    {
        GameManager.PlaySound("unlock");
        Flags.Set(openFlag);
        // The path opens at once; the visual shrink is just dressing.
        col.enabled = false;
        if (GridMap.I) GridMap.I.Rebuild();
        if (sr != null && sr.enabled) StartCoroutine(OpenAnim());
        if (unlockLines != null && unlockLines.Length > 0) DialogueManager.I.Show(unlockLines);
    }

    IEnumerator OpenAnim()
    {
        Vector3 s0 = transform.localScale;
        Color c0 = sr.color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openSeconds;
            float e = Mathf.Clamp01(t);
            transform.localScale = Vector3.Lerp(s0, new Vector3(0.6f, 0.6f, 1f), e);
            var c = c0; c.a = Mathf.Lerp(c0.a, 0f, e);
            sr.color = c;
            yield return null;
        }
        sr.enabled = false;
    }

    public override void RefreshFromFlags()
    {
        bool open = Flags.Has(openFlag);
        col.enabled = !open;
        if (sr != null)
        {
            sr.enabled = !open;
            var c = sr.color;
            c.a = open ? 0f : baseAlpha;
            sr.color = c;
        }
        transform.localScale = open ? new Vector3(0.6f, 0.6f, 1f) : Vector3.one;
        if (GridMap.I) GridMap.I.Rebuild();
    }
}
