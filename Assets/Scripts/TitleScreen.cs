using UnityEngine;
using UnityEngine.SceneManagement;

// Start-button hook on the title scene. The button's OnClick points here via
// UnityEventTools.AddPersistentListener in SchoolBuilder.
public static class TitleScreen
{
    public static void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("School");
    }

    // Main-menu hooks on the pause and end panels.
    public static void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Title");
    }

    // Exit-button hook on the title scene. Mirrors PauseMenu.QuitGame.
    public static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
