using UnityEngine;

// Esc pause screen. Built in SchoolBuilder. While open it freezes the game via
// Time.timeScale, and GameManager.IsPaused folds it in so the clock, monster and
// player all hold. The keypad keeps its own Esc handling, so pause is ignored
// whenever the keypad owns the screen.
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu I;

    public GameObject panel;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        I = this;
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        // The keypad owns Esc while it is up, and the end screen is not pausable.
        if (KeypadUI.I != null && KeypadUI.I.IsBusy) return;
        if (GameManager.I != null && GameManager.I.HasEnded) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
        if (IsOpen) Resume(); else Open();
    }

    public void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        GameManager.I.Restart();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
