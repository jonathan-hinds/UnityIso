using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum TacticsSelectionPanelRole
{
    ActiveCharacter = 0,
    SelectedCharacter = 1
}

[DisallowMultipleComponent]
public sealed class TacticsSelectionPanelView : MonoBehaviour
{
    public const string DefaultPrefabResourcePath = "UI/TacticsSelectionPanelView";

    [Header("Panel")]
    [SerializeField] private TacticsSelectionPanelRole panelRole = TacticsSelectionPanelRole.ActiveCharacter;
    [SerializeField] private string panelTitle = "ACTIVE";
    [SerializeField] private Vector2 anchorMin = new Vector2(0f, 0f);
    [SerializeField] private Vector2 anchorMax = new Vector2(0f, 0f);
    [SerializeField] private Vector2 pivot = new Vector2(0f, 0f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(28f, 28f);
    [SerializeField] private Vector2 panelSize = new Vector2(432f, 206f);

    [Header("Theme")]
    [SerializeField] private int sortingOrder = 5005;
    [SerializeField] private Color panelColor = new Color(0.04f, 0.07f, 0.12f, 0.42f);
    [SerializeField] private Color panelInnerColor = new Color(0.07f, 0.11f, 0.18f, 0.34f);
    [SerializeField] private Color frameColor = new Color(0.95f, 0.79f, 0.48f, 0.34f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.16f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.95f, 0.91f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.73f, 0.80f, 0.90f, 0.92f);
    [SerializeField] private Color mutedTextColor = new Color(0.50f, 0.59f, 0.71f, 0.92f);
    [SerializeField] private Color fallbackAccentColor = new Color(0.93f, 0.78f, 0.48f, 1f);
    [SerializeField] private Color ownerPillColor = new Color(0.33f, 0.48f, 0.70f, 0.14f);
    [SerializeField] private Color trackColor = new Color(0.06f, 0.10f, 0.16f, 0.52f);
    [SerializeField] private Color trackBorderColor = new Color(0.72f, 0.80f, 0.91f, 0.10f);
    [SerializeField] private Color indicatorColor = new Color(0.08f, 0.12f, 0.19f, 0.36f);
    [SerializeField] private Color pipOffColor = new Color(0.28f, 0.34f, 0.42f, 0.55f);

    [Header("Bindings")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image glowImage;
    [SerializeField] private Image innerPanelImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Image ownerPillImage;
    [SerializeField] private Text ownerText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text nameText;

    private Font sharedFont;
    private ResourceRowWidgets healthBar;
    private ResourceRowWidgets manaBar;
    private ResourceRowWidgets staminaBar;
    private ResourceRowWidgets experienceBar;
    private CounterWidgets actionCounter;
    private CounterWidgets movementCounter;

    public TacticsSelectionPanelRole PanelRole => panelRole;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void Configure(
        TacticsSelectionPanelRole role,
        string title,
        Vector2 minAnchor,
        Vector2 maxAnchor,
        Vector2 panelPivot,
        Vector2 position,
        Vector2 size)
    {
        panelRole = role;
        panelTitle = title;
        anchorMin = minAnchor;
        anchorMax = maxAnchor;
        pivot = panelPivot;
        anchoredPosition = position;
        panelSize = size;

        if (panelRoot != null)
        {
            ApplyLayout();
            ApplyHeader();
        }
    }

    public void Show(ITacticsSelectionHudTarget selectionTarget)
    {
        if (selectionTarget == null)
        {
            Hide();
            return;
        }

        Show(selectionTarget.BuildSelectionHudData());
    }

    public void Show(TacticsSelectionHudData hudData)
    {
        EnsureBuilt();
        if (!HasRequiredBindings())
        {
            return;
        }

        panelRoot.SetActive(true);
        nameText.text = string.IsNullOrWhiteSpace(hudData.DisplayName) ? "Unit" : hudData.DisplayName;
        levelText.text = hudData.Level.ToString();

        bool showOwner = !string.IsNullOrWhiteSpace(hudData.OwnerDisplayName);
        ownerPillImage.gameObject.SetActive(showOwner);
        ownerText.text = showOwner ? hudData.OwnerDisplayName : string.Empty;

        Color accentColor = hudData.AccentColor.a <= 0.001f ? fallbackAccentColor : hudData.AccentColor;
        ApplyAccentColor(accentColor);
        ApplyBar(healthBar, hudData.Health);
        ApplyBar(manaBar, hudData.Mana);
        ApplyBar(staminaBar, hudData.Stamina);
        ApplyBar(experienceBar, hudData.Experience);
        ApplyCounter(actionCounter, hudData.RemainingActions, accentColor);
        ApplyCounter(movementCounter, hudData.RemainingMovement, accentColor);
    }

    public void Hide()
    {
        EnsureBuilt();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

#if UNITY_EDITOR
    public void EditorBuildPrefabContent()
    {
        ClearExistingChildren();
        ResetBindings();
        EnsureBuilt();
    }
#endif

    private void EnsureBuilt()
    {
        sharedFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        raycaster.enabled = false;

        if (!TryBindExistingHierarchy())
        {
            BuildHierarchy();
        }

        ApplyLayout();
        ApplyHeader();
        ApplyAccentColor(fallbackAccentColor);
    }

    private bool TryBindExistingHierarchy()
    {
        if (panelRoot == null)
        {
            Transform rootTransform = transform.Find("SelectionPanel");
            panelRoot = rootTransform != null ? rootTransform.gameObject : null;
        }

        if (panelRoot == null)
        {
            return false;
        }

        glowImage = glowImage != null ? glowImage : FindImage("SelectionPanel/Glow");
        innerPanelImage = innerPanelImage != null ? innerPanelImage : FindImage("SelectionPanel/OuterFrame/InnerFrame");
        titleText = titleText != null ? titleText : FindText("SelectionPanel/OuterFrame/InnerFrame/Title");
        ownerPillImage = ownerPillImage != null ? ownerPillImage : FindImage("SelectionPanel/OuterFrame/InnerFrame/OwnerPill");
        ownerText = ownerText != null ? ownerText : FindText("SelectionPanel/OuterFrame/InnerFrame/OwnerPill/Owner");
        levelText = levelText != null ? levelText : FindText("SelectionPanel/OuterFrame/InnerFrame/LevelChip/LevelValue");
        nameText = nameText != null ? nameText : FindText("SelectionPanel/OuterFrame/InnerFrame/Name");

        healthBar = new ResourceRowWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/HPRow"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/HPRow/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/HPRow/Value"),
            FindImage("SelectionPanel/OuterFrame/InnerFrame/HPRow/Track/Fill"));
        manaBar = new ResourceRowWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/MPRow"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/MPRow/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/MPRow/Value"),
            FindImage("SelectionPanel/OuterFrame/InnerFrame/MPRow/Track/Fill"));
        staminaBar = new ResourceRowWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/STRow"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/STRow/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/STRow/Value"),
            FindImage("SelectionPanel/OuterFrame/InnerFrame/STRow/Track/Fill"));
        experienceBar = new ResourceRowWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/EXPRow"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/EXPRow/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/EXPRow/Value"),
            FindImage("SelectionPanel/OuterFrame/InnerFrame/EXPRow/Track/Fill"));
        actionCounter = new CounterWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/ACTCounter"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/ACTCounter/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/ACTCounter/Value"),
            FindChild("SelectionPanel/OuterFrame/InnerFrame/ACTCounter/Pips"));
        movementCounter = new CounterWidgets(
            FindChild("SelectionPanel/OuterFrame/InnerFrame/MOVCounter"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/MOVCounter/Label"),
            FindText("SelectionPanel/OuterFrame/InnerFrame/MOVCounter/Value"),
            FindChild("SelectionPanel/OuterFrame/InnerFrame/MOVCounter/Pips"));

        RebuildPipLists(actionCounter);
        RebuildPipLists(movementCounter);
        return HasRequiredBindings();
    }

    private void BuildHierarchy()
    {
        panelRoot = CreateUiObject("SelectionPanel", transform);

        GameObject shadow = CreateUiObject("Shadow", panelRoot.transform);
        RectTransform shadowRect = StretchRect(shadow);
        shadowRect.offsetMin = new Vector2(8f, -8f);
        shadowRect.offsetMax = new Vector2(8f, -8f);
        Image shadowImage = shadow.AddComponent<Image>();
        shadowImage.color = shadowColor;

        GameObject glow = CreateUiObject("Glow", panelRoot.transform);
        RectTransform glowRect = StretchRect(glow);
        glowRect.offsetMin = new Vector2(18f, 112f);
        glowRect.offsetMax = new Vector2(-18f, -14f);
        glowImage = glow.AddComponent<Image>();

        GameObject outerFrame = CreateUiObject("OuterFrame", panelRoot.transform);
        StretchRect(outerFrame);
        Image outerImage = outerFrame.AddComponent<Image>();
        outerImage.color = panelColor;
        Outline outerOutline = outerFrame.AddComponent<Outline>();
        outerOutline.effectColor = frameColor;
        outerOutline.effectDistance = new Vector2(1f, -1f);

        GameObject innerFrame = CreateUiObject("InnerFrame", outerFrame.transform);
        RectTransform innerRect = StretchRect(innerFrame);
        innerRect.offsetMin = new Vector2(10f, 10f);
        innerRect.offsetMax = new Vector2(-10f, -10f);
        innerPanelImage = innerFrame.AddComponent<Image>();

        BuildHeader(innerFrame.transform);
        BuildName(innerFrame.transform);
        healthBar = BuildResourceRow(innerFrame.transform, "HP", 100f);
        manaBar = BuildResourceRow(innerFrame.transform, "MP", 124f);
        staminaBar = BuildResourceRow(innerFrame.transform, "ST", 148f);
        experienceBar = BuildResourceRow(innerFrame.transform, "EXP", 172f);
        actionCounter = BuildCounter(innerFrame.transform, "ACT", new Vector2(-128f, 10f));
        movementCounter = BuildCounter(innerFrame.transform, "MOV", new Vector2(-14f, 10f));
    }

    private void BuildHeader(Transform parent)
    {
        titleText = CreateText("Title", parent, 10, FontStyle.Bold, fallbackAccentColor, TextAnchor.MiddleLeft);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(150f, 14f));

        GameObject ownerPill = CreateUiObject("OwnerPill", parent);
        ownerPillImage = ownerPill.AddComponent<Image>();
        SetRect(ownerPill.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -36f), new Vector2(160f, 18f));

        ownerText = CreateText("Owner", ownerPill.transform, 10, FontStyle.Normal, secondaryTextColor, TextAnchor.MiddleCenter);
        SetRect(ownerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 2f), new Vector2(-8f, -2f));

        GameObject levelChip = CreateUiObject("LevelChip", parent);
        SetRect(levelChip.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(64f, 24f));
        Image levelBackground = levelChip.AddComponent<Image>();
        levelBackground.color = new Color(0.09f, 0.14f, 0.22f, 0.42f);
        Outline levelOutline = levelChip.AddComponent<Outline>();
        levelOutline.effectColor = new Color(frameColor.r, frameColor.g, frameColor.b, 0.32f);
        levelOutline.effectDistance = new Vector2(1f, -1f);

        Text levelPrefix = CreateText("LevelPrefix", levelChip.transform, 10, FontStyle.Bold, mutedTextColor, TextAnchor.MiddleLeft);
        SetRect(levelPrefix.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(18f, 0f));
        levelPrefix.text = "Lv";

        levelText = CreateText("LevelValue", levelChip.transform, 14, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleRight);
        SetRect(levelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(22f, 0f), new Vector2(-8f, 0f));
    }

    private void BuildName(Transform parent)
    {
        nameText = CreateText("Name", parent, 22, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleLeft);
        SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -66f), new Vector2(-16f, -34f));
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private ResourceRowWidgets BuildResourceRow(Transform parent, string labelText, float topOffset)
    {
        GameObject row = CreateUiObject($"{labelText}Row", parent);
        SetRect(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -topOffset), new Vector2(-16f, -(topOffset - 18f)));

        Text label = CreateText("Label", row.transform, 11, FontStyle.Bold, fallbackAccentColor, TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 0f));

        GameObject track = CreateUiObject("Track", row.transform);
        SetRect(track.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(34f, 3f), new Vector2(-74f, -3f));
        Image trackImage = track.AddComponent<Image>();
        trackImage.color = trackColor;
        Outline trackOutline = track.AddComponent<Outline>();
        trackOutline.effectColor = trackBorderColor;
        trackOutline.effectDistance = new Vector2(1f, -1f);

        GameObject fill = CreateUiObject("Fill", track.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(0f, -1f);
        Image fillImage = fill.AddComponent<Image>();

        GameObject shine = CreateUiObject("Shine", track.transform);
        RectTransform shineRect = StretchRect(shine);
        shineRect.offsetMin = new Vector2(1f, 6f);
        shineRect.offsetMax = new Vector2(-1f, -1f);
        Image shineImage = shine.AddComponent<Image>();
        shineImage.color = new Color(1f, 1f, 1f, 0.05f);

        Text value = CreateText("Value", row.transform, 11, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleRight);
        SetRect(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(66f, 0f));

        label.text = labelText;
        return new ResourceRowWidgets(row, label, value, fillImage);
    }

    private CounterWidgets BuildCounter(Transform parent, string labelText, Vector2 anchoredBottomRight)
    {
        GameObject root = CreateUiObject($"{labelText}Counter", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredBottomRight;
        rect.sizeDelta = new Vector2(104f, 42f);

        Image background = root.AddComponent<Image>();
        background.color = indicatorColor;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = trackBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Text label = CreateText("Label", root.transform, 9, FontStyle.Bold, fallbackAccentColor, TextAnchor.UpperLeft);
        SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(10f, -2f), new Vector2(-10f, -14f));
        label.text = labelText;

        GameObject pipContainer = CreateUiObject("Pips", root.transform);
        RectTransform pipRect = pipContainer.GetComponent<RectTransform>();
        pipRect.anchorMin = new Vector2(0f, 0f);
        pipRect.anchorMax = new Vector2(1f, 0f);
        pipRect.offsetMin = new Vector2(10f, 14f);
        pipRect.offsetMax = new Vector2(-42f, 22f);

        Text value = CreateText("Value", root.transform, 11, FontStyle.Bold, primaryTextColor, TextAnchor.LowerRight);
        SetRect(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(10f, 2f), new Vector2(-8f, -4f));

        return new CounterWidgets(root, label, value, pipContainer);
    }

    private void ApplyLayout()
    {
        if (panelRoot == null)
        {
            return;
        }

        RectTransform rect = panelRoot.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = panelSize;
    }

    private void ApplyHeader()
    {
        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(panelTitle)
                ? panelRole.ToString().ToUpperInvariant()
                : panelTitle.ToUpperInvariant();
        }
    }

    private void ApplyAccentColor(Color accentColor)
    {
        if (titleText != null)
        {
            titleText.color = accentColor;
        }

        if (glowImage != null)
        {
            glowImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.08f);
        }

        if (innerPanelImage != null)
        {
            innerPanelImage.color = panelRole == TacticsSelectionPanelRole.ActiveCharacter
                ? new Color(0.07f, 0.12f, 0.19f, 0.36f)
                : new Color(0.07f, 0.10f, 0.16f, 0.32f);
        }

        if (ownerPillImage != null)
        {
            ownerPillImage.color = ownerPillColor;
        }
    }

    private static void ApplyBar(ResourceRowWidgets widgets, TacticsSelectionHudResourceData resourceData)
    {
        if (widgets.Root == null)
        {
            return;
        }

        widgets.Root.SetActive(resourceData.IsVisible);
        if (!resourceData.IsVisible)
        {
            return;
        }

        widgets.Label.text = resourceData.Label.ToUpperInvariant();
        widgets.Value.text = resourceData.ValueText;
        widgets.Fill.color = resourceData.FillColor;

        RectTransform fillRect = widgets.Fill.rectTransform;
        fillRect.anchorMax = new Vector2(resourceData.FillNormalized, 1f);
        fillRect.offsetMax = new Vector2(resourceData.FillNormalized > 0.001f ? -1f : 0f, -1f);
        widgets.Fill.enabled = resourceData.FillNormalized > 0.001f;
    }

    private void ApplyCounter(CounterWidgets widgets, TacticsSelectionHudCounterData counterData, Color accentColor)
    {
        if (widgets.Root == null)
        {
            return;
        }

        widgets.Root.SetActive(counterData.IsVisible);
        if (!counterData.IsVisible)
        {
            return;
        }

        widgets.Label.text = counterData.Label.ToUpperInvariant();
        widgets.Value.text = counterData.ValueText;
        SyncPips(widgets, counterData.MaxValue);

        for (int i = 0; i < widgets.Pips.Count; i++)
        {
            widgets.Pips[i].color = i < counterData.CurrentValue ? accentColor : pipOffColor;
        }
    }

    private static void SyncPips(CounterWidgets widgets, int count)
    {
        if (widgets.PipContainer == null)
        {
            return;
        }

        int desiredCount = Mathf.Max(0, count);
        while (widgets.Pips.Count < desiredCount)
        {
            GameObject pip = CreateUiObject("Pip", widgets.PipContainer.transform);
            RectTransform rect = pip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            Image image = pip.AddComponent<Image>();
            widgets.Pips.Add(image);
        }

        for (int i = 0; i < widgets.Pips.Count; i++)
        {
            bool isVisible = i < desiredCount;
            widgets.Pips[i].gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                continue;
            }

            RectTransform rect = widgets.Pips[i].rectTransform;
            rect.anchoredPosition = new Vector2(i * 18f, 0f);
            rect.sizeDelta = new Vector2(14f, 6f);
        }
    }

    private void RebuildPipLists(CounterWidgets widgets)
    {
        widgets.Pips.Clear();
        if (widgets.PipContainer == null)
        {
            return;
        }

        for (int i = 0; i < widgets.PipContainer.transform.childCount; i++)
        {
            Image pip = widgets.PipContainer.transform.GetChild(i).GetComponent<Image>();
            if (pip != null)
            {
                widgets.Pips.Add(pip);
            }
        }
    }

    private bool HasRequiredBindings()
    {
        return panelRoot != null &&
               titleText != null &&
               ownerPillImage != null &&
               ownerText != null &&
               levelText != null &&
               nameText != null;
    }

    private void ResetBindings()
    {
        panelRoot = null;
        glowImage = null;
        innerPanelImage = null;
        titleText = null;
        ownerPillImage = null;
        ownerText = null;
        levelText = null;
        nameText = null;
        healthBar = default;
        manaBar = default;
        staminaBar = default;
        experienceBar = default;
        actionCounter = new CounterWidgets(null, null, null, null);
        movementCounter = new CounterWidgets(null, null, null, null);
    }

    private void ClearExistingChildren()
    {
#if UNITY_EDITOR
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            Object.DestroyImmediate(child);
        }
#endif
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Text FindText(string path)
    {
        GameObject target = FindChild(path);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private Image FindImage(string path)
    {
        GameObject target = FindChild(path);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private GameObject FindChild(string path)
    {
        Transform found = transform.Find(path);
        return found != null ? found.gameObject : null;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMinValue, Vector2 anchorMaxValue, Vector2 rectPivot, Vector2 offsetMinValue, Vector2 offsetMaxValue)
    {
        rect.anchorMin = anchorMinValue;
        rect.anchorMax = anchorMaxValue;
        rect.pivot = rectPivot;
        rect.offsetMin = offsetMinValue;
        rect.offsetMax = offsetMaxValue;
    }

    private static RectTransform StretchRect(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private readonly struct ResourceRowWidgets
    {
        public ResourceRowWidgets(GameObject root, Text label, Text value, Image fill)
        {
            Root = root;
            Label = label;
            Value = value;
            Fill = fill;
        }

        public GameObject Root { get; }
        public Text Label { get; }
        public Text Value { get; }
        public Image Fill { get; }
    }

    private sealed class CounterWidgets
    {
        public CounterWidgets(GameObject root, Text label, Text value, GameObject pipContainer)
        {
            Root = root;
            Label = label;
            Value = value;
            PipContainer = pipContainer;
        }

        public GameObject Root { get; }
        public Text Label { get; }
        public Text Value { get; }
        public GameObject PipContainer { get; }
        public List<Image> Pips { get; } = new();
    }
}
