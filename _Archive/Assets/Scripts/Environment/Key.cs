using UnityEngine;

public class Key : Interactable
{
    public string keyID = "GoldKey";

    public override string GetPrompt()
    {
        return "Press E to Take " + keyID;
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        if (player != null)
        {
            player.AddKey(keyID);
            Debug.Log("Picked up key: " + keyID);
            Destroy(gameObject);
        }
    }
}
