using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager I;

    public TMP_Text clockText, objectiveText, toastText;
    public TMP_Text promptText, keysText, healthText;
    public Image redVignette;
    public GameObject hidingOverlay;
    public GameObject scareOverlay;
    public Image scareImage;
    public GameObject endPanel;
    public TMP_Text endTitle, endBody;
    public Button retryButton, continueButton, menuButton;

    float toastTimer;

    // Prompt/objective presentation state.
    static readonly Color PromptColor = new Color(0.95f, 0.92f, 0.70f);
    static readonly Color LockedColor = new Color(0.95f, 0.45f, 0.40f);
    static readonly Color ObjectiveColor = new Color(0.72f, 0.90f, 0.72f);
    float promptAlpha;
    float objectivePulse;
    string lastObjective;

    void Awake()
    {
        I = this;
        hidingOverlay.SetActive(false);
        if (scareOverlay != null) scareOverlay.SetActive(false);
        endPanel.SetActive(false);
        toastText.text = "";
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        clockText.text = gm.ClockText();
        clockText.color = gm.TimeLeft < 120f ? new Color(1f, 0.35f, 0.35f) : Color.white;

        // Highlight the objective for a moment whenever it changes, so the next
        // step is never missed.
        string objective = gm.Objective();
        if (objective != lastObjective)
        {
            lastObjective = objective;
            objectivePulse = 0.7f;
        }
        if (objectivePulse > 0f) objectivePulse = Mathf.Max(0f, objectivePulse - Time.deltaTime);
        objectiveText.text = objective;
        objectiveText.color = objectivePulse > 0f
            ? Color.Lerp(ObjectiveColor, Color.white, objectivePulse / 0.7f)
            : ObjectiveColor;

        // Contextual interact prompt. Mirrors PlayerController.TryInteract, which
        // acts on the cell the player faces, and is suppressed while a dialogue,
        // the keypad, or the locker-hiding overlay owns the screen.
        if (promptText != null)
        {
            bool canPrompt = !gm.IsPaused && gm.player != null && !gm.player.IsHidden;
            var facing = canPrompt
                ? PlayerController.InteractableAt(gm.player.Cell + gm.player.Facing)
                : null;
            string prompt = PromptFor(facing);
            promptText.text = prompt;

            // Fade the prompt in and out, and tint locked doors differently.
            float targetAlpha = string.IsNullOrEmpty(prompt) ? 0f : 1f;
            promptAlpha = Mathf.MoveTowards(promptAlpha, targetAlpha, Time.deltaTime * 8f);
            Color pc = prompt.StartsWith("Locked") ? LockedColor : PromptColor;
            pc.a = promptAlpha;
            promptText.color = pc;
        }

        if (keysText != null) keysText.text = KeysLine();

        if (healthText != null && gm.Health > 0f)
        {
            healthText.text = $"HP {Mathf.CeilToInt(gm.Health)} / {Mathf.CeilToInt(gm.maxHealth)}";
            healthText.color = gm.Health <= gm.maxHealth * 0.25f
                ? new Color(1f, 0.35f, 0.35f)
                : Color.white;
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f) toastText.text = "";
        }

        float danger = 0f;
        var m = MonsterAI.Active;
        if (m != null && m.IsHunting) danger = Mathf.Clamp01((9f - m.DistanceToPlayer) / 12f);
        if (damageFlash > 0f) damageFlash -= Time.deltaTime;
        float target = Mathf.Max(danger, damageFlash > 0f ? 0.6f : 0f);
        // A slow pulse once the health bar is nearly empty, so low health is
        // felt even when nothing is nearby.
        if (gm.Health > 0f && gm.Health <= gm.maxHealth * 0.25f)
            target = Mathf.Max(target, 0.18f + 0.12f * Mathf.Sin(Time.time * 5f));
        var c = redVignette.color;
        c.a = Mathf.MoveTowards(c.a, target, Time.deltaTime * 2f);
        redVignette.color = c;
    }

    public void Toast(string message, float seconds = 3f)
    {
        toastText.text = message;
        toastTimer = seconds;
    }

    float damageFlash;

    public void FlashDamage()
    {
        damageFlash = 0.4f;
        if (GameManager.I != null && GameManager.I.Health <= 0f) return;
        Toast("The curse takes a cut of you.", 2f);
    }

    public void SetHiding(bool on) => hidingOverlay.SetActive(on);

    public void ShowScare() { if (scareOverlay != null) scareOverlay.SetActive(true); }
    public void HideScare() { if (scareOverlay != null) scareOverlay.SetActive(false); }

    // Key-aware prompt for whatever the player is facing. KeypadDoor derives
    // from LockedDoor, so it has to be matched first.
    string PromptFor(Interactable it)
    {
        if (it == null) return "";
        if (it is KeypadDoor) return "[E] Enter code";
        if (it is Janitor) return "[E] Talk";
        if (it is LockedDoor door)
        {
            string key = string.IsNullOrEmpty(door.keyLabel) ? "key" : door.keyLabel;
            return Flags.Has(door.requiredFlag)
                ? "[E] Use " + key
                : "Locked - " + key + " required";
        }
        return "[E] Interact";
    }

    // Collected-key readout for the HUD, built straight from the flag store.
    string KeysLine()
    {
        string s = "";
        if (Flags.Has("labKey")) s += "Lab Key    ";
        if (Flags.Has("elecKey")) s += "Elec Key    ";
        if (Flags.Has("fuse")) s += "Fuse    ";
        if (Flags.Has("drive")) s += "Flash Drive";
        return s.Length == 0 ? "" : "KEYS:  " + s.TrimEnd();
    }

    public void ShowEnd(string title, string body, bool canRetry, bool canContinue = false)
    {
        endTitle.text = title;
        endBody.text = body;
        retryButton.gameObject.SetActive(canRetry);
        if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
        endPanel.SetActive(true);
    }

    public void HideEnd() => endPanel.SetActive(false);

    // Hooks for the end-panel buttons.
    public void OnRetry() => GameManager.I.RetryFromCheckpoint();
    public void OnRestart() => GameManager.I.Restart();
    public void OnContinue() => GameManager.I.NextLevel();
    public void OnMainMenu() => TitleScreen.MainMenu();
}
