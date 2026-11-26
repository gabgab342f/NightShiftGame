using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public GameObject winPanel;
    public GameObject mainMenuPanel;
    public Text locationLabel;
    public GameObject notePanel;
    public Text noteTitleText;
    public Text noteBodyText;

    public bool IsNoteOpen { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged += UpdateLocationLabel;
            UpdateLocationLabel(LocationManager.Instance.CurrentZone);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += UpdateUI;
            UpdateUI(GameManager.Instance.CurrentState);
        }
        else
        {
            Debug.LogWarning("GameManager not found in scene!");
        }

        if (notePanel != null) notePanel.SetActive(false);
    }

    void Update()
    {
        if (IsNoteOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseNote();
        }
    }

    void OnDestroy()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= UpdateLocationLabel;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= UpdateUI;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowNote(string title, string body)
    {
        if (notePanel != null) notePanel.SetActive(true);
        if (noteTitleText != null) noteTitleText.text = title;
        if (noteBodyText != null) noteBodyText.text = body;
        IsNoteOpen = true;
    }

    public void CloseNote()
    {
        if (notePanel != null) notePanel.SetActive(false);
        IsNoteOpen = false;
    }

    private void UpdateLocationLabel(LocationZone zone)
    {
        if (locationLabel != null)
        {
            locationLabel.text = zone != null ? zone.DisplayName : "";
        }
    }

    private void UpdateUI(GameManager.GameState state)
    {
        if (pausePanel != null) pausePanel.SetActive(state == GameManager.GameState.Paused);
        if (gameOverPanel != null) gameOverPanel.SetActive(state == GameManager.GameState.GameOver);
        if (winPanel != null) winPanel.SetActive(state == GameManager.GameState.GameWon);
        
        if (mainMenuPanel != null && state == GameManager.GameState.Playing) 
        {
            mainMenuPanel.SetActive(false);
        }
    }

    public void ResumeButton()
    {
        GameManager.Instance.ResumeGame();
    }

    public void RestartButton()
    {
        GameManager.Instance.RestartGame();
    }

    public void MainMenuButton()
    {
        GameManager.Instance.LoadMainMenu();
    }
    
    public void QuitButton()
    {
        GameManager.Instance.QuitGame();
    }
}