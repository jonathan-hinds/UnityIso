using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsCursorMovementCostView : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(22f, -18f);
    [SerializeField] private Vector2 labelSize = new Vector2(120f, 36f);

    [Header("Theme")]
    [SerializeField] private Color textColor = new Color(1f, 0.94f, 0.94f, 0.82f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private int sortingOrder = 5050;
    [SerializeField, Min(1)] private int fontSize = 24;

    private Canvas rootCanvas;
    private GameObject labelRoot;
    private RectTransform labelRect;
    private Text labelText;
    private Font sharedFont;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void Show(string movementCostText, Vector2 screenPosition)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(movementCostText))
        {
            Hide();
            return;
        }

        labelText.text = movementCostText;
        labelRoot.SetActive(true);

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            Hide();
            return;
        }

        labelRect.anchoredPosition = localPoint + cursorOffset;
    }

    public void Hide()
    {
        if (labelRoot != null)
        {
            labelRoot.SetActive(false);
        }
    }

    private void EnsureBuilt()
    {
        if (labelRoot != null)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        raycaster.enabled = false;

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        labelRoot = CreateUiObject("MovementCostLabel", transform);
        labelRect = labelRoot.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.sizeDelta = labelSize;

        labelText = labelRoot.AddComponent<Text>();
        labelText.font = sharedFont;
        labelText.fontSize = fontSize;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = textColor;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;

        Shadow textShadow = labelRoot.AddComponent<Shadow>();
        textShadow.effectColor = shadowColor;
        textShadow.effectDistance = new Vector2(1f, -1f);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
