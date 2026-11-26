using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smooth = 10f;
    public float shake;                 // set by MonsterAI when it is close
    public float lookAhead = 0.6f;      // lead the camera the way the player faces

    void LateUpdate()
    {
        if (!target) return;
        Vector3 goal = new Vector3(target.position.x, target.position.y, -10f);

        // Nudge the frame toward whatever the player is facing so the next cells
        // are already on screen. Guarded because the manager may not exist yet.
        var gm = GameManager.I;
        if (gm != null && gm.player != null)
        {
            Vector2Int f = gm.player.Facing;
            goal += new Vector3(f.x, f.y, 0f) * lookAhead;
        }

        transform.position = Vector3.Lerp(transform.position, goal, smooth * Time.deltaTime);
        if (shake > 0f)
            transform.position += (Vector3)(Random.insideUnitCircle * shake * 0.05f);
    }
}
