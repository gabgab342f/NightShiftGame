using UnityEngine;

public class StairTrigger : MonoBehaviour
{
    public string targetScene = "";
    public string spawnOnArrival = "";

    void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(targetScene)) return;
        if (other.CompareTag("Player"))
        {
            FloorNav.GoTo(targetScene, spawnOnArrival);
        }
    }
}