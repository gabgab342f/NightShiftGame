using UnityEngine;

public class Cabinet : Interactable
{
    public Interactable containsItem;
    public Transform dropPoint;
    private bool isOpen;
    private bool itemGiven;

    public override string GetPrompt()
    {
        return isOpen ? "Press E to Close" : "Press E to Open";
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        isOpen = !isOpen;

        if (isOpen && !itemGiven && containsItem != null)
        {
            Vector3 pos = dropPoint != null
                ? dropPoint.position
                : transform.position + Vector3.right * 0.8f;
            Interactable spawned = Instantiate(containsItem, pos, Quaternion.identity);
            spawned.name = containsItem.name;
            itemGiven = true;
        }
    }
}