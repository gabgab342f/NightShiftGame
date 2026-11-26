using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 1.5f;
    public LayerMask interactableLayer;
    public InteractionPrompt prompt;
    private PlayerController playerController;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        if (prompt == null) prompt = FindOrCreatePrompt();
    }

    InteractionPrompt FindOrCreatePrompt()
    {
        var existing = FindObjectOfType<InteractionPrompt>();
        if (existing != null) return existing;

        var canvasGo = new GameObject("InteractionPromptCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var holder = new GameObject("PromptHolder");
        holder.transform.SetParent(canvasGo.transform, false);
        var canvasGroup = holder.AddComponent<CanvasGroup>();

        var text = holder.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 22;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.12f);
        rect.anchorMax = new Vector2(0.5f, 0.12f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localPosition = Vector3.zero;
        rect.sizeDelta = new Vector2(700f, 40f);

        var prompt = holder.AddComponent<InteractionPrompt>();
        prompt.label = text;
        prompt.canvasGroup = canvasGroup;
        DontDestroyOnLoad(canvasGo);
        return prompt;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            HidePrompt();
            return;
        }

        if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
        {
            HidePrompt();
            return;
        }

        UpdatePrompt();

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
        {
            Interact();
        }
    }

    void UpdatePrompt()
    {
        RaycastHit2D hit = CastInteractionRay();
        if (hit.collider != null)
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                if (prompt != null) prompt.Show(true, interactable.GetPrompt());
                return;
            }
        }
        HidePrompt();
    }

    void HidePrompt()
    {
        if (prompt != null) prompt.Show(false, "");
    }

    void Interact()
    {
        RaycastHit2D hit = CastInteractionRay();
        if (hit.collider != null)
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.Interact(playerController);
            }
        }
    }

    RaycastHit2D CastInteractionRay()
    {
        Vector2 direction = playerController != null ? playerController.facingDirection : Vector2.down;
        if (interactableLayer.value == 0)
        {
            return Physics2D.Raycast(transform.position, direction, interactionDistance);
        }
        return Physics2D.Raycast(transform.position, direction, interactionDistance, interactableLayer);
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && playerController != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, playerController.facingDirection * interactionDistance);
        }
    }
}