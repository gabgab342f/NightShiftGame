using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools/Night Shift/Rebuild Floor 1
// Select a new map sprite in the Project window, then run this tool to:
// swap the sprite, rebuild collisions, delete old room zones, create new
// zone stubs, and place doors. Resize zones in the Scene view to match
// your new floor art.
public static class FloorBuilder
{
    const float DefaultRoomW = 10f;
    const float DefaultRoomH = 6f;
    const float DefaultHallH = 8f;
    const float MarginFraction = 0.12f;

    struct ZoneSpec
    {
        public string name;
        public LocationZone.LocationType type;
        public float x, y, w, h;
    }

    [MenuItem("Tools/Night Shift/Rebuild Floor 1")]
    public static void RebuildFloor1()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Shift", "Stop Play mode first.", "OK");
            return;
        }

        var selected = Selection.activeObject;
        var newSprite = selected as Sprite;
        if (newSprite == null && selected is Texture2D)
        {
            string texPath = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(texPath))
            {
                newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
            }
        }
        if (newSprite == null)
        {
            EditorUtility.DisplayDialog("Night Shift",
                "Select a map sprite in the Project window first, then run this tool.\n\n" +
                "The sprite should be your new wall-map blueprint:\n" +
                "dark pixels = wall, bright = floor, mid-gray = furniture.\n\n" +
                "For a multi-sprite atlas, select the specific floor slice sub-asset.", "OK");
            return;
        }

        var mapSr = FindMapSpriteRenderer();
        if (mapSr == null)
        {
            EditorUtility.DisplayDialog("Night Shift",
                "No Map_?F found in this scene. Open NightShiftFloor1 first.", "OK");
            return;
        }

        EnsureMaskReadable(newSprite.texture);
        newSprite = RefreshSprite(newSprite) ?? newSprite;
        mapSr.sprite = newSprite;

        DeleteOldZones();

        WallMapBuilder.GenerateCollisions();

        int zonesCreated = CreateDefaultZones(mapSr.bounds);

        DoorPlacer.PlaceDoors();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Night Shift",
            "Floor 1 rebuilt!\n\n" +
            zonesCreated + " zone stubs created under Locations (1F).\n\n" +
            "Next steps:\n" +
            "1. Select each zone in the Hierarchy and resize its BoxCollider2D to match the room.\n" +
            "   The cyan gizmo shows coverage.\n" +
            "2. Verify FloorSpawn_Start is on walkable floor.\n" +
            "3. Save the scene.", "OK");
    }

    static void DeleteOldZones()
    {
        var locations = GameObject.Find("Locations (1F)");
        if (locations == null) return;
        for (int i = locations.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(locations.transform.GetChild(i).gameObject);
        }
    }

    static int CreateDefaultZones(Bounds bounds)
    {
        var locations = GameObject.Find("Locations (1F)");
        if (locations == null)
        {
            locations = new GameObject("Locations (1F)");
            locations.transform.localPosition = Vector3.zero;
        }

        float minX = bounds.min.x;
        float minY = bounds.min.y;
        float mapW = bounds.size.x;
        float mapH = bounds.size.y;

        float insetX = mapW * MarginFraction;
        float insetY = mapH * MarginFraction;
        float usableMinX = minX + insetX;
        float usableW = mapW - insetX * 2f;
        float usableMinY = minY + insetY;
        float usableH = mapH - insetY * 2f;

        float roomW = Mathf.Min(DefaultRoomW, usableW / 4f * 0.85f);
        float roomH = Mathf.Min(DefaultRoomH, usableH * 0.3f);
        float hallW = usableW * 0.9f;

        float topY = usableMinY + usableH * 0.75f;
        float botY = usableMinY + usableH * 0.25f;
        float hallY = usableMinY + usableH * 0.5f;

        float spacing = usableW / 4f;
        float firstX = usableMinX + spacing / 2f;

        var specs = new List<ZoneSpec>
        {
            MakeSpec("Room1",     LocationZone.LocationType.Room,                firstX,              topY, roomW, roomH),
            MakeSpec("Room2",     LocationZone.LocationType.Room,                firstX + spacing,    topY, roomW, roomH),
            MakeSpec("Faculty",   LocationZone.LocationType.Faculty,             firstX + spacing*2,  topY, roomW, roomH),
            MakeSpec("CompLab",   LocationZone.LocationType.ComputerLaboratory,  firstX + spacing*3,  topY, roomW, roomH),

            MakeSpec("Hallway",   LocationZone.LocationType.Hallway,             bounds.center.x,     hallY, hallW, DefaultHallH),

            MakeSpec("Generator", LocationZone.LocationType.Generator,           firstX,              botY, roomW, roomH),
            MakeSpec("Lobby",     LocationZone.LocationType.Lobby,               firstX + spacing,    botY, roomW, roomH),
            MakeSpec("Room3",     LocationZone.LocationType.Room,                firstX + spacing*2,  botY, roomW, roomH),
            MakeSpec("Room4",     LocationZone.LocationType.Room,                firstX + spacing*3,  botY, roomW, roomH),
        };

        foreach (var s in specs)
        {
            CreateZone(locations.transform, s);
        }

        return specs.Count;
    }

    static ZoneSpec MakeSpec(string name, LocationZone.LocationType type, float x, float y, float w, float h)
    {
        return new ZoneSpec { name = name, type = type, x = x, y = y, w = w, h = h };
    }

    static void CreateZone(Transform parent, ZoneSpec spec)
    {
        var go = new GameObject(spec.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(spec.x, spec.y, 0f);

        var zone = go.AddComponent<LocationZone>();
        zone.locationType = spec.type;
        zone.detailName = spec.name;
        zone.lockOnEnter = spec.type != LocationZone.LocationType.Hallway &&
                           spec.type != LocationZone.LocationType.Lobby;

        var col = go.GetComponent<BoxCollider2D>();
        if (col == null) col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(spec.w, spec.h);
        col.offset = Vector2.zero;
    }

    static SpriteRenderer FindMapSpriteRenderer()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name.StartsWith("Map_"))
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }
        }
        return null;
    }

    static void EnsureMaskReadable(Texture2D tex)
    {
        if (tex == null || tex.isReadable) return;
        string path = AssetDatabase.GetAssetPath(tex);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.isReadable = true;
        imp.SaveAndReimport();
    }

    static Sprite RefreshSprite(Sprite input)
    {
        if (input == null) return null;
        string path = AssetDatabase.GetAssetPath(input);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(s => s != null && s.name == input.name);
    }
}
