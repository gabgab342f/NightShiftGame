using UnityEngine;

public class GhostEffects : MonoBehaviour
{
    public EnemyAI enemyAI;
    public AudioSource heartbeatSource;
    public AudioClip heartbeatClip;
    public CameraController cameraController;
    public float shakeFrequency = 2f;
    public float shakeIntensity = 0.15f;
    public float shakeDuration = 0.3f;

    private bool chasing;
    private float shakeCooldown;

    void Start()
    {
        if (enemyAI == null) enemyAI = GetComponent<EnemyAI>();
        if (heartbeatSource == null) heartbeatSource = GetComponent<AudioSource>();
        if (cameraController == null && Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.loop = true;
            heartbeatSource.playOnAwake = false;
            heartbeatSource.clip = heartbeatClip;
            heartbeatSource.Stop();
        }

        if (enemyAI != null)
        {
            enemyAI.OnStateChanged += HandleStateChanged;
        }
    }

    void OnDestroy()
    {
        if (enemyAI != null)
        {
            enemyAI.OnStateChanged -= HandleStateChanged;
        }
    }

    void HandleStateChanged(EnemyAI.State state)
    {
        bool nowChasing = state == EnemyAI.State.Chasing;
        if (nowChasing == chasing) return;
        chasing = nowChasing;

        if (heartbeatSource == null || heartbeatClip == null) return;

        if (chasing)
        {
            heartbeatSource.clip = heartbeatClip;
            heartbeatSource.Play();
        }
        else
        {
            heartbeatSource.Stop();
        }
    }

    void Update()
    {
        if (!chasing) return;

        if (cameraController != null && shakeCooldown <= 0f)
        {
            cameraController.Shake(shakeIntensity, shakeDuration);
            shakeCooldown = 1f / shakeFrequency;
        }
        if (shakeCooldown > 0f)
        {
            shakeCooldown -= Time.deltaTime;
        }
    }
}