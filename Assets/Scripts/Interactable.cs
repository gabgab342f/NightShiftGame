using UnityEngine;

// Base class for anything the player can press E on.
// The object needs a Collider2D on the cell it occupies.
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("If on, walking into this object triggers it (used for doors)")]
    public bool bumpToInteract = false;

    public abstract void Interact(PlayerController player);

    // Called after a checkpoint is restored, so objects can reset themselves.
    public virtual void RefreshFromFlags() { }
}
