using System;
using TMPro;
using UnityEngine;

public class KeypadUI : MonoBehaviour
{
    public static KeypadUI I;

    public GameObject panel;
    public TMP_InputField input;
    public TMP_Text status;

    public bool IsBusy => panel.activeSelf || Time.frameCount == closedFrame;

    string code;
    Action onSuccess;
    int closedFrame = -1;

    void Awake() { I = this; panel.SetActive(false); }

    public void Open(string correctCode, Action success)
    {
        code = correctCode;
        onSuccess = success;
        input.text = "";
        status.text = "ENTER CODE";
        panel.SetActive(true);
        input.ActivateInputField();
    }

    public void Submit()                  // also hook this to the OK button
    {
        if (input.text == code)
        {
            Close();
            onSuccess?.Invoke();
        }
        else
        {
            status.text = "WRONG CODE";
            input.text = "";
            input.ActivateInputField();
        }
    }

    public void Close()                   // also hook this to the Cancel button
    {
        panel.SetActive(false);
        closedFrame = Time.frameCount;
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit();
        else if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }
}
