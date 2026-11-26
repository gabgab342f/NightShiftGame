using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Tools/Night Shift/Auto-Place Doors
// Scans the same wall classifier as WallMapBuilder, finds every doorway gap
// (1..3 cells of open floor framed by wall on both sides), spawns a visible
// sliding Door there, wires each room zone to its own doors, and attaches the
// horror CameraController to the Main Camera. Run once per floor scene and Save.
public static class DoorPlacer
{
    const float DoorHeight = 0.75f;

    static readonly string SlatSpritePath = "Assets/Art/WallMaps/Generated/DoorSlat.png";

    static readonly Color DoorColor = new Color(0.42f, 0.33f, 0.23f, 1f);

    [MenuItem("Tools/Night Shift/Auto-Place Doors")]
    public static void PlaceDoors()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Shift", "Stop Play mode first.", "OK");
            return;
        }

        var mapSr = FindMapSpriteRenderer();
        if (mapSr == null || mapSr.sprite == null)
        {
            EditorUtility.DisplayDialog("Night Shift",
                "No Map_?F found in this scene. Open a NightShiftFloor scene first.", "OK");
            return;
        }

        EnsureMaskReadable(mapSr.sprite.texture);
        RefreshSprite(mapSr);
        EnsureSlatSprite();

        var bounds = mapSr.bounds;
        var start = GameObject.Find("FloorSpawn_Start");
        var carved = new HashSet<long>();
        var gridGo = GameObject.Find("Grid");
        var grid = gridGo != null ? gridGo.GetComponent<UnityEngine.Grid>() : null;
        var walls = gridGo != null ? gridGo.transform.Find("Walls")?.GetComponent<Tilemap>() : null;
        float cell = (grid != null && grid.cellSize.x > 0f) ? grid.cellSize.x : 1f;
        var solid = BuildSolidGrid(walls, grid, bounds, cell, out int cols, out int rows);

        var zones = Object.FindObjectsOfType<LocationZone>();
        var hall = FindHallZone(zones);
        Bounds hallBox = hall != null ? ZoneWorldBounds(hall) : new Bounds(Vector3.zero, Vector3.zero);
        bool hasHall = hall != null;

        var entrance = start != null
            ? CarveFrontGate(bounds, walls, grid, solid, cols, rows, cell, start.transform.position,
                carved, hallBox, hasHall)
            : (Vector3?)null;

        DeletePreviousDoors();

        var placed = new List<Door>();
        int wired = 0;

        foreach (var zone in zones)
        {
            var box = ZoneWorldBounds(zone);
            ClearZoneInterior(box, bounds, walls, grid, solid, cols, rows, cell, carved);
            if (zone.locationType == LocationZone.LocationType.Hallway ||
                zone.locationType == LocationZone.LocationType.Lobby || hall == null)
            {
                continue;
            }

            Vector3? doorPos = CarveDoorwayToHall(box, hallBox, bounds, walls, grid,
                solid, cols, rows, cell, carved);
            if (!doorPos.HasValue) continue;

            var p = doorPos.Value;
            var door = CreateDoor("Door_" + zone.gameObject.name, p.x, p.y, cell * 3f,
                mapSr.sortingLayerID, mapSr.sortingOrder);
            int il = ResolveInteractableLayer();
            if (il >= 0) door.gameObject.layer = il;
            placed.Add(door);
            zone.doors = new[] { door };
            wired++;
        }

        if (entrance.HasValue)
        {
            var p = entrance.Value;
            bool near = placed.Exists(d => (d.transform.position - p).sqrMagnitude < 1.5f);
            if (!near)
            {
                var door = CreateDoor("Door_Entrance", p.x, p.y, cell * 3f,
                    mapSr.sortingLayerID, mapSr.sortingOrder);
                int gIL = ResolveInteractableLayer();
                if (gIL >= 0) door.gameObject.layer = gIL;
                placed.Add(door);
            }
        }

        // Stairwells on the upper floors are trigger objects, not LocationZones:
        // carve their rooms (and a corridor onto the hall) so the stair triggers
        // are actually reachable. Floor 1 already handles StairsL/StairsR as Room zones.
        foreach (var col in Object.FindObjectsOfType<BoxCollider2D>())
        {
            if (col == null || !col.gameObject.name.StartsWith("Stairs")) continue;
            if (col.GetComponent<LocationZone>() != null) continue;
            var sBox = ColliderWorldBounds(col);
            ClearZoneInterior(sBox, bounds, walls, grid, solid, cols, rows, cell, carved);
            if (hasHall)
            {
                CarveStairCorridor(sBox, hallBox, bounds, walls, grid, solid, cols, rows, cell, carved);
            }
        }

        var cam = Object.FindObjectOfType<Camera>();
        bool camAdded = false;
        if (cam != null && cam.GetComponent<CameraController>() == null)
        {
            cam.gameObject.AddComponent<CameraController>();
            camAdded = true;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Night Shift",
            "Placed " + placed.Count + " doors across " + cols + "x" + rows + " cells.\n\n" +
            "Zones wired to their own doors: " + wired +
            (camAdded ? "\n\nCameraController attached to Main Camera." : "") +
            "\n\nSave the scene, then press Play to test. Reposition any Door_* " +
            "objects by hand; rerun this tool after moving zones to final spots.",
            "OK");
    }

    // Solid cells now come from the hand-painted Walls tilemap (the ground truth
    // of the settled floor plan) instead of reading the map sprite, which is a
    // dark blueprint the luminance classifier cannot distinguish.
    static bool[,] BuildSolidGrid(Tilemap walls, Grid grid, Bounds bounds, float cell,
        out int cols, out int rows)
    {
        cols = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cell));
        rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / cell));
        var solid = new bool[cols, rows];

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                var world = bounds.min + new Vector3((cx + 0.5f) * cell, (cy + 0.5f) * cell, 0f);
                var tileCell = grid != null ? grid.WorldToCell(world) : new Vector3Int();
                solid[cx, cy] = walls != null && walls.GetTile(tileCell) != null;
            }
        }
        return solid;
    }

    static LocationZone FindHallZone(LocationZone[] zones)
    {
        LocationZone best = null;
        float area = -1f;
        foreach (var zone in zones)
        {
            if (zone == null || zone.locationType != LocationZone.LocationType.Hallway) continue;
            var b = ZoneWorldBounds(zone);
            float a = b.size.x * b.size.y;
            if (a > area) { area = a; best = zone; }
        }
        return best;
    }

    static Bounds ZoneWorldBounds(LocationZone zone)
    {
        var col = zone.GetComponent<BoxCollider2D>();
        return col != null ? ColliderWorldBounds(col) : new Bounds(zone.transform.position, Vector3.one);
    }

    static Bounds ColliderWorldBounds(BoxCollider2D col)
    {
        var center = col.transform.TransformPoint(col.offset);
        var ext = Vector2.Scale(new Vector2(col.size.x, col.size.y), col.transform.lossyScale) * 0.5f;
        return new Bounds(center, ext * 2f);
    }

    // Opens every settled zone interior so the player can walk the floor plan
    // even while the Walls tilemap is a solid fill. Uses XY-only containment:
    // the map sprite bounds and the zone boxes live at different Z, so a
    // Bounds.Contains on 3D points silently rejects every cell.
    static void ClearZoneInterior(Bounds box, Bounds bounds, Tilemap walls, Grid grid,
        bool[,] solid, int cols, int rows, float cell, HashSet<long> carved)
    {
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                var world = bounds.min + new Vector3((cx + 0.5f) * cell, (cy + 0.5f) * cell, 0f);
                bool inside = world.x >= box.min.x && world.x <= box.max.x
                              && world.y >= box.min.y && world.y <= box.max.y;
                if (!inside) continue;
                ClearCell(cx, cy, bounds, walls, grid, solid, carved, cell);
            }
        }
    }

    // Cuts a 3-cell-wide passable doorway between a room/stair zone and the
    // hallway where they meet, and answers where that doorway's door stands.
    static Vector3? CarveDoorwayToHall(Bounds box, Bounds hall, Bounds bounds,
        Tilemap walls, Grid grid, bool[,] solid, int cols, int rows, float cell,
        HashSet<long> carved)
    {
        float ox = Mathf.Min(box.max.x, hall.max.x) - Mathf.Max(box.min.x, hall.min.x);
        float oy = Mathf.Min(box.max.y, hall.max.y) - Mathf.Max(box.min.y, hall.min.y);
        if (ox < 2f && oy < 2f) return null;

        float doorX, doorY;
        if (ox >= 2f)
        {
            doorX = Mathf.Clamp(box.center.x,
                Mathf.Max(box.min.x, hall.min.x) + 0.5f,
                Mathf.Min(box.max.x, hall.max.x) - 0.5f);
            int rowA = Mathf.FloorToInt((Mathf.Max(box.min.y, hall.min.y) - bounds.min.y) / cell);
            int rowB = Mathf.FloorToInt((Mathf.Min(box.max.y, hall.max.y) - bounds.min.y) / cell);
            int r0 = Mathf.Max(0, Mathf.Min(rowA, rowB));
            int r1 = Mathf.Min(rows - 1, Mathf.Max(rowA, rowB));
            if (r0 > r1) return null;

            int col = Mathf.Clamp(Mathf.FloorToInt((doorX - bounds.min.x) / cell), 1, cols - 2);
            doorY = bounds.min.y + (r0 + r1) * 0.5f * cell;
            for (int cc = col - 1; cc <= col + 1; cc++)
            {
                if (cc < 1 || cc > cols - 2) continue;
                for (int cy = r0; cy <= r1; cy++)
                {
                    ClearCell(cc, cy, bounds, walls, grid, solid, carved, cell);
                }
            }
        }
        else
        {
            doorY = Mathf.Clamp(box.center.y,
                Mathf.Max(box.min.y, hall.min.y) + 0.5f,
                Mathf.Min(box.max.y, hall.max.y) - 0.5f);
            int colA = Mathf.FloorToInt((Mathf.Max(box.min.x, hall.min.x) - bounds.min.x) / cell);
            int colB = Mathf.FloorToInt((Mathf.Min(box.max.x, hall.max.x) - bounds.min.x) / cell);
            int c0 = Mathf.Max(1, Mathf.Min(colA, colB));
            int c1 = Mathf.Min(cols - 2, Mathf.Max(colA, colB));
            if (c0 > c1) return null;

            int row = Mathf.Clamp(Mathf.FloorToInt((doorY - bounds.min.y) / cell), 1, rows - 2);
            doorX = bounds.min.x + (c0 + c1) * 0.5f * cell;
            for (int rr = row - 1; rr <= row + 1; rr++)
            {
                if (rr < 1 || rr > rows - 2) continue;
                for (int cx = c0; cx <= c1; cx++)
                {
                    ClearCell(cx, rr, bounds, walls, grid, solid, carved, cell);
                }
            }
        }
        return new Vector3(doorX, doorY, 0f);
    }

    // Carves a 3-cell-wide passable gap through the front wall band at the spawn
    // column, then reports the world position of the first floor cell above the
    // gap (where the entrance door should sit). With a settled hallway the gap
    // stops at the hall floor; every cleared cell is recorded in "carved".
    static Vector3 CarveFrontGate(Bounds bounds, Tilemap walls, Grid grid,
        bool[,] solid, int cols, int rows, float cell, Vector3 spawn, HashSet<long> carved,
        Bounds hall, bool hasHall)
    {
        int cx = Mathf.Clamp(Mathf.FloorToInt((spawn.x - bounds.min.x) / cell), 1, cols - 2);
        int spawnRow = Mathf.Clamp(Mathf.FloorToInt((spawn.y - bounds.min.y) / cell), 0, rows - 1);
        int firstFreeRow = rows;

        // With a settled hallway, the gate stops at the hall floor instead of
        // chewing through the whole solid-fill wall band above it.
        int cyStop = rows;
        if (hasHall)
        {
            cyStop = Mathf.Clamp(Mathf.FloorToInt((hall.min.y - bounds.min.y) / cell), 0, rows - 1);
        }

        for (int dx = -1; dx <= 1; dx++)
        {
            int col = cx + dx;
            int cy = 0;
            if (hasHall)
            {
                for (; cy <= cyStop; cy++)
                {
                    ClearCell(col, cy, bounds, walls, grid, solid, carved, cell);
                }
            }
            else
            {
                while (cy <= spawnRow)
                {
                    ClearCell(col, cy, bounds, walls, grid, solid, carved, cell);
                    cy++;
                }
                while (cy < rows && solid[col, cy])
                {
                    ClearCell(col, cy, bounds, walls, grid, solid, carved, cell);
                    cy++;
                }
            }
            if (cy < firstFreeRow) firstFreeRow = cy;
        }

        if (walls != null)
        {
            var collider = walls.GetComponent<TilemapCollider2D>();
            if (collider != null)
            {
                collider.enabled = false;
                collider.enabled = true;
            }
        }

        float wx = bounds.min.x + (cx + 0.5f) * cell;
        float wy = hasHall
            ? hall.min.y - cell * 0.5f
            : bounds.min.y + (firstFreeRow - 0.25f) * cell;
        return new Vector3(wx, wy, 0f);
    }

    static void ClearCell(int col, int cy, Bounds bounds, Tilemap walls, Grid grid,
        bool[,] solid, HashSet<long> carved, float cell)
    {
        solid[col, cy] = false;
        carved.Add(col * 100000 + cy);
        if (walls != null && grid != null)
        {
            var world = bounds.min + new Vector3((col + 0.5f) * cell, (cy + 0.5f) * cell, 0f);
            walls.SetTile(grid.WorldToCell(world), null);
        }
    }

    // Cuts a horizontal corridor from the hall edge to a stair trigger volume at the
    // map boundary — needed because stair triggers sit just beyond the hall in X and
    // have no Y overlap with the hall.
    static void CarveStairCorridor(Bounds stairBox, Bounds hallBox, Bounds bounds,
        Tilemap walls, Grid grid, bool[,] solid, int cols, int rows, float cell,
        HashSet<long> carved)
    {
        float corridorH = cell * 3f;
        float y = Mathf.Clamp(stairBox.center.y - corridorH * 0.5f,
            Mathf.Max(stairBox.min.y, hallBox.min.y),
            Mathf.Min(stairBox.max.y, hallBox.max.y) - corridorH);
        if (y < stairBox.min.y) y = stairBox.center.y - corridorH * 0.5f;
        float y1 = y + corridorH;

        float x0, x1;
        if (stairBox.center.x < hallBox.center.x)
        {
            x0 = Mathf.Max(stairBox.min.x, bounds.min.x);
            x1 = Mathf.Min(hallBox.min.x, bounds.max.x);
        }
        else
        {
            x0 = Mathf.Max(hallBox.max.x, bounds.min.x);
            x1 = Mathf.Min(stairBox.max.x, bounds.max.x);
        }
        if (x1 <= x0) return;

        int cx0 = Mathf.Clamp(Mathf.FloorToInt((x0 - bounds.min.x) / cell), 0, cols - 1);
        int cx1 = Mathf.Clamp(Mathf.CeilToInt((x1 - bounds.min.x) / cell), 0, cols - 1);
        int cy0 = Mathf.Clamp(Mathf.FloorToInt((y - bounds.min.y) / cell), 0, rows - 1);
        int cy1 = Mathf.Clamp(Mathf.CeilToInt((y1 - bounds.min.y) / cell), 0, rows - 1);
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                ClearCell(cx, cy, bounds, walls, grid, solid, carved, cell);
    }

    static Door CreateDoor(string name, float x, float y, float width, int sortingLayerId, int sortingOrder)
    {
        var root = new GameObject(name);
        root.transform.position = new Vector3(x, y, 0f);

        var col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width, DoorHeight);
        col.enabled = true;

        var slat = new GameObject("Slat");
        slat.transform.SetParent(root.transform, false);
        slat.transform.localPosition = Vector3.zero;

        var sr = slat.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlatSpritePath);
        sr.color = DoorColor;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder + 3;
        slat.transform.localScale = new Vector3(width + 0.1f, DoorHeight + 0.1f, 1f);

        var door = root.AddComponent<Door>();
        return door;
    }

    static int ResolveInteractableLayer()
    {
        var pi = Object.FindObjectOfType<PlayerInteraction>();
        if (pi != null && pi.interactableLayer.value != 0)
        {
            // first set bit of the mask = the concrete layer to stamp on
            uint v = (uint)pi.interactableLayer.value;
            for (int b = 0; b < 32; b++)
            {
                if ((v & (1u << b)) != 0) return b;
            }
        }
        return 0; // no mask configured -> E works over all layers anyway
    }

    static void DeletePreviousDoors()
    {
        var doors = Object.FindObjectsOfType<Door>();
        foreach (var d in doors)
        {
            if (d != null) Object.DestroyImmediate(d.gameObject);
        }
        var all = Object.FindObjectsOfType<Transform>();
        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i].name.StartsWith("Door_") && all[i].parent == null)
            {
                Object.DestroyImmediate(all[i].gameObject);
            }
        }
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
        if (tex.isReadable) return;
        string path = AssetDatabase.GetAssetPath(tex);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.isReadable = true;
        imp.SaveAndReimport();
    }

    // After SaveAndReimport the old Texture2D handle is destroyed; re-fetch the
    // sprite so .texture points at the freshly imported, readable asset.
    static void RefreshSprite(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return;
        string path = AssetDatabase.GetAssetPath(sr.sprite);
        var fresh = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .FirstOrDefault(s => s != null && s.name == sr.sprite.name);
        if (fresh != null) sr.sprite = fresh;
    }

    static void EnsureSlatSprite()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(SlatSpritePath) != null) return;

        if (!AssetDatabase.IsValidFolder("Assets/Art"))
        {
            AssetDatabase.CreateFolder("Assets", "Art");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Art/WallMaps"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "WallMaps");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Art/WallMaps/Generated"))
        {
            AssetDatabase.CreateFolder("Assets/Art/WallMaps", "Generated");
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var white = Color.white;
        tex.SetPixels(new[] { white, white, white, white });
        tex.Apply();
        System.IO.File.WriteAllBytes(SlatSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SlatSpritePath);
        var imp = AssetImporter.GetAtPath(SlatSpritePath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 2f;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
    }
}