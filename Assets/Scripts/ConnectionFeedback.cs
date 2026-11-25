using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Mensajes de conexión que aparecen sobre la pantalla existente, sin reemplazarla.</summary>
public static class ConnectionFeedback
{
    private const string ObjectName = "Connection Feedback";

    public static void Hide(MonoBehaviour owner)
    {
        if (owner == null) return;
        for (int i = owner.transform.childCount - 1; i >= 0; i--)
        {
            Transform previous = owner.transform.GetChild(i);
            if (previous.name != ObjectName) continue;
            previous.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(previous.gameObject);
        }
    }

    public static void Show(MonoBehaviour owner, string message, Action retry = null)
    {
        if (owner == null) return;
        Hide(owner);
        GameObject canvasObject = new GameObject(ObjectName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(owner.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = .5f;
        GameObject panel = new GameObject("Message", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>(); panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, 0f);
        panelRect.pivot = new Vector2(.5f, 0f); panelRect.anchoredPosition = new Vector2(0f, 22f); panelRect.sizeDelta = new Vector2(1000f, retry != null ? 145f : 90f);
        panel.GetComponent<Image>().color = new Color(.055f, .075f, .11f, .96f);
        MakeText(panel.transform, message, new Vector2(0f, retry != null ? 22f : 0f), new Vector2(950f, 75f), 23);
        if (retry == null) return;
        GameObject buttonObject = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button)); buttonObject.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>(); buttonRect.anchoredPosition = new Vector2(0f, -44f); buttonRect.sizeDelta = new Vector2(260f, 42f);
        buttonObject.GetComponent<Image>().color = new Color(.14f, .36f, .48f);
        Button button = buttonObject.GetComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>(); button.onClick.AddListener(() => retry());
        MakeText(buttonObject.transform, "REINTENTAR", Vector2.zero, buttonRect.sizeDelta, 20);
    }

    private static void MakeText(Transform parent, string message, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject host = new GameObject("Text", typeof(RectTransform), typeof(Text)); host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        Text text = host.GetComponent<Text>(); text.text = message; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
    }
}
