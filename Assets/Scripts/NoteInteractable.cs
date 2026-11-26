using UnityEngine;

public class NoteInteractable : Interactable
{
    [TextArea(2, 4)] public string[] lines;
    public string setsFlag;               // optional

    public override void Interact(PlayerController player)
    {
        Flags.Set(setsFlag);
        DialogueManager.I.Show(lines);
    }
}
