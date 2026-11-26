using UnityEngine;

public class Exit : Interactable
{
    public string requiredKeyID = "ExitCard";

    public override string GetPrompt()
    {
        return "Press E to Use Exit Card";
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        if (player != null && !string.IsNullOrEmpty(requiredKeyID) && !player.HasKey(requiredKeyID))
        {
            Debug.Log("Locked - requires " + requiredKeyID + ".");
            return;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameWon();
        }
    }
}