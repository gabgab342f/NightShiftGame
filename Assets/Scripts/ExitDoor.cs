using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ExitDoor : Interactable
{
    Collider2D col;
    SpriteRenderer sr;

    void Awake()
    {
        bumpToInteract = true;
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Start() { RefreshFromFlags(); }

    // Open until you walk in; shut from then on.
    public override void RefreshFromFlags()
    {
        bool shut = Flags.Has("entered");
        col.enabled = shut;
        if (sr) sr.enabled = shut;
        if (GridMap.I) GridMap.I.Rebuild();
    }

    public override void Interact(PlayerController player)
    {
        if (!Flags.Has("power"))
            DialogueManager.I.Show(new[] { "The doors are sealed. The light on the lock glows a dead red." });
        else if (!Flags.Has("drive"))
            DialogueManager.I.Show(new[] { "The doors will open now. But not without the drive." });
        else
            GameManager.I.Win();
    }
}
