using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools/Night Shift/Apply Room Connectivity
// Runs DoorPlacer across all three floor scenes (doors + zone wiring) and then
// wires Floor 1's StairsL / StairsR up to NightShiftFloor2.
public static class ConnectivityRunner
{
    const string Floor1 = "Assets/Scenes/NightShiftFloor1.unity";
    const string Floor2 = "Assets/Scenes/NightShiftFloor2.unity";
    const string Floor3 = "Assets/Scenes/NightShiftFloor3.unity";

    [MenuItem("Tools/Night Shift/Apply Room Connectivity")]
    public static void ApplyAll()
    {
        ApplyToFloor(Floor1);
        ApplyToFloor(Floor2);
        ApplyToFloor(Floor3);
        Debug.Log("Connectivity applied to all floors.");
    }

    static void ApplyToFloor(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        DoorPlacer.PlaceDoors();
        if (scenePath == Floor1)
        {
            WireStair("StairsL", Floor2, "Up");
            WireStair("StairsR", Floor2, "Down");
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Saved " + scenePath);
    }

    static void WireStair(string name, string targetScene, string spawn)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            Debug.LogWarning("Missing " + name + " in Floor 1; stair not wired.");
            return;
        }
        var trigger = go.GetComponent<StairTrigger>();
        if (trigger == null) trigger = go.AddComponent<StairTrigger>();
        trigger.targetScene = targetScene;
        trigger.spawnOnArrival = spawn;
    }
}