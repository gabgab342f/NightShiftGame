using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Paused, GameOver, GameWon }
    public GameState CurrentState { get; private set; }

    public event Action<GameState> OnGameStateChanged;

    public bool ghostActive = false;
    public int fuseCount = 0;
    public bool generatorPowered = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        ChangeState(GameState.Playing);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UIManager.Instance != null && UIManager.Instance.IsNoteOpen)
            {
                return;
            }
            if (CurrentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (CurrentState == GameState.Paused)
            {
                ResumeGame();
            }
        }
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);

        if (newState == GameState.Paused || newState == GameState.GameOver || newState == GameState.GameWon)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    public void PauseGame()
    {
        ChangeState(GameState.Paused);
    }

    public void ResumeGame()
    {
        ChangeState(GameState.Playing);
    }

    public void GameOver()
    {
        ChangeState(GameState.GameOver);
    }

    public void GameWon()
    {
        ChangeState(GameState.GameWon);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        fuseCount = 0;
        generatorPowered = false;
        ghostActive = false;
        ChangeState(GameState.Playing);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); 
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}
