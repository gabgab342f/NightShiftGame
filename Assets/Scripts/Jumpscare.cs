using System.Collections;
using UnityEngine;

public class Jumpscare : MonoBehaviour
{
    public SpriteRenderer body;
    public Sprite[] scareFrames;
    public Sprite[] idleFrames;
    public string requiresFlag = "janitorStory";
    public string alsoRequiresFlag = "elecOpen";
    public float damage = 25f;
    public float idleSeconds = 0.25f;
    public float triggerDistance = 1.5f;
    public float rearmDistance = 4f;
    public float cooldownSeconds = 20f;
    public float holdSeconds = 1.1f;
    public float frameSeconds = 0.09f;

    // Path the curse takes after the door opens: hover across the electrical
    // room hoverCycles round-trips, then park beside the fuse box.
    public Vector2 hoverA;
    public Vector2 hoverB;
    public Vector2 settledWorld;
    public int hoverCycles = 2;
    public float hoverSeconds = 4f;

    enum State { Hidden, Hovering, Armed, Scaring, Vanished }
    State state = State.Hidden;

    PlayerController player;
    CameraFollow cam;
    float cooldown;
    int idleFrame;
    float idleTimer;
    bool started;

    void Start()
    {
        if (GameManager.I != null) player = GameManager.I.player;
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow>();
        if (body != null && WorldTint.I != null) WorldTint.I.Register(body);
        ResetForRetry();
    }

    bool Gated() =>
        (string.IsNullOrEmpty(requiresFlag) || Flags.Has(requiresFlag)) &&
        (string.IsNullOrEmpty(alsoRequiresFlag) || Flags.Has(alsoRequiresFlag));

    void Update()
    {
        if (player == null || state == State.Scaring) return;

        if (!Gated())
        {
            state = State.Hidden;
            SetVisible(false);
            return;
        }

        if (!started && state == State.Hidden)
        {
            started = true;
            StartCoroutine(Arrive());
        }

        if (state == State.Armed) TickIdle();

        float d = Vector2.Distance(transform.position, player.transform.position);

        if (state == State.Vanished)
        {
            cooldown -= Time.deltaTime;
            if (cooldown <= 0f && d > rearmDistance)
            {
                transform.position = settledWorld;
                ResetIdle();
                state = State.Armed;
                SetVisible(true);
            }
            return;
        }

        if (state == State.Armed && d <= triggerDistance &&
            !player.IsHidden && !GameManager.I.IsPaused)
            StartCoroutine(Scare());
    }

    void TickIdle()
    {
        if (body == null || idleFrames == null || idleFrames.Length == 0) return;
        idleTimer += Time.deltaTime;
        if (idleTimer < idleSeconds) return;
        idleTimer = 0f;
        idleFrame = (idleFrame + 1) % idleFrames.Length;
        body.sprite = idleFrames[idleFrame];
    }

    void ResetIdle()
    {
        idleFrame = 0;
        idleTimer = 0f;
        if (body != null && idleFrames != null && idleFrames.Length > 0)
            body.sprite = idleFrames[0];
    }

    IEnumerator Arrive()
    {
        state = State.Hovering;
        transform.position = hoverA;
        SetVisible(true);
        ResetIdle();

        float half = hoverSeconds * 0.5f;
        for (int cycle = 0; cycle < hoverCycles; cycle++)
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.MoveTowards(t, 1f, Time.deltaTime / half);
                transform.position = Vector2.Lerp(hoverA, hoverB, t);
                TickIdle();
                yield return null;
            }
            if (cycle == hoverCycles - 1) break;
            while (t > 0f)
            {
                t = Mathf.MoveTowards(t, 0f, Time.deltaTime / half);
                transform.position = Vector2.Lerp(hoverA, hoverB, t);
                TickIdle();
                yield return null;
            }
        }

        while (Vector2.Distance(transform.position, settledWorld) > 0.02f)
        {
            transform.position = Vector2.MoveTowards(transform.position, settledWorld,
                Time.deltaTime * Mathf.Max(2f, hoverSeconds));
            TickIdle();
            yield return null;
        }

        transform.position = settledWorld;
        ResetIdle();
        state = State.Armed;
    }

    IEnumerator Scare()
    {
        state = State.Scaring;
        if (GameManager.I != null) GameManager.I.BeginScare();
        GameManager.PlaySound("scream");
        if (cam != null) cam.shake = 7f;
        if (UIManager.I != null) UIManager.I.ShowScare();

        ShowFrame(0);
        float elapsed = 0f;
        int i = 1;
        while (elapsed < holdSeconds)
        {
            yield return new WaitForSecondsRealtime(frameSeconds);
            elapsed += frameSeconds;
            ShowFrame(i++);
        }

        if (UIManager.I != null) UIManager.I.HideScare();
        if (cam != null) cam.shake = 0f;
        if (GameManager.I != null) GameManager.I.EndScare();

        if (GameManager.I != null) GameManager.I.TakeDamage(damage);
        state = State.Vanished;
        cooldown = cooldownSeconds;
        SetVisible(false);
    }

    void ShowFrame(int index)
    {
        if (scareFrames == null || scareFrames.Length == 0) return;
        if (UIManager.I == null || UIManager.I.scareImage == null) return;
        UIManager.I.scareImage.sprite = scareFrames[index % scareFrames.Length];
    }

    void SetVisible(bool on)
    {
        if (body != null) body.enabled = on;
    }

    public void ResetForRetry()
    {
        StopAllCoroutines();
        started = Gated();
        state = Gated() ? State.Armed : State.Hidden;
        cooldown = 0f;
        if (cam != null) cam.shake = 0f;
        if (Gated())
        {
            transform.position = settledWorld;
            SetVisible(true);
            ResetIdle();
        }
        else
        {
            SetVisible(false);
        }
    }
}