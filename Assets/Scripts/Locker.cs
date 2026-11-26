using System;
using UnityEngine;

public class Locker : Interactable
{
    // The monster listens to this to decide if it saw you hide.
    public static event Action<PlayerController> OnPlayerHid;

    public override void Interact(PlayerController player)
    {
        GameManager.PlaySound("locker");
        player.SetHidden(true);
        OnPlayerHid?.Invoke(player);
    }
}
