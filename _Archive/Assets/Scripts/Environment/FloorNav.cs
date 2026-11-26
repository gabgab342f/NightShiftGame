using UnityEngine;
using UnityEngine.SceneManagement;

public static class FloorNav
{
    public static string PendingSpawn = null;

    public static void GoTo(string sceneName, string spawnMarker)
    {
        PendingSpawn = spawnMarker;
        SceneManager.LoadScene(sceneName);
    }

    public static int CurrentFloor()
    {
        string s = SceneManager.GetActiveScene().name;
        if (s == "NightShiftFloor1") return 1;
        if (s == "NightShiftFloor2") return 2;
        if (s == "NightShiftFloor3") return 3;
        return 0;
    }
}