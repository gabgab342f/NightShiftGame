using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridMap : MonoBehaviour
{
    public static GridMap I;

    [Tooltip("Layer used by walls, furniture and closed doors")]
    public LayerMask blockingMask;
    public Vector2Int min = new Vector2Int(0, -41);
    public Vector2Int max = new Vector2Int(51, 0);

    public static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    bool[,] walkable;
    Tilemap wallsMap, furnitureMap;

    void Awake() { I = this; }
    void Start()
    {
        Rebuild();
        StartCoroutine(InitialBake());
    }

    // TilemapCollider2D and CompositeCollider2D shapes are only generated during
    // a physics step, which has not run yet when Start() fires, so the very
    // first bake can see no walls at all and the player walks straight through
    // them. Bake again once the shapes exist.
    IEnumerator InitialBake()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
        Rebuild();
    }

    public static Vector2Int WorldToCell(Vector3 p) =>
        new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

    public static Vector3 CellToWorld(Vector2Int c) =>
        new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);

    // Call this whenever a door opens or closes.
    public void Rebuild()
    {
        // Tilemap shapes can lag a step behind, so ask the composites to
        // solidify what they have right now. GenerateGeometry is a no-op when
        // the member shapes are not ready yet; InitialBake() re-runs the bake
        // once the physics step has created them. The wall/furniture tile data
        // is always readable, so it is OR'ed in as a fallback that makes the
        // static shell block even on the very first bake (batch/offline, or a
        // frame where the composite has not generated yet). Dynamic blockers
        // (doors, lockers) are still pure physics.
        foreach (var cc in FindObjectsOfType<CompositeCollider2D>())
            if (cc != null) cc.GenerateGeometry();
        Physics2D.SyncTransforms();
        if (wallsMap == null) wallsMap = FindTilemap("Walls");
        if (furnitureMap == null) furnitureMap = FindTilemap("Furniture");
        int w = max.x - min.x + 1, h = max.y - min.y + 1;
        walkable = new bool[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = new Vector2Int(x + min.x, y + min.y);
                bool physBlocked = Physics2D.OverlapBox(CellToWorld(c),
                    new Vector2(0.8f, 0.8f), 0f, blockingMask) != null;
                walkable[x, y] = !(physBlocked || HasBlockingTile(c));
            }
    }

    public static Vector3Int ToTilePos(Vector2Int c) =>
        new Vector3Int(c.x, c.y, 0);

    public bool HasBlockingTile(Vector2Int c)
    {
        var p = ToTilePos(c);
        return (wallsMap != null && wallsMap.GetTile(p) != null) ||
               (furnitureMap != null && furnitureMap.GetTile(p) != null);
    }

    public static Tilemap FindTilemap(string name)
    {
        foreach (var tm in FindObjectsOfType<Tilemap>())
            if (tm.name == name) return tm;
        return null;
    }

    public bool IsWalkable(Vector2Int c)
    {
        if (c.x < min.x || c.x > max.x || c.y < min.y || c.y > max.y) return false;
        if (walkable == null) Rebuild();
        return walkable[c.x - min.x, c.y - min.y];
    }

    // Breadth-first search: steps from 'target' to every reachable cell.
    public Dictionary<Vector2Int, int> DistanceField(Vector2Int target)
    {
        var dist = new Dictionary<Vector2Int, int> { [target] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(target);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (dist.ContainsKey(n) || !IsWalkable(n)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }
}
