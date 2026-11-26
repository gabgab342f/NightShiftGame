using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public float maxBattery = 100f;
    public float drainRate = 2f;
    public Transform lightAnchor;
    public SpriteRenderer glowRenderer;
    public PlayerController playerController;

    private bool isOn;
    private float battery;

    public bool IsOn { get { return isOn; } }
    public float Battery { get { return battery; } }

    void Start()
    {
        battery = maxBattery;
        if (glowRenderer == null) glowRenderer = GetComponentInChildren<SpriteRenderer>();
        if (lightAnchor == null) lightAnchor = transform.Find("FlashlightAnchor");
        if (playerController == null) playerController = GetComponent<PlayerController>();
        SetLight(false);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            return;
        }

        if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (battery > 0f) SetLight(!isOn);
        }

        if (isOn)
        {
            battery -= drainRate * Time.deltaTime;
            if (battery <= 0f)
            {
                battery = 0f;
                SetLight(false);
            }
        }

        PositionAnchor();
    }

    void SetLight(bool on)
    {
        isOn = on;
        if (glowRenderer != null) glowRenderer.enabled = on;
    }

    void PositionAnchor()
    {
        if (playerController == null || lightAnchor == null) return;
        Vector2 dir = playerController.facingDirection;
        if (dir == Vector2.zero) return;
        Vector3 offset = (Vector3)(dir.normalized * 1.6f);
        lightAnchor.localPosition = Vector3.Lerp(lightAnchor.localPosition, offset, Time.deltaTime * 10f);
    }
}