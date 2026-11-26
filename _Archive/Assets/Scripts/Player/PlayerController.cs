using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float maxStamina = 100f;
    public float staminaDrain = 15f;
    public float staminaRegen = 8f;
    public float staminaResumeThreshold = 10f;
    public LayerMask solidLayer;
    private Rigidbody2D rb;
    private Vector2 movement;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private List<string> inventoryKeys = new List<string>();
    private bool isRunning;
    private float stamina;
    private Collider2D myCol;

    public Vector2 facingDirection = Vector2.down;
    public bool IsRunning { get { return isRunning; } }
    public float Stamina { get { return stamina; } }
    public bool IsBuggySpawn
    {
        get
        {
            if (rb == null) return false;
            int mask = solidLayer.value != 0 ? solidLayer.value : Physics2D.AllLayers;
            Collider2D hit = Physics2D.OverlapPoint(transform.position, mask);
            return hit != null && !hit.isTrigger && hit != myCol;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        myCol = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        stamina = maxStamina;
        SnapToSpawn();
    }

    void SnapToSpawn()
    {
        string spawn = FloorNav.PendingSpawn;
        FloorNav.PendingSpawn = null;
        if (string.IsNullOrEmpty(spawn)) spawn = "Start";
        var marker = GameObject.Find("FloorSpawn_" + spawn);
        if (marker != null)
        {
            transform.position = marker.transform.position;
            UnstickFromOverlap();
        }
    }

    // The front-gate fix: if the spawn cell is inside a wall, door slat or other
    // static collider (e.g. an auto-placed gate door overlapping the marker),
    // the character would be pinned in place. Move to the nearest free point.
    void UnstickFromOverlap()
    {
        if (rb == null) return;
        int mask = solidLayer.value != 0 ? solidLayer.value : Physics2D.AllLayers;
        if (IsFree(transform.position, mask)) return;

        float step = 0.5f;
        var start = transform.position;
        for (int ring = 1; ring <= 3; ring++)
        {
            for (float a = 0f; a < 360f; a += 30f)
            {
                Vector3 p = start + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * (ring * step);
                if (IsFree(p, mask))
                {
                    transform.position = p;
                    return;
                }
            }
        }
    }

    bool IsFree(Vector3 point, int mask)
    {
        Collider2D hit = Physics2D.OverlapPoint(point, mask);
        return hit == null || hit.isTrigger || hit == myCol;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            movement = Vector2.zero;
            UpdateAnimation();
            return;
        }

        if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
        {
            movement = Vector2.zero;
            isRunning = false;
            UpdateAnimation();
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        if (movement != Vector2.zero)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                facingDirection = new Vector2(Mathf.Sign(movement.x), 0f);
            }
            else
            {
                facingDirection = new Vector2(0f, Mathf.Sign(movement.y));
            }
        }

        bool wantRun = Input.GetKey(KeyCode.LeftShift) && movement != Vector2.zero;
        if (wantRun && stamina > 0f)
        {
            isRunning = true;
            stamina -= staminaDrain * Time.deltaTime;
            if (stamina < 0f) stamina = 0f;
        }
        else
        {
            isRunning = false;
            if (stamina < maxStamina)
            {
                stamina += staminaRegen * Time.deltaTime;
                if (stamina > maxStamina) stamina = maxStamina;
            }
        }

        UpdateAnimation();
    }
    
    void UpdateAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("Horizontal", facingDirection.x);
            animator.SetFloat("Vertical", facingDirection.y);
            animator.SetFloat("Speed", movement.sqrMagnitude);

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = movement != Vector2.zero && facingDirection.x > 0f;
            }
        }
    }

    void FixedUpdate()
    {
        float speed = isRunning ? runSpeed : moveSpeed;
        rb.MovePosition(rb.position + movement.normalized * speed * Time.fixedDeltaTime);
    }
    
    public void AddKey(string keyID)
    {
        if (!inventoryKeys.Contains(keyID))
        {
            inventoryKeys.Add(keyID);
        }
    }

    public bool HasKey(string keyID)
    {
        return inventoryKeys.Contains(keyID);
    }
}
