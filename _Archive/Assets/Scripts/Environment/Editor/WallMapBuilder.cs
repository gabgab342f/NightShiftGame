using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class WallMapBuilder
{
    const string MasksDir = "Assets/Art/WallMaps";
    const string GeneratedDir = MasksDir + "/Generated";

    // World units per tile cell. Cells are 1.0 unit wide so every mapped passage
    // (1-cell minimum in the building) stays wider than the player collider.
    const float CellSize = 1f;

    // Collision bands by average RGB of the sampled map texel:
    // opaque and darker than FurnitureLuminanceThreshold => solid (walls + props).
    const float WallLuminanceThreshold = 90f;
    const float FurnitureLuminanceThreshold = 150f;

    // Seed exporter band: walls + furniture marks live between the near-black
    // background and the brighter walkable floors on the night-blue blueprint.
    const float SeedLow = 25f;
    const float SeedHigh = 150f;

    static readonly Color WallColor = new Color(22f / 255f, 40f / 255f, 60f / 255f, 1f);
    static readonly Color FloorColor = new Color(26f / 255f, 46f / 255f, 71f / 255f, 120f / 255f);

    static readonly string MapsTexturePath = "Assets/Scenes/MapsNightShift.png";

    // Which map slice belongs to which scene, keyed by the imported sprite fileID.
    static readonly Dictionary<long, string> SeedByFileId = new Dictionary<long, string>
    {
        { 1450843498234498249L, "WallMapF1.seed.png" },
        { 1371721429882338283L, "WallMapF2.seed.png" },
        { 7441453763945219301L, "WallMapF3.seed.png" }
    };

    [MenuItem("Tools/Night Shift/Generate Collisions for Current Scene")]
    public static void GenerateCollisions()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Shift", "Stop Play mode first.", "OK");
            return;
        }

        EnsureGeneratedFolder();

        var mapSr = FindMapSpriteRenderer();
        if (mapSr == null || mapSr.sprite == null)
        {
            EditorUtility.DisplayDialog("Night Shift",
                "No Map_?F found in this scene. Open a NightShiftFloor scene first.", "OK");
            return;
        }

        EnsureMaskReadable(mapSr.sprite.texture);
        RefreshSprite(mapSr);

        var wallSprite = EnsureSolidSprite("WallTile.png", WallColor);
        var floorSprite = EnsureSolidSprite("FloorTile.png", FloorColor);
        var propSprite = EnsureSolidSprite("PropTile.png", Color.clear);
        var wallTile = EnsureTile("WallTile.asset", wallSprite, WallColor, Tile.ColliderType.Grid);
        var floorTile = EnsureTile("FloorTile.asset", floorSprite, FloorColor, Tile.ColliderType.None);
        // Props stay fully passable (visibility only): a top-down horror floor plan
        // drawn with furniture can otherwise turn most interior floor into invisible
        // walls, trapping the player between 0.5-unit gaps.
        var propTile = EnsureTile("PropTile.asset", propSprite, Color.clear, Tile.ColliderType.None);

        var grid = EnsureGrid(CellSize);
        var floor = EnsureTilemap(grid.transform, "Floor");
        var walls = EnsureTilemap(grid.transform, "Walls");
        var props = EnsureTilemap(grid.transform, "Collisions");

        var bounds = mapSr.bounds;
        int wallCells, propCells;
        BuildFloor(mapSr, grid, walls, floor, props,
            wallTile, floorTile, propTile, CellSize, out wallCells, out propCells);

        int totalCells = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / CellSize))
                       * Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / CellSize));
        bool allSolid = wallCells >= totalCells;

        int layerId = mapSr.sortingLayerID;
        int order = mapSr.sortingOrder;
        ConfigureRenderer(floor, layerId, order + 1);
        ConfigureRenderer(walls, layerId, order + 2);
        ConfigureRenderer(props, layerId, order - 1);

        EnsureCollider(walls);
        StripCollider(floor);
        StripCollider(props);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string warning = allSolid
            ? "\n\nWARNING: every cell sampled as solid. The map texture is probably " +
              "unreadable (GetPixel could not read pixels). Re-import the map with " +
              "'Read/Write Enabled' checked and rerun."
            : "";
        EditorUtility.DisplayDialog("Night Shift",
            "Collision build done for " + mapSr.gameObject.name +
            "\n\nWalls = " + wallCells + " cells\nFurniture/props = " + propCells +
            " cells (" + CellSize + " unit cells, map " + bounds.size +
            ")." + warning +
            "\n\nSolid colliders were added to the Walls and Collisions tilemaps " +
            "(invisible props collision). Free tilemaps (Doors, Floor) stay passable. " +
            "Save the scene and press Play to test.\n\nTune the FurnitureLuminanceThreshold " +
            "const in WallMapBuilder.cs (now " + FurnitureLuminanceThreshold +
            ") if more or fewer objects should block, then rerun.",
            "OK");
    }

    static void BuildFloor(SpriteRenderer mapSr, Grid grid, Tilemap walls, Tilemap floor,
        Tilemap props, Tile wallTile, Tile floorTile, Tile propTile, float cell,
        out int wallCount, out int propCount)
    {
        walls.ClearAllTiles();
        floor.ClearAllTiles();
        props.ClearAllTiles();

        var sprite = mapSr.sprite;
        var texture = sprite.texture;
        var rect = sprite.textureRect;
        var bounds = mapSr.bounds;

        int cols = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cell));
        int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / cell));
        wallCount = 0;
        propCount = 0;

        for (int cy = 0; cy < rows; cy++)
        {
            if (cy % 64 == 0)
            {
                EditorUtility.DisplayProgressBar("Building collisions", mapSr.gameObject.name,
                    cy / (float)rows);
            }
            for (int cx = 0; cx < cols; cx++)
            {
                var world = bounds.min + new Vector3((cx + 0.5f) * cell, (cy + 0.5f) * cell, 0f);

                var local = mapSr.transform.InverseTransformPoint(world);
                float u = (local.x - sprite.bounds.min.x) / sprite.bounds.size.x;
                float v = (local.y - sprite.bounds.min.y) / sprite.bounds.size.y;
                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);

                int px = Mathf.FloorToInt(rect.x + u * rect.width);
                int py = Mathf.FloorToInt(rect.y + v * rect.height);
                var p = texture.GetPixel(px, py);

                float lum = (p.r + p.g + p.b) / 3f;
                if (p.a >= 200f && lum >= FurnitureLuminanceThreshold)
                {
                    // Bright walkable floor.
                    floor.SetTile(grid.WorldToCell(world), floorTile);
                }
                else if (p.a < 200f || lum < WallLuminanceThreshold)
                {
                    // Solid wall (dark) or transparent edge/border.
                    walls.SetTile(grid.WorldToCell(world), wallTile);
                    wallCount++;
                }
                else
                {
                    // Furniture, tables and props: solid but invisible collision so
                    // the map art stays visible underneath.
                    props.SetTile(grid.WorldToCell(world), propTile);
                    propCount++;
                }
            }
        }

        EditorUtility.ClearProgressBar();
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

    static Grid EnsureGrid(float cell)
    {
        var go = GameObject.Find("Grid");
        if (go == null) go = new GameObject("Grid");
        var grid = go.GetComponent<Grid>();
        if (grid == null) grid = go.AddComponent<Grid>();
        grid.cellSize = new Vector3(cell, cell, 0f);
        return grid;
    }

    static Tilemap EnsureTilemap(Transform parent, string name)
    {
        var child = FindChild(parent, name);
        if (child == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            child = go.transform;
        }
        var tm = child.GetComponent<Tilemap>();
        if (tm == null) tm = child.gameObject.AddComponent<Tilemap>();
        if (child.GetComponent<TilemapRenderer>() == null)
        {
            child.gameObject.AddComponent<TilemapRenderer>();
        }
        child.localPosition = Vector3.zero;
        child.localScale = Vector3.one;
        return tm;
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
        }
        return null;
    }

    static void ConfigureRenderer(Tilemap tm, int sortingLayerId, int sortingOrder)
    {
        var r = tm.GetComponent<TilemapRenderer>();
        if (r == null) return;
        r.sortingLayerID = sortingLayerId;
        r.sortingOrder = sortingOrder;
    }

    static void EnsureCollider(Tilemap walls)
    {
        var go = walls.gameObject;
        if (go.GetComponent<TilemapCollider2D>() == null)
            go.AddComponent<TilemapCollider2D>();
        var comp = go.GetComponent<CompositeCollider2D>();
        if (comp != null) Object.DestroyImmediate(comp);
    }

    static void StripCollider(Tilemap floor)
    {
        var go = floor.gameObject;
        var c = go.GetComponent<TilemapCollider2D>();
        if (c != null) Object.DestroyImmediate(c);
        var comp = go.GetComponent<CompositeCollider2D>();
        if (comp != null) Object.DestroyImmediate(comp);
    }

    
    [MenuItem("Tools/Night Shift/Export Wall Map Seeds")]
    public static void ExportSeeds()
    {
        EnsureGeneratedFolder();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapsTexturePath);
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Night Shift",
                "Missing " + MapsTexturePath + ". Re-import the map first.", "OK");
            return;
        }
        EnsureMaskReadable(texture);

        var sprites = AssetDatabase.LoadAllAssetsAtPath(MapsTexturePath);
        int count = 0;
        foreach (var asset in sprites)
        {
            var sp = asset as Sprite;
            if (sp == null) continue;

            string guid;
            long fileId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sp, out guid, out fileId)) continue;

            string fileName;
            if (!SeedByFileId.TryGetValue(fileId, out fileName)) continue;

            ExportSeed(texture, sp.textureRect, MasksDir + "/" + fileName);
            count++;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Night Shift",
            "Exported " + count + " seed mask(s) to " + MasksDir +
            ".\n\nEach is black where the map is probably a wall. Paint/clear pixels in " +
            "any image editor, then rerun Tools/Night Shift/Generate Collisions for " +
            "Current Scene after pasting fixes into the build source.", "OK");
    }

    static void ExportSeed(Texture2D tex, Rect rect, string outPath)
    {
        int w = (int)rect.width, h = (int)rect.height;
        var dst = new Color32[w * h];
        var src = tex.GetPixels32();
        int texW = tex.width;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = src[((int)rect.y + y) * texW + (int)rect.x + x];
                float lum = (p.r + p.g + p.b) / 3f;
                dst[y * w + x] = (p.a >= 200 && lum >= SeedLow && lum <= SeedHigh)
                    ? new Color32(0, 0, 0, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(dst);
        t.Apply();
        File.WriteAllBytes(outPath, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(outPath);
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

    static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder(MasksDir))
        {
            if (AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets/Art", "WallMaps");
            else
                AssetDatabase.CreateFolder("Assets", "Art");
        }
        if (!AssetDatabase.IsValidFolder(GeneratedDir))
            AssetDatabase.CreateFolder(MasksDir, "Generated");
    }

    static Sprite EnsureSolidSprite(string fileName, Color color)
    {
        string path = GeneratedDir + "/" + fileName;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[]
        {
            color, color,
            color, color
        });
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Tile EnsureTile(string fileName, Sprite sprite, Color color, Tile.ColliderType collider)
    {
        string path = GeneratedDir + "/" + fileName;
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile != null) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = collider;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }
}