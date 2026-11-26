using System;
using TMPro;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager I;

    public GameObject panel;
    public TMP_Text text;
    public float charsPerSecond = 55f;

    public bool IsOpen { get; private set; }
    // True on the frame the box closes, so the same key press
    // does not also trigger the player's interact.
    public bool IsBusy => IsOpen || Time.frameCount == closedFrame;

    string[] lines;
    int index, openedFrame, closedFrame = -1;
    float shown;
    float holdTime;
    Action onDone;

    const float HoldDelay = 0.3f;         // hold this long to fast-forward
    const string Caret = " \u25BC";       // blinking "more text" arrow

    void Awake()
    {
        I = this;
        panel.SetActive(false);
    }

    public void Show(string[] newLines, Action whenDone = null)
    {
        lines = newLines;
        index = 0;
        shown = 0f;
        onDone = whenDone;
        IsOpen = true;
        openedFrame = Time.frameCount;
        text.text = "";
        panel.SetActive(true);
    }

    void Update()
    {
        if (!IsOpen) return;

        string line = lines[index];
        bool complete = shown >= line.Length;
        if (!complete)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            complete = shown >= line.Length;
        }

        if (complete)
        {
            bool blink = (Time.frameCount / 20) % 2 == 0;
            text.text = blink ? line + Caret : line;
        }
        else
        {
            text.text = line.Substring(0, Mathf.Min(line.Length, (int)shown));
        }

        if (Time.frameCount == openedFrame) return;

        // A paused game must not eat clicks meant for the pause buttons.
        if (PauseMenu.I != null && PauseMenu.I.IsOpen) return;

        bool down = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
        bool held = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) ||
                    Input.GetKey(KeyCode.Return) || Input.GetMouseButton(0);

        if (!down)
        {
            // Only a sustained press auto-advances; a tap stays deliberate.
            if (!held) { holdTime = 0f; return; }
            holdTime += Time.unscaledDeltaTime;
            if (holdTime < HoldDelay) return;
        }
        holdTime = 0f;

        if (!complete)
        {
            shown = line.Length;              // first press: finish the line
            text.text = line;
            return;
        }

        index++;                              // next line
        shown = 0f;
        if (index < lines.Length) return;
        Close();
    }

    void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        panel.SetActive(false);
        var callback = onDone;
        onDone = null;
        callback?.Invoke();
    }
}
