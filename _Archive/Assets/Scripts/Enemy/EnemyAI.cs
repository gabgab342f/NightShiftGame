using System;
using UnityEngine;
using UnityEngine.AI;

// Ghost AI: three states (Patrol / Search / Chase).
// The ghost stays frozen until the generator is powered (GameManager.ghostActive).
// Search is triggered by noise (running nearby, flashlight, opened doors).
// Chase is triggered by line-of-sight only; being caught is Game Over.
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrolling, Searching, Chasing }

    public Transform player;
    public Transform[] patrolPoints;

    public float patrolSpeed = 2f;
    public float searchSpeed = 3f;
    public float chaseSpeed = 5.5f;

    public float searchRadius = 15f;
    public float sightRadius = 8f;
    public float sightAngle = 45f;
    public float searchDuration = 6f;
    public float losePlayerTime = 3f;
    public LayerMask wallLayer;

    public SpriteRenderer ghostSprite;
    public Animator ghostAnimator;

    public event Action<State> OnStateChanged;

    private NavMeshAgent agent;
    private int currentPatrolIndex = 0;
    private State currentState;
    private Vector3 searchTarget;
    private Vector2 facingDir = Vector2.down;
    private float searchTimer;
    private float searchIdleTimer;
    private float loseSightTimer;
    private PlayerController playerController;
    private Flashlight playerFlashlight;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = patrolSpeed;
        agent.isStopped = true;

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerFlashlight = player.GetComponent<Flashlight>();
        }

        currentState = State.Patrolling;
        GoToNextPatrolPoint();
    }

    void OnEnable()
    {
        Door.OnDoorOpened += HandleDoorOpened;
    }

    void OnDisable()
    {
        Door.OnDoorOpened -= HandleDoorOpened;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            agent.isStopped = true;
            return;
        }

        bool active = GameManager.Instance == null || GameManager.Instance.ghostActive;
        if (!active)
        {
            agent.isStopped = true;
            return;
        }

        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            if (player == null) return;
        }

        switch (currentState)
        {
            case State.Patrolling: UpdatePatrol(); break;
            case State.Searching: UpdateSearch(); break;
            case State.Chasing: UpdateChase(); break;
        }

        UpdateFacing();
    }

    void UpdatePatrol()
    {
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        if (CheckForNoiseSignals())
        {
            EnterSearch();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextPatrolPoint();
        }
    }

    void UpdateSearch()
    {
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            EnterPatrol();
            return;
        }

        if (searchIdleTimer > 0f)
        {
            searchIdleTimer -= Time.deltaTime;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            if (TryGetRandomWanderPoint(out Vector3 point))
            {
                agent.SetDestination(point);
            }
            else
            {
                searchIdleTimer = 0.5f;
                if (agent.remainingDistance < 0.2f)
                {
                    agent.ResetPath();
                }
            }
        }
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);

        if (CanSeePlayer())
        {
            loseSightTimer = 0f;
        }
        else
        {
            loseSightTimer += Time.deltaTime;
            if (loseSightTimer >= losePlayerTime)
            {
                searchTarget = player.position;
                EnterSearch();
            }
        }
    }

    bool CheckForNoiseSignals()
    {
        if (player == null) return false;

        if (playerController != null && playerController.IsRunning)
        {
            if (Vector2.Distance(transform.position, player.position) <= searchRadius)
            {
                searchTarget = player.position;
                return true;
            }
        }

        if (playerFlashlight != null && playerFlashlight.IsOn)
        {
            if (Vector2.Distance(transform.position, player.position) <= searchRadius * 0.7f)
            {
                searchTarget = player.position;
                return true;
            }
        }

        return false;
    }

    void HandleDoorOpened(Door door)
    {
        if (door == null) return;
        if (currentState != State.Patrolling) return;
        if (Vector2.Distance(transform.position, door.transform.position) <= searchRadius)
        {
            searchTarget = door.transform.position;
            EnterSearch();
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > sightRadius) return false;

        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        if (toPlayer.sqrMagnitude < 0.0001f) return true;

        if (Vector2.Angle(facingDir, toPlayer) > sightAngle) return false;

        if (wallLayer.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, dist, wallLayer);
            if (hit.collider != null) return false;
        }

        return true;
    }

    void UpdateFacing()
    {
        Vector2 vel = agent.velocity;
        if (vel.sqrMagnitude > 0.1f)
        {
            facingDir = vel.normalized;
            if (ghostSprite != null)
            {
                ghostSprite.flipX = facingDir.x > 0f;
            }
            if (ghostAnimator != null)
            {
                ghostAnimator.SetFloat("Speed", vel.magnitude);
            }
        }
        else if (ghostAnimator != null)
        {
            ghostAnimator.SetFloat("Speed", 0f);
        }
    }

    void EnterPatrol()
    {
        ChangeState(State.Patrolling);
        agent.speed = patrolSpeed;
        ResetSearchTimers();
        GoToNextPatrolPoint();
    }

    void EnterSearch()
    {
        ChangeState(State.Searching);
        agent.speed = searchSpeed;
        ResetSearchTimers();
        agent.SetDestination(searchTarget);
    }

    void EnterChase()
    {
        ChangeState(State.Chasing);
        agent.speed = chaseSpeed;
        loseSightTimer = 0f;
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    void ChangeState(State newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }

    void ResetSearchTimers()
    {
        searchTimer = searchDuration;
        searchIdleTimer = 0f;
        loseSightTimer = 0f;
    }

    bool TryGetRandomWanderPoint(out Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 3f;
            Vector3 candidate = searchTarget + (Vector3)offset;
            if (wallLayer.value == 0 ||
                Physics2D.OverlapPoint(candidate, wallLayer.value) == null)
            {
                point = candidate;
                return true;
            }
        }
        point = transform.position;
        return false;
    }

    void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameOver();
            }
            else
            {
                Debug.Log("The cursed student caught the player! Game Over.");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}