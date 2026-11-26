using UnityEngine;

public class FuseBox : Interactable
{
    public override void Interact(PlayerController player)
    {
        if (Flags.Has("power"))
        {
            DialogueManager.I.Show(new[] { "The panel hums. Red light spills out of every seam." });
            return;
        }
        if (!Flags.Has("fuse"))
        {
            DialogueManager.I.Show(new[] { "The main fuse is black and cracked. You need a spare." });
            return;
        }
        Flags.Set("power");
        LightingController.I.Set(LightingController.Mode.Emergency);
        GameManager.PlaySound("powerup");
        DialogueManager.I.Show(new[] {
            "You twist out the burnt fuse and push in the new one.",
            "CLUNK. The emergency lights stutter on, deep red.",
            Flags.Has("drive")
                ? "Every door in the building starts banging at once. It knows you're leaving. RUN."
                : "Somewhere in the east wing, something screams. You still need your drive."
        }, () => GameManager.I.TriggerEvent("power"));
    }
}
