using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Collects every world renderer once so LightingController can tint the whole
// school dark in a single pass (Built-in RP has no 2D lights to switch off).
// Renderers that appear later register themselves.
public class WorldTint : MonoBehaviour
{
    public static WorldTint I;

    [Tooltip("How dark the world goes before the flashlight is added back in")]
    public float normal = 0.85f;
    public float blackout = 0.28f;
    public float emergency = 0.35f;

    [Tooltip("Tint mixed into the world while the emergency lights are red")]
    public Color emergencyColor = new Color(0.95f, 0.35f, 0.35f);
    public Color blackoutColor = new Color(0.72f, 0.80f, 1f);

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>();

    void Awake() { I = this; }

    void Start()
    {
        Collect();
        ApplyTint(LightingController.I != null ? LightingController.I.mode
                                               : LightingController.Mode.Normal);
    }

    public void Collect()
    {
        renderers.Clear();
        baseColors.Clear();
        foreach (var r in FindObjectsOfType<Renderer>())
        {
            if (r is SpriteRenderer && (r as SpriteRenderer).sortingOrder >= 3) continue;
            if (r.gameObject.layer == 5) continue;      // UI
            Register(r);
        }
    }

    public void Register(Renderer r)
    {
        if (r == null || baseColors.ContainsKey(r)) return;
        renderers.Add(r);
        baseColors[r] = BaseColor(r);
    }

    // Renderer itself has no colour property. Sprites carry one directly;
    // tilemaps keep theirs on the Tilemap component, not the renderer.
    static Color BaseColor(Renderer r)
    {
        if (r is SpriteRenderer sr) return sr.color;
        var map = r.GetComponent<Tilemap>();
        if (map != null) return map.color;
        return Color.white;
    }

    static void Paint(Renderer r, Color c)
    {
        if (r is SpriteRenderer sr) { sr.color = c; return; }
        var map = r.GetComponent<Tilemap>();
        if (map != null) map.color = c;
    }

    public void ApplyTint(LightingController.Mode mode)
    {
        // Normal is not a no-op: it has to put the base colours back when the
        // emergency lights go off again.
        float v = 1f, tr = 1f, tg = 1f, tb = 1f;
        if (mode == LightingController.Mode.Blackout)
        {
            v = blackout;
            tr = blackoutColor.r; tg = blackoutColor.g; tb = blackoutColor.b;
        }
        else if (mode == LightingController.Mode.Emergency)
        {
            v = emergency;
            tr = emergencyColor.r; tg = emergencyColor.g; tb = emergencyColor.b;
        }

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var baseColor = baseColors[r];
            // Keep the live alpha: MonsterAI fades its own body through the
            // renderer's colour, and a cached base alpha would snap it to 0.
            float a = r.gameObject.layer == 5 ? baseColor.a : BaseColor(r).a;
            Paint(r, new Color(baseColor.r * v * tr,
                               baseColor.g * v * tg,
                               baseColor.b * v * tb,
                               a));
        }
    }
}
