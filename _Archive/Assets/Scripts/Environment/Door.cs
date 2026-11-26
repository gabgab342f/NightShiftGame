using System;
using System.Collections.Generic;
using UnityEngine;

// Horror door: an interactive solid door that slides open/closed with E.
// Can be slammed shut instantly and locked to seal the player in a room.
public class Door : Interactable
{
    public static event Action<Door> OnDoorOpened;

    public bool locked = false;
    public string requiredKeyID = "";
    public float slideSpeed = 6f;

    [Tooltip("Sprite to render as the sliding door panel when no Slat child is present.")]
    public Sprite slatSprite;
    public float doorWidth = 1.5f;

    private static readonly Color DoorColor = new Color(0.42f, 0.33f, 0.23f, 1f);
    private static readonly List<Door> All = new List<Door>();

    private Collider2D col;
    private Transform slat;
    private bool isOpen = false;
    private Vector3 closedScale = Vector3.one;
    private float targetScaleX = 1f;

    void Awake()
    {
        slat = transform.Find("Slat");
        if (slat == null && slatSprite != null)
        {
            slat = CreateSlatVisual();
        }
        if (slat == null) slat = transform;
        closedScale = slat.localScale;
        targetScaleX = closedScale.x;
        col = GetComponent<Collider2D>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(doorWidth, 0.75f);
            col = box;
        }
        UpdateCollider();
        All.Add(this);
    }

    // Builds the sliding panel visual + collider footprint for a door placed
    // on the map directly (no Slat child), mirroring DoorPlacer's Door_Auto.
    Transform CreateSlatVisual()
    {
        var child = new GameObject("Slat");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localScale = new Vector3(doorWidth + 0.1f, 0.85f, 1f);

        var sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = slatSprite;
        sr.color = DoorColor;
        sr.sortingLayerID = MapSortingLayerID();
        sr.sortingOrder = MapSortingOrder() + 3;
        return child.transform;
    }

    int MapSortingLayerID()
    {
        var map = FindMapSpriteRenderer();
        return map != null ? map.sortingLayerID : 0;
    }

    int MapSortingOrder()
    {
        var map = FindMapSpriteRenderer();
        return map != null ? map.sortingOrder : 0;
    }

    static SpriteRenderer FindMapSpriteRenderer()
    {
        var maps = FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].gameObject.name.StartsWith("Map_"))
            {
                return maps[i];
            }
        }
        return null;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    public bool IsOpen
    {
        get { return isOpen; }
    }

    public override void Interact(PlayerController player)
    {
        base.Interact(player);

        if (locked)
        {
            if (!string.IsNullOrEmpty(requiredKeyID))
            {
                if (player != null && player.HasKey(requiredKeyID))
                {
                    locked = false;
                    Debug.Log("Unlocked with " + requiredKeyID + ".");
                }
                else
                {
                    Debug.Log("Locked - requires " + requiredKeyID + ".");
                    return;
                }
            }
            else
            {
                Debug.Log("The door won't budge.");
                return;
            }
        }
        SetOpen(!isOpen, false);
        if (isOpen) OnDoorOpened?.Invoke(this);
    }

    public override string GetPrompt()
    {
        if (locked)
        {
            if (!string.IsNullOrEmpty(requiredKeyID))
            {
                return "Locked - requires " + requiredKeyID;
            }
            return "The door is locked";
        }
        return isOpen ? "Press E to Close" : "Press E to Open";
    }

    public void SetOpen(bool open, bool instant)
    {
        isOpen = open;
        targetScaleX = open ? closedScale.x * 0.05f : closedScale.x;
        if (instant && slat != null)
        {
            Vector3 s = slat.localScale;
            s.x = targetScaleX;
            slat.localScale = s;
        }
        UpdateCollider();
    }

    public void Slam()
    {
        SetOpen(false, true);
    }

    public static void SlamAll()
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != null) All[i].Slam();
        }
    }

    private void UpdateCollider()
    {
        if (col == null) return;
        col.enabled = !isOpen;
        if (locked) col.enabled = true;
    }

    void Update()
    {
        if (slat == null) return;
        Vector3 s = slat.localScale;
        s.x = Mathf.MoveTowards(s.x, targetScaleX, Time.deltaTime * slideSpeed);
        slat.localScale = s;
    }
}