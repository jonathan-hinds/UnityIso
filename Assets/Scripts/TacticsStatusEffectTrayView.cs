using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsStatusEffectTrayView : MonoBehaviour
{
    private const int SlotsPerSection = 8;
    private static readonly Vector2 TrayOffset = new(0f, 42f);
    private static TacticsStatusEffectTrayView instance;

    [Header("Theme")]
    [SerializeField] private Color trayColor = new(0f, 0f, 0f, 0.42f);
    [SerializeField] private Color buffSectionColor = new(0f, 0f, 0f, 0.42f);
    [SerializeField] private Color debuffSectionColor = new(0f, 0f, 0f, 0.42f);
    [SerializeField] private Color outlineColor = new(0.82f, 0.86f, 0.95f, 0.22f);
    [SerializeField] private Color dividerColor = new(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color labelColor = new(0.9f, 0.93f, 0.98f, 0.78f);
    [SerializeField] private Color emptySlotColor = new(0f, 0f, 0f, 0.32f);
    [SerializeField] private Color emptySlotOutlineColor = new(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color slotTextColor = new(0f, 0f, 0f, 0.88f);
    [SerializeField] private Color slotIconColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private Color occupiedSlotColor = new(0f, 0f, 0f, 0.48f);

    private readonly List<StatusEffectSlotWidgets> buffSlots = new(SlotsPerSection);
    private readonly List<StatusEffectSlotWidgets> debuffSlots = new(SlotsPerSection);
    private readonly List<TacticsStatusEffectInstance> reusableBuffs = new(SlotsPerSection);
    private readonly List<TacticsStatusEffectInstance> reusableDebuffs = new(SlotsPerSection);

    private Canvas rootCanvas;
    private RectTransform trayRect;
    private TacticsAbilityTooltipView tooltipView;
    private Font sharedFont;
    private TacticsCharacterController boundCharacter;
    private Camera targetCamera;

    public static TacticsStatusEffectTrayView Instance => EnsureInstance();

    public static void ToggleFor(TacticsCharacterController character, Camera worldCamera = null)
    {
        if (character == null)
        {
            HideTray();
            return;
        }

        TacticsStatusEffectTrayView tray = EnsureInstance();
        if (tray.boundCharacter == character && tray.IsVisible)
        {
            tray.Hide();
            return;
        }

        tray.ShowFor(character, worldCamera);
    }

    public static void HideTray()
    {
        if (instance != null)
        {
            instance.Hide();
        }
    }

    public static bool IsOpen => instance != null && instance.IsVisible;

    public bool IsVisible => trayRect != null && trayRect.gameObject.activeSelf;

    private static TacticsStatusEffectTrayView EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<TacticsStatusEffectTrayView>();
        if (instance != null)
        {
            return instance;
        }

        GameObject trayObject = new("Status Effect Tray HUD");
        instance = trayObject.AddComponent<TacticsStatusEffectTrayView>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureBuilt();
        Hide();
    }

    private void LateUpdate()
    {
        if (!IsVisible)
        {
            return;
        }

        if (boundCharacter == null ||
            !boundCharacter.isActiveAndEnabled ||
            !boundCharacter.IsAlive ||
            !boundCharacter.IsPresentationVisible ||
            boundCharacter.ActiveStatusEffects.Count == 0)
        {
            Hide();
            return;
        }

        RefreshContents();
        UpdateTrayPosition();
    }

    public void ShowFor(TacticsCharacterController character, Camera worldCamera = null)
    {
        if (character == null)
        {
            Hide();
            return;
        }

        EnsureBuilt();
        boundCharacter = character;
        targetCamera = worldCamera != null ? worldCamera : (Camera.main != null ? Camera.main : targetCamera);
        RefreshContents();
        UpdateTrayPosition();
        trayRect.gameObject.SetActive(true);
        trayRect.SetAsLastSibling();
    }

    public void Hide()
    {
        boundCharacter = null;
        tooltipView?.Hide();
        if (trayRect != null)
        {
            trayRect.gameObject.SetActive(false);
        }
    }

    private void EnsureBuilt()
    {
        if (trayRect != null)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 5075;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        tooltipView = GetComponent<TacticsAbilityTooltipView>();
        if (tooltipView == null)
        {
            tooltipView = gameObject.AddComponent<TacticsAbilityTooltipView>();
        }

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        GameObject trayObject = CreateUiObject("Tray", transform);
        trayRect = trayObject.GetComponent<RectTransform>();
        trayRect.anchorMin = new Vector2(0.5f, 0.5f);
        trayRect.anchorMax = new Vector2(0.5f, 0.5f);
        trayRect.pivot = new Vector2(0.5f, 0f);
        trayRect.sizeDelta = new Vector2(292f, 270f);

        Image trayImage = trayObject.AddComponent<Image>();
        trayImage.color = trayColor;

        Outline trayOutline = trayObject.AddComponent<Outline>();
        trayOutline.effectColor = outlineColor;
        trayOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup trayLayout = trayObject.AddComponent<VerticalLayoutGroup>();
        trayLayout.padding = new RectOffset(10, 10, 10, 10);
        trayLayout.spacing = 8f;
        trayLayout.childAlignment = TextAnchor.UpperCenter;
        trayLayout.childControlHeight = true;
        trayLayout.childControlWidth = true;
        trayLayout.childForceExpandHeight = false;
        trayLayout.childForceExpandWidth = true;

        CreateSection("Buffs", trayObject.transform, buffSlots, buffSectionColor);
        CreateDivider(trayObject.transform);
        CreateSection("Debuffs", trayObject.transform, debuffSlots, debuffSectionColor);
    }

    private void CreateSection(string title, Transform parent, List<StatusEffectSlotWidgets> slots, Color backgroundColor)
    {
        GameObject sectionObject = CreateUiObject($"{title}Section", parent);
        Image sectionImage = sectionObject.AddComponent<Image>();
        sectionImage.color = backgroundColor;

        VerticalLayoutGroup sectionLayout = sectionObject.AddComponent<VerticalLayoutGroup>();
        sectionLayout.padding = new RectOffset(8, 8, 8, 8);
        sectionLayout.spacing = 6f;
        sectionLayout.childAlignment = TextAnchor.UpperCenter;
        sectionLayout.childControlHeight = true;
        sectionLayout.childControlWidth = true;
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.childForceExpandWidth = true;

        LayoutElement sectionElement = sectionObject.AddComponent<LayoutElement>();
        sectionElement.preferredHeight = 116f;

        Text header = CreateText($"{title}Label", sectionObject.transform, 13, FontStyle.Bold, labelColor, TextAnchor.MiddleLeft);
        header.text = title.ToUpperInvariant();

        GameObject gridObject = CreateUiObject($"{title}Grid", sectionObject.transform);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(58f, 38f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperCenter;

        LayoutElement gridElement = gridObject.AddComponent<LayoutElement>();
        gridElement.preferredHeight = 82f;

        for (int i = 0; i < SlotsPerSection; i++)
        {
            slots.Add(CreateSlot($"{title}Slot{i}", gridObject.transform));
        }
    }

    private void CreateDivider(Transform parent)
    {
        GameObject dividerObject = CreateUiObject("Divider", parent);
        LayoutElement dividerLayout = dividerObject.AddComponent<LayoutElement>();
        dividerLayout.preferredHeight = 1f;
        dividerLayout.minHeight = 1f;

        Image dividerImage = dividerObject.AddComponent<Image>();
        dividerImage.color = dividerColor;
    }

    private StatusEffectSlotWidgets CreateSlot(string objectName, Transform parent)
    {
        GameObject slotObject = CreateUiObject(objectName, parent);
        Image backgroundImage = slotObject.AddComponent<Image>();
        backgroundImage.color = emptySlotColor;

        Button button = slotObject.AddComponent<Button>();
        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = emptySlotColor;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = emptySlotColor;
        button.colors = colors;

        Outline outline = slotObject.AddComponent<Outline>();
        outline.effectColor = emptySlotOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject iconObject = CreateUiObject("Icon", slotObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = new Vector2(4f, 4f);
        iconRect.offsetMax = new Vector2(-4f, -4f);

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        Text labelText = CreateText("Label", slotObject.transform, 13, FontStyle.Bold, slotTextColor, TextAnchor.MiddleCenter);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TacticsAbilityTooltipTrigger trigger = slotObject.AddComponent<TacticsAbilityTooltipTrigger>();
        return new StatusEffectSlotWidgets(button, backgroundImage, iconImage, labelText, outline, trigger);
    }

    private void RefreshContents()
    {
        reusableBuffs.Clear();
        reusableDebuffs.Clear();

        IReadOnlyList<TacticsStatusEffectInstance> activeEffects = boundCharacter != null
            ? boundCharacter.ActiveStatusEffects
            : null;
        if (activeEffects != null)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                TacticsStatusEffectInstance effect = activeEffects[i];
                if (effect.IsExpired)
                {
                    continue;
                }

                TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(effect.StatusEffectType);
                if (descriptor.IsBuff)
                {
                    if (reusableBuffs.Count < SlotsPerSection)
                    {
                        reusableBuffs.Add(effect);
                    }
                }
                else if (reusableDebuffs.Count < SlotsPerSection)
                {
                    reusableDebuffs.Add(effect);
                }
            }
        }

        for (int i = 0; i < buffSlots.Count; i++)
        {
            ConfigureSlot(buffSlots[i], i < reusableBuffs.Count ? reusableBuffs[i] : default, i < reusableBuffs.Count);
        }

        for (int i = 0; i < debuffSlots.Count; i++)
        {
            ConfigureSlot(debuffSlots[i], i < reusableDebuffs.Count ? reusableDebuffs[i] : default, i < reusableDebuffs.Count);
        }
    }

    private void ConfigureSlot(StatusEffectSlotWidgets widgets, TacticsStatusEffectInstance effect, bool isOccupied)
    {
        if (!isOccupied)
        {
            widgets.Background.color = emptySlotColor;
            widgets.Icon.enabled = false;
            widgets.Label.enabled = false;
            widgets.Outline.effectColor = emptySlotOutlineColor;
            widgets.Button.interactable = false;
            widgets.Trigger.Initialize(null, _ => tooltipView?.Hide(), null);
            return;
        }

        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(effect.StatusEffectType);
        Sprite iconSprite = TacticsStatusEffectLibrary.GetIconSprite(effect.StatusEffectType);
        widgets.Button.interactable = true;
        widgets.Background.color = occupiedSlotColor;
        widgets.Outline.effectColor = new Color(descriptor.AccentColor.r, descriptor.AccentColor.g, descriptor.AccentColor.b, 0.6f);
        widgets.Icon.enabled = iconSprite != null;
        widgets.Icon.sprite = iconSprite;
        widgets.Icon.color = slotIconColor;
        widgets.Label.enabled = iconSprite == null;
        widgets.Label.text = descriptor.ShortLabel;
        widgets.Label.color = slotTextColor;
        TacticsAbilityTooltipContent content = TacticsStatusEffectLibrary.BuildTooltipContent(effect);

        widgets.Trigger.Initialize(
            eventData => ShowTooltip(content, eventData),
            _ => tooltipView?.Hide(),
            eventData => MoveTooltip(eventData));
    }

    private void ShowTooltip(TacticsAbilityTooltipContent content, PointerEventData eventData)
    {
        if (tooltipView == null || rootCanvas == null || eventData == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, rootCanvas.worldCamera, out Vector2 canvasPosition))
        {
            return;
        }

        tooltipView.ShowInCanvasSpace(content, canvasPosition, rootCanvas);
    }

    private void MoveTooltip(PointerEventData eventData)
    {
        if (tooltipView == null || rootCanvas == null || eventData == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, rootCanvas.worldCamera, out Vector2 canvasPosition))
        {
            return;
        }

        tooltipView.UpdatePositionInCanvasSpace(canvasPosition);
    }

    private void UpdateTrayPosition()
    {
        if (boundCharacter == null || trayRect == null || rootCanvas == null)
        {
            return;
        }

        Camera worldCameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (worldCameraToUse == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Vector3 screenPosition = worldCameraToUse.WorldToScreenPoint(boundCharacter.GetCombatTextSpawnPosition(0.12f));
        if (screenPosition.z < 0f)
        {
            Hide();
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, rootCanvas.worldCamera, out Vector2 canvasPosition))
        {
            trayRect.anchoredPosition = canvasPosition + TrayOffset;
        }
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
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private readonly struct StatusEffectSlotWidgets
    {
        public StatusEffectSlotWidgets(
            Button button,
            Image background,
            Image icon,
            Text label,
            Outline outline,
            TacticsAbilityTooltipTrigger trigger)
        {
            Button = button;
            Background = background;
            Icon = icon;
            Label = label;
            Outline = outline;
            Trigger = trigger;
        }

        public Button Button { get; }
        public Image Background { get; }
        public Image Icon { get; }
        public Text Label { get; }
        public Outline Outline { get; }
        public TacticsAbilityTooltipTrigger Trigger { get; }
    }
}
