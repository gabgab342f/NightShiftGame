using UnityEngine;

// Horror camera: smoothly follows the player, zooms in when inside a room,
// widens in hallways and does a short "slam" punch when a door event fires.
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public Transform target;
    public float followLerp = 6f;
    public float zoomLerp = 5f;
    public float hallwaySize = 9f;
    public float roomSize = 5.5f;
    public float punchSize = 0.45f;
    public float punchDuration = 0.18f;

    private Camera cam;
    private float startZ = -10f;
    private float currentSize;
    private float targetSize;
    private float punch;
    private float shakeIntensity;
    private float shakeTimer;
    private Vector3 shakeSeed;

    void Awake()
    {
        cam = GetComponent<Camera>();
        currentSize = cam.orthographicSize;
        targetSize = currentSize;
        startZ = cam.transform.position.z;
        shakeSeed = new Vector3(Random.value * 100f, Random.value * 100f, 0f);
    }

    void Start()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) target = go.transform;
        }
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged += HandleZoneChanged;
        }
    }

    void OnDestroy()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= HandleZoneChanged;
        }
    }

    void HandleZoneChanged(LocationZone zone)
    {
        bool isRoom = zone != null
            && zone.locationType != LocationZone.LocationType.Hallway
            && zone.locationType != LocationZone.LocationType.Lobby;
        targetSize = isRoom ? roomSize : hallwaySize;
        punch = punchSize;
    }

    public void Shake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }

    void LateUpdate()
    {
        if (punch > 0f)
        {
            punch -= Time.deltaTime * (punchSize / punchDuration);
        }

        float finalSize = targetSize + Mathf.Max(0f, punch);
        currentSize = Mathf.Lerp(currentSize, finalSize, Time.deltaTime * zoomLerp);
        cam.orthographicSize = currentSize;

        if (target != null)
        {
            Vector3 pos = target.position;
            pos.z = startZ;
            transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * followLerp);

            if (shakeTimer > 0f)
            {
                float t = shakeTimer * 10f;
                shakeSeed += new Vector3(0.13f, 0.07f, 0f);
                Vector3 offset = new Vector3(
                    Mathf.PerlinNoise(shakeSeed.x, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, shakeSeed.y) - 0.5f,
                    0f) * shakeIntensity;
                transform.position += offset;
                shakeTimer -= Time.deltaTime;
                if (shakeTimer <= 0f) shakeIntensity = 0f;
            }
        }
    }
}