using UnityEngine;

public class Generator : Interactable
{
    public int requiredFuses = 3;
    public GameObject[] lightsToActivate;
    public Door targetDoor;

    public override string GetPrompt()
    {
        if (GameManager.Instance == null) return "Press E to Start Generator";
        if (GameManager.Instance.generatorPowered) return "Generator is Running";
        int need = requiredFuses - GameManager.Instance.fuseCount;
        if (need > 0) return "Need " + need + " more Fuse" + (need > 1 ? "s" : "");
        return "Press E to Start Generator";
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.generatorPowered) return;

        if (GameManager.Instance.fuseCount >= requiredFuses)
        {
            GameManager.Instance.generatorPowered = true;
            GameManager.Instance.ghostActive = true;

            if (lightsToActivate != null)
            {
                foreach (GameObject light in lightsToActivate)
                {
                    if (light != null) light.SetActive(true);
                }
            }

            if (targetDoor != null)
            {
                targetDoor.locked = false;
                targetDoor.SetOpen(true, false);
            }

            Debug.Log("Generator powered on. The ghost begins to move...");
        }
        else
        {
            int need = requiredFuses - GameManager.Instance.fuseCount;
            Debug.Log("Generator needs " + need + " more fuse(s).");
        }
    }
}