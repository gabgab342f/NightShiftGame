using UnityEngine;

// A sensor door: it opens on its own when the player is in or beside the cell
// it occupies, and closes again once they walk away. Like every door it sits on
// the Blocking layer, so toggling its collider must re-bake GridMap walkability.
// It is not an Interactable, so it never needs an E prompt.
[RequireComponent(typeof(Collider2D))]
public class AutoDoor : MonoBehaviour
{
    public Vector2Int cell;               // the cell this door blocks
    public float slideSeconds = 0.22f;    // how fast the leaf fades/shrinks

    Collider2D col;
    SpriteRenderer sr;
    bool open;
    float openAmount;                     // 0 = shut, 1 = fully open
    float baseAlpha = 1f;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseAlpha = sr.color.a;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm != null && gm.player != null)
        {
            Vector2Int p = gm.player.Cell;
            bool near = !gm.IsPaused &&
                        Mathf.Abs(p.x - cell.x) <= 1 &&
                        Mathf.Abs(p.y - cell.y) <= 1;
            if (near != open)
            {
                open = near;
                col.enabled = !open;             // path toggles at once
                if (GridMap.I) GridMap.I.Rebuild();
            }
        }

        // The visual leaf catches up smoothly; timeScale 0 (pause) freezes it.
        float target = open ? 1f : 0f;
        openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / slideSeconds);
        if (sr == null) return;
        var c = sr.color;
        c.a = baseAlpha * (1f - openAmount);
        sr.color = c;
        sr.enabled = openAmount < 0.999f;
    }
}
