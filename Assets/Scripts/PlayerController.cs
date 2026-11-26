using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float stepTime = 0.18f;          // seconds per cell
    public float animCycleSeconds = 1.5f;   // length of one walk cycle
    public float stepsPerCycle = 2f;        // one clip covers this many cell steps
    public float idleAnimSpeed = 1f;        // animator rate while standing still
    public SpriteRenderer body;
    public Transform flashlight;            // child additive cone (Step 4)
    public Animator animator;

    float walkAnimSpeed = 1f;

    public Vector2Int Cell { get; private set; }
    public Vector2Int Facing { get; private set; } = Vector2Int.up;
    public bool IsHidden { get; private set; }

    bool moving;
    Vector3 from, to;
    Vector2Int targetCell;
    float t;
    Interactable lastBumped;

    void Start()
    {
        SnapTo(GridMap.WorldToCell(transform.position));
        // A full walk cycle covers stepsPerCycle cells, so one footfall lands
        // per cell. Slower than one cycle per cell, which skipped most frames
        // and read as a judder. Idle keeps its own plain rate.
        walkAnimSpeed = animCycleSeconds / (stepsPerCycle * stepTime);
        if (animator) animator.speed = idleAnimSpeed;
    }

    public void SnapTo(Vector2Int cell)
    {
        moving = false;
        Cell = cell;
        transform.position = GridMap.CellToWorld(cell);
        SetHidden(false);
    }

    void Update()
    {
        if (GameManager.I.IsPaused) return;

        if (moving)
        {
            t += Time.deltaTime / stepTime;
            float p = Mathf.Clamp01(t);
            // Constant velocity across cells: easing each cell to a stop would
            // make a run of steps lurch. The position is snapped exactly onto
            // the cell on arrival below.
            transform.position = Vector3.Lerp(from, to, p);
            if (t < 1f) return;
            moving = false;
            Cell = targetCell;
            transform.position = GridMap.CellToWorld(Cell);
            UpdateAnimator();
            GameManager.I.OnPlayerArrived(Cell);
            if (GameManager.I.IsPaused) return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return))
        {
            TryInteract();
            return;
        }

        if (IsHidden) return;

        Vector2Int dir = ReadDirection();
        if (dir == Vector2Int.zero) { lastBumped = null; UpdateAnimator(); return; }
        if (dir != Facing) lastBumped = null;
        Facing = dir;
        AimFlashlight();

        Vector2Int next = Cell + dir;
        if (GridMap.I.IsWalkable(next))
        {
            from = transform.position;
            to = GridMap.CellToWorld(next);
            targetCell = next;
            t = 0f;
            moving = true;
            lastBumped = null;
            UpdateAnimator();
        }
        else
        {
            var it = InteractableAt(next);
            if (it != null && it.bumpToInteract && it != lastBumped)
            {
                lastBumped = it;
                it.Interact(this);
            }
        }
    }

    Vector2Int ReadDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (h != 0) return new Vector2Int((int)Mathf.Sign(h), 0);
        if (v != 0) return new Vector2Int(0, (int)Mathf.Sign(v));
        return Vector2Int.zero;
    }

    void TryInteract()
    {
        if (IsHidden) { SetHidden(false); return; }
        var it = InteractableAt(Cell + Facing);
        if (it != null) it.Interact(this);
    }

    public static Interactable InteractableAt(Vector2Int cell)
    {
        foreach (var col in Physics2D.OverlapPointAll(GridMap.CellToWorld(cell)))
        {
            var it = col.GetComponentInParent<Interactable>();
            if (it != null) return it;
        }
        return null;
    }

    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        if (body) body.enabled = !hidden;
        if (UIManager.I) UIManager.I.SetHiding(hidden);
    }

    // Every transition keys off Speed plus a direction, so the direction has to
    // be zeroed while standing still. Otherwise the Horizontal-only transitions
    // fire straight out of Idle and fight the Speed ones.
    void UpdateAnimator()
    {
        if (!animator) return;
        if (moving)
        {
            animator.SetFloat("Horizontal", Facing.x);
            animator.SetFloat("Vertical", Facing.y);
            animator.SetFloat("Speed", 1f);
            animator.speed = walkAnimSpeed;
        }
        else
        {
            animator.SetFloat("Horizontal", 0f);
            animator.SetFloat("Vertical", 0f);
            animator.SetFloat("Speed", 0f);
            animator.speed = idleAnimSpeed;
        }
        // Left and right each play their own side clip (RunSide / RunopoSide),
        // so the sprite must never be mirrored.
        if (body) body.flipX = false;
    }

    void AimFlashlight()
    {
        if (!flashlight) return;
        float angle = Facing == Vector2Int.up ? 0 : Facing == Vector2Int.left ? 90 :
                      Facing == Vector2Int.down ? 180 : -90;
        flashlight.localRotation = Quaternion.Euler(0, 0, angle);
        // The cone is ~6 units long with a centred pivot, so shifting it half a
        // length forward puts its narrow base on the player and opens the beam
        // the way the player faces (without this it stays parked above them).
        flashlight.localPosition = new Vector3(Facing.x, Facing.y, 0f) * 3f;
    }
}
