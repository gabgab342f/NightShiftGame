using UnityEngine;

public class Fuse : Interactable
{
    public override string GetPrompt()
    {
        return "Press E to Take Fuse";
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.fuseCount++;
            Debug.Log("Fuse collected. Total: " + GameManager.Instance.fuseCount);
        }
        Destroy(gameObject);
    }
}