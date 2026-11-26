using UnityEngine;

public class Note : Interactable
{
    public string noteTitle = "";
    [TextArea(3, 10)]
    public string noteBody = "";

    public override string GetPrompt()
    {
        return "Press E to Read";
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNote(noteTitle, noteBody);
        }
    }
}