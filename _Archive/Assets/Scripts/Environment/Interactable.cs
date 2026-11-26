using UnityEngine;

// This is a base class for all interactable objects in the game.
// Attach this (or a script inheriting from it) to an object with a Collider2D.
[RequireComponent(typeof(Collider2D))]
public class Interactable : MonoBehaviour
{
    public virtual void Interact(PlayerController player)
    {
        // This method will be overridden by specific interactable items (e.g. Door, Note).
        Debug.Log("Interacted with " + gameObject.name);
    }

    public virtual string GetPrompt()
    {
        return "Press E to Interact";
    }
}
