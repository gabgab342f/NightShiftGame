using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public class LocationZone : MonoBehaviour
{
    public enum LocationType { Hallway, Room, Generator, Faculty, Lobby, ComputerLaboratory }

    public LocationType locationType = LocationType.Hallway;
    public string detailName = "";

    public Door[] doors;
    public bool lockOnEnter = true;

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(detailName) ? locationType.ToString() : detailName; }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (LocationManager.Instance != null)
            {
                LocationManager.Instance.EnterZone(this);
            }

            if (lockOnEnter && locationType != LocationType.Hallway && locationType != LocationType.Lobby)
            {
                Door.SlamAll();
                if (doors != null)
                {
                    foreach (Door door in doors)
                    {
                        if (door == null) continue;
                        door.Slam();
                        door.locked = true;
                    }
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (LocationManager.Instance != null)
            {
                LocationManager.Instance.ExitZone(this);
            }

            if (doors != null)
            {
                foreach (Door door in doors)
                {
                    if (door != null) door.locked = false;
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(col.offset, col.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(col.offset, col.size);
        Gizmos.matrix = Matrix4x4.identity;

        Handles.Label(transform.position + Vector3.up * 1f, DisplayName);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
#endif
}