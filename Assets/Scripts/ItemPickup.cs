using UnityEngine;

public class ItemPickup : Interactable
{
    public string flag;                   // e.g. labKey
    [TextArea(2, 4)] public string[] pickupLines;
    [TextArea(2, 4)] public string[] emptyLines = { "There's nothing else here." };
    public string storyEvent;             // optional, sent to GameManager after the text
    public GameObject sparkle;            // optional glint shown until taken

    void Start() { RefreshFromFlags(); }

    public override void Interact(PlayerController player)
    {
        if (Flags.Has(flag)) { DialogueManager.I.Show(emptyLines); return; }
        Flags.Set(flag);
        GameManager.PlaySound("pickup");
        RefreshFromFlags();
        DialogueManager.I.Show(pickupLines, () =>
        {
            if (!string.IsNullOrEmpty(storyEvent)) GameManager.I.TriggerEvent(storyEvent);
        });
    }

    public override void RefreshFromFlags()
    {
        if (sparkle) sparkle.SetActive(!Flags.Has(flag));
    }
}
