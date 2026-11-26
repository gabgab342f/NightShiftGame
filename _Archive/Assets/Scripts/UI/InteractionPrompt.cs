using UnityEngine;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    public Text label;
    public CanvasGroup canvasGroup;
    public float fadeSpeed = 8f;
    private bool visible;
    private float alpha;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<Text>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        alpha = 0f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Update()
    {
        float target = visible ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime * fadeSpeed);
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    public void Show(bool show, string message)
    {
        visible = show;
        if (label != null && show)
        {
            label.text = message;
        }
    }
}