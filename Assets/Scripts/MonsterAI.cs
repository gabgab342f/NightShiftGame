using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    public enum State { Chase, Search, Leave }
    public static MonsterAI Active;

    public float chaseStepTime = 0.215f;
    public float finalStepTime = 0.195f;
    public float searchStepTime = 0.3f;
    public float catchDistance = 0.62f;
    public float seeHideDistance = 3.3f;
    public SpriteRenderer body;

    public State state { get; private set; }
    public bool IsHunting => state != State.Leave;
    public float DistanceToPlayer { get; private set; } = 99f;

    PlayerController player;
    CameraFollow cam;
    bool isFinal, seenHiding, moving;
    float chaseTimer, searchTimer, alpha, t, stepTime;
    Vector2Int cell;
    Vector2Int? lastKnown;
    Vector3 from, to;

    void OnEnable() { Locker.OnPlayerHid += HandleHide; }

    void OnDisable()
    {
        Locker.OnPlayerHid -= HandleHide;
        if (Active == this) Active = null;
        if (cam) cam.shake = 0f;
    }

    public void Begin(PlayerController p, Vector2Int startCell, float chaseSeconds, bool final)
    {
        player = p;
        cam = Camera.main.GetComponent<CameraFollow>();
        cell = startCell;
        transform.position = GridMap.CellToWorld(cell);
        chaseTimer = chaseSeconds;
        isFinal = final;
        state = State.Chase;
        alpha = 0f;
        Active = this;
        if (body && WorldTint.I != null) WorldTint.I.Register(body);
    }

    void HandleHide(PlayerController p)
    {
        if (IsHunting && Vector2.Distance(transform.position, p.transform.position) < seeHideDistance)
            seenHiding = true;
    }

    void Update()
    {
        if (player == null || GameManager.I.IsPaused) return;
        float dt = Time.deltaTime;

        if (moving)
        {
            t += dt / stepTime;
            transform.position = Vector3.Lerp(from, to, t);
            if (t >= 1f) { moving = false; cell = GridMap.WorldToCell(to); }
        }

        if (state == State.Leave)
        {
            alpha -= dt * 0.9f;
            SetAlpha();
            if (alpha <= 0f) { GameManager.I.OnMonsterGone(); Destroy(gameObject); }
            return;
        }

        alpha = Mathf.Min(1f, alpha + dt * 2f);
        SetAlpha();
        if (!moving) Think();

        if (state == State.Search)
        {
            searchTimer -= dt;
            if (searchTimer <= 0f)
            {
                if (isFinal) searchTimer = 4f;
                else state = State.Leave;
            }
        }

        if (!isFinal && state == State.Chase)
        {
            chaseTimer -= dt;
            if (chaseTimer <= 0f)
            {
                var field = GridMap.I.DistanceField(player.Cell);
                bool close = field.TryGetValue(cell, out int steps) && steps <= 7;
                if (close) chaseTimer = 3f; else state = State.Leave;
            }
        }

        DistanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
        if (cam) cam.shake = Mathf.Max(0f, 4f - DistanceToPlayer) * 1.5f;

        bool canGrab = !player.IsHidden || seenHiding;
        if (alpha > 0.5f && DistanceToPlayer < catchDistance && canGrab)
            GameManager.I.PlayerCaught();
    }

    void Think()
    {
        Vector2Int target;
        if (!player.IsHidden || seenHiding)
        {
            target = player.Cell;
            lastKnown = target;
            state = State.Chase;
        }
        else
        {
            if (state == State.Chase) { state = State.Search; searchTimer = 5f; }
            if (lastKnown.HasValue && lastKnown.Value != cell) target = lastKnown.Value;
            else { lastKnown = null; Wander(); return; }
        }

        var field = GridMap.I.DistanceField(target);
        Vector2Int best = cell;
        int bestSteps = int.MaxValue;
        foreach (var d in GridMap.Dirs)
        {
            var n = cell + d;
            if (field.TryGetValue(n, out int v) && v < bestSteps) { bestSteps = v; best = n; }
        }
        if (best == cell) return;
        float speed = state == State.Search ? searchStepTime : (isFinal ? finalStepTime : chaseStepTime);
        StepTo(best, speed);
    }

    void Wander()
    {
        var options = new System.Collections.Generic.List<Vector2Int>();
        foreach (var d in GridMap.Dirs)
            if (GridMap.I.IsWalkable(cell + d)) options.Add(cell + d);
        if (options.Count > 0) StepTo(options[Random.Range(0, options.Count)], searchStepTime + 0.08f);
    }

    void StepTo(Vector2Int next, float seconds)
    {
        from = transform.position;
        to = GridMap.CellToWorld(next);
        stepTime = seconds;
        t = 0f;
        moving = true;
        if (body && next.x != cell.x) body.flipX = next.x < cell.x;
    }

    void SetAlpha()
    {
        if (!body) return;
        var c = body.color;
        c.a = alpha;
        body.color = c;
    }
}
