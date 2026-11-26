using UnityEngine;

public class KeypadDoor : LockedDoor
{
    public string code = "0417";

    public override void Interact(PlayerController player)
    {
        if (Flags.Has(openFlag)) return;
        KeypadUI.I.Open(code, Open);
    }
}
