using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsLootBannerView : MonoBehaviour
{
    private const float DefaultBannerWidth = 720f;
    private const float DefaultBannerHeight = 64f;

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 4996;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasScaler rootScaler;
    [SerializeField] private GraphicRaycaster raycaster;
    [SerializeField] private TacticsAbilityTooltipView tooltipView;

    [Header("Layout")]
    [SerializeField] private Vector2 stackOffset = new(0f, 42f);
    [SerializeField] private float stackSpacing = 8f;
    [SerializeField] private float bannerWidth = DefaultBannerWidth;
    [SerializeField] private float bannerHeight = DefaultBannerHeight;

    [Header("Timing")]
    [SerializeField] private float popDurationSeconds = 0.18f;
    [SerializeField] private float visibleDurationSeconds = 2.4f;
    [SerializeField] private float fadeDurationSeconds = 0.42f;
    [SerializeField] private float spawnStaggerSeconds = 0.24f;

    [Header("Theme")]
    [SerializeField] private Color panelColor = new(0.04f, 0.04f, 0.05f, 0.92f);
    [SerializeField] private Color innerPanelColor = new(1f, 1f, 1f, 0.035f);
    [SerializeField] private Color borderColor = new(0.86f, 0.74f, 0.38f, 1f);
    [SerializeField] private Color textColor = new(0.97f, 0.95f, 0.88f, 1f);
    [SerializeField] private Color detailColor = new(0.88f, 0.79f, 0.5f, 1f);
    [SerializeField] private Color quantityColor = new(1f, 0.95f, 0.79f, 1f);

    private readonly Queue<LootBannerRequest> pendingRequests = new();
    private readonly List<LootBannerEntryView> pooledEntries = new();
    private RectTransform stackRoot;
    private Font sharedFont;
    private Coroutine queueRoutine;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void EnqueueLoot(TacticsCharacterController character, TacticsInventoryItemAddedEvent itemAddedEvent)
    {
        if (character == null || !itemAddedEvent.IsValid)
        {
            return;
        }

        pendingRequests.Enqueue(new LootBannerRequest(character, itemAddedEvent));
        if (queueRoutine == null)
        {
            queueRoutine = StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        while (pendingRequests.Count > 0)
        {
            LootBannerRequest request = pendingRequests.Dequeue();
            LootBannerEntryView entry = GetEntry();
            entry.Bind(
                request.Character,
                request.ItemAddedEvent,
                tooltipView,
                rootCanvas,
                TacticsItemTooltipUtility.BuildTooltipContent(request.ItemAddedEvent.ItemDefinition));
            entry.Play(popDurationSeconds, visibleDurationSeconds, fadeDurationSeconds, HandleEntryFinished);

            if (pendingRequests.Count > 0)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, spawnStaggerSeconds));
            }
        }

        queueRoutine = null;
    }

    private void HandleEntryFinished(LootBannerEntryView entry)
    {
        entry?.ResetView();
    }

    private LootBannerEntryView GetEntry()
    {
        EnsureBuilt();
        for (int i = 0; i < pooledEntries.Count; i++)
        {
            LootBannerEntryView pooledEntry = pooledEntries[i];
            if (pooledEntry == null || pooledEntry.gameObject.activeSelf)
            {
                continue;
            }

            pooledEntry.transform.SetAsLastSibling();
            return pooledEntry;
        }

        LootBannerEntryView createdEntry = CreateEntry();
        pooledEntries.Add(createdEntry);
        return createdEntry;
    }

    private void EnsureBuilt()
    {
        if (stackRoot != null)
        {
            return;
        }

        rootCanvas = rootCanvas != null ? rootCanvas : GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootScaler = rootScaler != null ? rootScaler : GetComponent<CanvasScaler>();
        if (rootScaler == null)
        {
            rootScaler = gameObject.AddComponent<CanvasScaler>();
        }

        raycaster = raycaster != null ? raycaster : GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        tooltipView = tooltipView != null ? tooltipView : GetComponent<TacticsAbilityTooltipView>();
        if (tooltipView == null)
        {
            tooltipView = gameObject.AddComponent<TacticsAbilityTooltipView>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;
        rootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        rootScaler.referenceResolution = new Vector2(1920f, 1080f);
        rootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        rootScaler.matchWidthOrHeight = 0.5f;

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject stackObject = CreateUiObject("LootBannerStack", transform);
        stackRoot = stackObject.GetComponent<RectTransform>();
        stackRoot.anchorMin = new Vector2(0.5f, 0f);
        stackRoot.anchorMax = new Vector2(0.5f, 0f);
        stackRoot.pivot = new Vector2(0.5f, 0f);
        stackRoot.anchoredPosition = stackOffset;
        stackRoot.sizeDelta = new Vector2(bannerWidth, 0f);

        VerticalLayoutGroup layoutGroup = stackObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = stackSpacing;
        layoutGroup.childAlignment = TextAnchor.LowerCenter;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;

        ContentSizeFitter fitter = stackObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private LootBannerEntryView CreateEntry()
    {
        GameObject rootObject = CreateUiObject($"LootBanner{pooledEntries.Count + 1}", stackRoot);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(bannerWidth, bannerHeight);

        LayoutElement layoutElement = rootObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = bannerWidth;
        layoutElement.preferredHeight = bannerHeight;

        Image background = rootObject.AddComponent<Image>();
        background.color = panelColor;

        Outline outline = rootObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = rootObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
        shadow.effectDistance = new Vector2(0f, -3f);

        CanvasGroup canvasGroup = rootObject.AddComponent<CanvasGroup>();
        TacticsAbilityTooltipTrigger tooltipTrigger = rootObject.AddComponent<TacticsAbilityTooltipTrigger>();

        GameObject chromeObject = CreateUiObject("Chrome", rootObject.transform);
        RectTransform chromeRect = chromeObject.GetComponent<RectTransform>();
        chromeRect.anchorMin = Vector2.zero;
        chromeRect.anchorMax = Vector2.one;
        chromeRect.offsetMin = new Vector2(6f, 6f);
        chromeRect.offsetMax = new Vector2(-6f, -6f);
        Image chromeImage = chromeObject.AddComponent<Image>();
        chromeImage.color = innerPanelColor;
        chromeImage.raycastTarget = false;

        GameObject iconObject = CreateUiObject("Icon", rootObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(18f, 0f);
        iconRect.sizeDelta = new Vector2(44f, 44f);
        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.preserveAspect = true;

        Text titleText = CreateText("Title", rootObject.transform, 20, FontStyle.Bold, TextAnchor.MiddleLeft, textColor);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.offsetMin = new Vector2(78f, 2f);
        titleRect.offsetMax = new Vector2(-132f, 26f);

        Text detailText = CreateText("Detail", rootObject.transform, 14, FontStyle.Normal, TextAnchor.MiddleLeft, detailColor);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0.5f);
        detailRect.anchorMax = new Vector2(1f, 0.5f);
        detailRect.offsetMin = new Vector2(78f, -22f);
        detailRect.offsetMax = new Vector2(-132f, 0f);

        Text quantityText = CreateText("Quantity", rootObject.transform, 20, FontStyle.Bold, TextAnchor.MiddleRight, quantityColor);
        RectTransform quantityRect = quantityText.rectTransform;
        quantityRect.anchorMin = new Vector2(1f, 0.5f);
        quantityRect.anchorMax = new Vector2(1f, 0.5f);
        quantityRect.pivot = new Vector2(1f, 0.5f);
        quantityRect.anchoredPosition = new Vector2(-20f, 0f);
        quantityRect.sizeDelta = new Vector2(100f, 32f);

        LootBannerEntryView entry = rootObject.AddComponent<LootBannerEntryView>();
        entry.Configure(rootRect, canvasGroup, iconImage, titleText, detailText, quantityText, tooltipTrigger);
        entry.ResetView();
        return entry;
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private readonly struct LootBannerRequest
    {
        public LootBannerRequest(TacticsCharacterController character, TacticsInventoryItemAddedEvent itemAddedEvent)
        {
            Character = character;
            ItemAddedEvent = itemAddedEvent;
        }

        public TacticsCharacterController Character { get; }

        public TacticsInventoryItemAddedEvent ItemAddedEvent { get; }
    }

    [DisallowMultipleComponent]
    private sealed class LootBannerEntryView : MonoBehaviour
    {
        private RectTransform root;
        private CanvasGroup canvasGroup;
        private Image iconImage;
        private Text titleText;
        private Text detailText;
        private Text quantityText;
        private TacticsAbilityTooltipTrigger tooltipTrigger;
        private TacticsAbilityTooltipView tooltipView;
        private Canvas rootCanvas;
        private TacticsAbilityTooltipContent tooltipContent;
        private Coroutine presentationRoutine;
        private bool isPointerOver;

        public void Configure(
            RectTransform boundRoot,
            CanvasGroup boundCanvasGroup,
            Image boundIconImage,
            Text boundTitleText,
            Text boundDetailText,
            Text boundQuantityText,
            TacticsAbilityTooltipTrigger boundTooltipTrigger)
        {
            root = boundRoot;
            canvasGroup = boundCanvasGroup;
            iconImage = boundIconImage;
            titleText = boundTitleText;
            detailText = boundDetailText;
            quantityText = boundQuantityText;
            tooltipTrigger = boundTooltipTrigger;
        }

        public void Bind(
            TacticsCharacterController character,
            TacticsInventoryItemAddedEvent itemAddedEvent,
            TacticsAbilityTooltipView sharedTooltipView,
            Canvas sharedCanvas,
            TacticsAbilityTooltipContent sharedTooltipContent)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            tooltipView = sharedTooltipView;
            rootCanvas = sharedCanvas;
            tooltipContent = sharedTooltipContent;
            isPointerOver = false;

            if (iconImage != null)
            {
                iconImage.sprite = itemAddedEvent.ItemDefinition != null ? itemAddedEvent.ItemDefinition.Thumbnail : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (titleText != null)
            {
                titleText.text = itemAddedEvent.ItemDefinition != null
                    ? $"LOOT RECEIVED: {itemAddedEvent.ItemDefinition.DisplayName.ToUpperInvariant()}"
                    : "LOOT RECEIVED";
            }

            if (detailText != null)
            {
                detailText.text = BuildDetailLine(character, itemAddedEvent);
            }

            if (quantityText != null)
            {
                quantityText.text = itemAddedEvent.QuantityAdded > 1
                    ? $"+{itemAddedEvent.QuantityAdded}"
                    : string.Empty;
            }

            tooltipTrigger?.Initialize(HandlePointerEnter, HandlePointerExit, HandlePointerMove);
        }

        public void Play(float popDurationSeconds, float visibleDurationSeconds, float fadeDurationSeconds, Action<LootBannerEntryView> onFinished)
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
            }

            presentationRoutine = StartCoroutine(PlayRoutine(popDurationSeconds, visibleDurationSeconds, fadeDurationSeconds, onFinished));
        }

        public void ResetView()
        {
            if (presentationRoutine != null)
            {
                StopCoroutine(presentationRoutine);
                presentationRoutine = null;
            }

            isPointerOver = false;
            tooltipTrigger?.Initialize(null, null, null);
            tooltipView?.Hide();
            tooltipView = null;
            rootCanvas = null;
            tooltipContent = default;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (root != null)
            {
                root.localScale = Vector3.one;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator PlayRoutine(float popDurationSeconds, float visibleDurationSeconds, float fadeDurationSeconds, Action<LootBannerEntryView> onFinished)
        {
            if (root == null || canvasGroup == null)
            {
                yield break;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            root.localScale = new Vector3(0.92f, 0.92f, 1f);

            yield return Animate(0f, 1f, 0.92f, 1f, Mathf.Max(0.01f, popDurationSeconds));
            yield return WaitWhileHoverAware(Mathf.Max(0.01f, visibleDurationSeconds));
            yield return Animate(1f, 0f, 1f, 1f, Mathf.Max(0.01f, fadeDurationSeconds));

            presentationRoutine = null;
            onFinished?.Invoke(this);
        }

        private IEnumerator Animate(float fromAlpha, float toAlpha, float fromScale, float toScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
                }

                if (root != null)
                {
                    float scale = Mathf.LerpUnclamped(fromScale, toScale, eased);
                    root.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = toAlpha;
            }

            if (root != null)
            {
                root.localScale = new Vector3(toScale, toScale, 1f);
            }
        }

        private IEnumerator WaitWhileHoverAware(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!isPointerOver)
                {
                    elapsed += Time.unscaledDeltaTime;
                }

                yield return null;
            }
        }

        private void HandlePointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;
            if (tooltipView == null || rootCanvas == null || !tooltipContent.IsValid)
            {
                return;
            }

            Vector2 pointerPosition = eventData != null ? eventData.position : Input.mousePosition;
            tooltipView.Show(tooltipContent, pointerPosition, rootCanvas);
        }

        private void HandlePointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            tooltipView?.Hide();
        }

        private void HandlePointerMove(PointerEventData eventData)
        {
            if (tooltipView == null)
            {
                return;
            }

            tooltipView.UpdatePosition(eventData != null ? eventData.position : Input.mousePosition);
        }

        private static string BuildDetailLine(TacticsCharacterController character, TacticsInventoryItemAddedEvent itemAddedEvent)
        {
            string ownerName = character != null && !string.IsNullOrWhiteSpace(character.DisplayName)
                ? character.DisplayName.ToUpperInvariant()
                : "PARTY";

            string itemType = itemAddedEvent.ItemDefinition switch
            {
                TacticsWeaponItemDefinition weapon => weapon.WeaponType.ToString().ToUpperInvariant(),
                TacticsEquipmentItemDefinition equipment => equipment.Slot.ToString().ToUpperInvariant(),
                TacticsConsumableItemDefinition => "CONSUMABLE",
                _ => "ITEM"
            };

            string stackText = itemAddedEvent.MergedIntoExistingStack ? "STACK UPDATED" : "ADDED TO INVENTORY";
            return $"{ownerName}  |  {itemType}  |  {stackText}";
        }
    }
}
