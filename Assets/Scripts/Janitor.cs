using System.Collections;
using UnityEngine;

public class Janitor : Interactable
{
    public SpriteRenderer body;
    public Sprite[] idleFrames;
    public Sprite[] cleanFrames;
    public Sprite[] dissolveFrames;
    public Sprite[] mopFrames;
    public string[] story;

    public float idleSeconds = 0.30f;
    public float cleanSeconds = 0.12f;
    public int cleanLoops = 3;
    public float dissolveSeconds = 0.16f;
    public float mopSeconds = 0.22f;

    Coroutine sequence;
    bool gone;

    void Start()
    {
        RefreshFromFlags();
        if (!gone) sequence = StartCoroutine(IdleLoop());
    }

    public override void Interact(PlayerController player)
    {
        if (gone) return;
        DialogueManager.I.Show(story, () =>
        {
            Flags.Set("janitorStory");
            StopSequence();
            sequence = StartCoroutine(Vanish());
        });
    }

    public override void RefreshFromFlags()
    {
        StopSequence();
        gone = Flags.Has("janitorStory");
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = !gone;
        if (body == null) return;
        body.enabled = true;
        if (gone && mopFrames != null && mopFrames.Length > 0)
            body.sprite = mopFrames[mopFrames.Length - 1];
    }

    void StopSequence()
    {
        if (sequence != null)
        {
            StopCoroutine(sequence);
            sequence = null;
        }
    }

    IEnumerator IdleLoop()
    {
        while (true) yield return PlayOnce(idleFrames, idleSeconds);
    }

    IEnumerator Vanish()
    {
        for (int loop = 0; loop < cleanLoops; loop++)
            yield return PlayOnce(cleanFrames, cleanSeconds);
        yield return PlayOnce(dissolveFrames, dissolveSeconds);
        yield return PlayOnce(mopFrames, mopSeconds);
        if (body != null && mopFrames != null && mopFrames.Length > 0)
            body.sprite = mopFrames[mopFrames.Length - 1];
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        sequence = null;
        UIManager.I.Toast("The janitor is gone. He pointed toward the electrical room.", 5f);
    }

    IEnumerator PlayOnce(Sprite[] frames, float seconds)
    {
        if (body == null || frames == null || frames.Length == 0) yield break;
        for (int i = 0; i < frames.Length; i++)
        {
            body.sprite = frames[i];
            yield return new WaitForSeconds(seconds);
        }
    }
}