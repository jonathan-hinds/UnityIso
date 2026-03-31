using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TacticsInventoryMenuPrefabBuilder
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string UiFolder = "Assets/Resources/UI";
    private const string InventoryPanelPrefabPath = "Assets/Resources/UI/TacticsInventoryPanel.prefab";
    private const string InventoryItemCardPrefabPath = "Assets/Resources/UI/TacticsInventoryItemCard.prefab";

    [MenuItem("Tools/Tactics/Rebuild Inventory Menu Prefabs")]
    public static void RebuildPrefabs()
    {
        EnsureFolder(ResourcesFolder);
        EnsureFolder(UiFolder);
        BuildInventoryItemCardPrefab();
        BuildInventoryPanelPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt tactics inventory menu prefabs.");
    }

    private static void BuildInventoryPanelPrefab()
    {
        GameObject rootObject = CreateUiObject("Inventory Panel");
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-36f, -124f);
        rootRect.sizeDelta = new Vector2(1180f, 760f);

        Image background = rootObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.1f, 0.985f);

        Outline border = rootObject.AddComponent<Outline>();
        border.effectColor = new Color(0.88f, 0.84f, 0.72f, 1f);
        border.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = rootObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
        shadow.effectDistance = new Vector2(0f, -4f);

        Button dismissButton = rootObject.AddComponent<Button>();
        dismissButton.transition = Selectable.Transition.None;

        GameObject chrome = CreateUiObject("Chrome", rootObject.transform);
        RectTransform chromeRect = chrome.GetComponent<RectTransform>();
        chromeRect.anchorMin = Vector2.zero;
        chromeRect.anchorMax = Vector2.one;
        chromeRect.offsetMin = new Vector2(12f, 12f);
        chromeRect.offsetMax = new Vector2(-12f, -12f);
        Image chromeImage = chrome.AddComponent<Image>();
        chromeImage.color = new Color(1f, 1f, 1f, 0.035f);
        chromeImage.raycastTarget = false;

        Text titleText = CreateText("Title", rootObject.transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(32f, -42f);
        titleRect.offsetMax = new Vector2(-32f, -16f);
        titleText.text = "INVENTORY";

        Text subtitleText = CreateText("Subtitle", rootObject.transform, 34, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.offsetMin = new Vector2(32f, -98f);
        subtitleRect.offsetMax = new Vector2(-32f, -42f);
        subtitleText.text = "PACK";

        GameObject divider = CreateDivider(rootObject.transform, "Divider");
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.offsetMin = new Vector2(32f, -114f);
        dividerRect.offsetMax = new Vector2(-32f, -110f);

        GameObject rosterPanel = CreateUiObject("RosterPanel", rootObject.transform);
        RectTransform rosterRect = rosterPanel.GetComponent<RectTransform>();
        rosterRect.anchorMin = new Vector2(0f, 0f);
        rosterRect.anchorMax = new Vector2(0f, 1f);
        rosterRect.offsetMin = new Vector2(32f, 32f);
        rosterRect.offsetMax = new Vector2(248f, -130f);
        Image rosterImage = rosterPanel.AddComponent<Image>();
        rosterImage.color = new Color(1f, 1f, 1f, 0.025f);

        GameObject characterListRoot = CreateUiObject("CharacterListRoot", rosterPanel.transform);
        RectTransform characterListRect = characterListRoot.GetComponent<RectTransform>();
        characterListRect.anchorMin = Vector2.zero;
        characterListRect.anchorMax = Vector2.one;
        characterListRect.offsetMin = new Vector2(12f, 12f);
        characterListRect.offsetMax = new Vector2(-12f, -12f);
        VerticalLayoutGroup characterLayout = characterListRoot.AddComponent<VerticalLayoutGroup>();
        characterLayout.spacing = 10f;
        characterLayout.childForceExpandHeight = false;
        characterLayout.childForceExpandWidth = true;

        GameObject rightPanel = CreateUiObject("RightPanel", rootObject.transform);
        RectTransform rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = new Vector2(276f, 32f);
        rightRect.offsetMax = new Vector2(-32f, -130f);

        GameObject equipmentLabel = CreateUiObject("EquipmentLabel", rightPanel.transform);
        Text equipmentText = equipmentLabel.AddComponent<Text>();
        equipmentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        equipmentText.fontSize = 18;
        equipmentText.fontStyle = FontStyle.Bold;
        equipmentText.alignment = TextAnchor.MiddleLeft;
        equipmentText.color = new Color(0.76f, 0.69f, 0.5f, 1f);
        equipmentText.text = "EQUIPPED";
        RectTransform equipmentLabelRect = equipmentText.rectTransform;
        equipmentLabelRect.anchorMin = new Vector2(0f, 1f);
        equipmentLabelRect.anchorMax = new Vector2(1f, 1f);
        equipmentLabelRect.offsetMin = new Vector2(0f, -26f);
        equipmentLabelRect.offsetMax = new Vector2(0f, 0f);

        GameObject equipmentRoot = CreateUiObject("EquipmentRoot", rightPanel.transform);
        RectTransform equipmentRootRect = equipmentRoot.GetComponent<RectTransform>();
        equipmentRootRect.anchorMin = new Vector2(0f, 1f);
        equipmentRootRect.anchorMax = new Vector2(1f, 1f);
        equipmentRootRect.offsetMin = new Vector2(0f, -190f);
        equipmentRootRect.offsetMax = new Vector2(0f, -40f);
        HorizontalLayoutGroup equipmentLayout = equipmentRoot.AddComponent<HorizontalLayoutGroup>();
        equipmentLayout.spacing = 12f;
        equipmentLayout.childForceExpandHeight = false;
        equipmentLayout.childForceExpandWidth = false;

        GameObject inventoryLabel = CreateUiObject("InventoryLabel", rightPanel.transform);
        Text inventoryText = inventoryLabel.AddComponent<Text>();
        inventoryText.font = equipmentText.font;
        inventoryText.fontSize = 18;
        inventoryText.fontStyle = FontStyle.Bold;
        inventoryText.alignment = TextAnchor.MiddleLeft;
        inventoryText.color = new Color(0.76f, 0.69f, 0.5f, 1f);
        inventoryText.text = "BAG";
        RectTransform inventoryLabelRect = inventoryText.rectTransform;
        inventoryLabelRect.anchorMin = new Vector2(0f, 1f);
        inventoryLabelRect.anchorMax = new Vector2(1f, 1f);
        inventoryLabelRect.offsetMin = new Vector2(0f, -226f);
        inventoryLabelRect.offsetMax = new Vector2(0f, -198f);

        GameObject scrollRoot = CreateUiObject("InventoryScroll", rightPanel.transform);
        RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(0f, 0f);
        scrollRectTransform.offsetMax = new Vector2(0f, -238f);

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-18f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.025f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(136f, 136f);
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        GameObject scrollbarObject = CreateUiObject("Scrollbar", scrollRoot.transform);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.sizeDelta = new Vector2(12f, 0f);
        Image scrollbarTrack = scrollbarObject.AddComponent<Image>();
        scrollbarTrack.color = new Color(0.17f, 0.18f, 0.2f, 0.6f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarObject.transform);
        RectTransform slidingAreaRect = slidingArea.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);
        GameObject handleObject = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.pivot = new Vector2(0.5f, 0f);
        handleRect.sizeDelta = new Vector2(0f, 48f);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.76f, 0.69f, 0.5f, 1f);
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        Text emptyStateText = CreateText("EmptyState", viewport.transform, 16, FontStyle.Italic, TextAnchor.MiddleCenter);
        RectTransform emptyRect = emptyStateText.rectTransform;
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(24f, 24f);
        emptyRect.offsetMax = new Vector2(-24f, -24f);
        emptyStateText.text = "No items.";

        TacticsInventoryPanelPrefab bindings = rootObject.AddComponent<TacticsInventoryPanelPrefab>();
        bindings.Configure(
            rootRect,
            dismissButton,
            titleText,
            subtitleText,
            emptyStateText,
            characterListRect,
            equipmentRootRect,
            contentRect);

        SavePrefab(rootObject, InventoryPanelPrefabPath);
    }

    private static void BuildInventoryItemCardPrefab()
    {
        GameObject rootObject = CreateUiObject("Inventory Item Card");
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(136f, 136f);

        Image background = rootObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0f);

        Button button = rootObject.AddComponent<Button>();
        button.targetGraphic = background;

        TacticsAbilityTooltipTrigger tooltipTrigger = rootObject.AddComponent<TacticsAbilityTooltipTrigger>();

        GameObject equippedIndicator = CreateUiObject("EquippedIndicator", rootObject.transform);

        GameObject iconObject = CreateUiObject("Icon", rootObject.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.preserveAspect = false;
        iconImage.raycastTarget = false;

        Text stackCountText = CreateText("StackCount", rootObject.transform, 24, FontStyle.Bold, TextAnchor.LowerRight);
        RectTransform stackCountRect = stackCountText.rectTransform;
        stackCountRect.anchorMin = new Vector2(1f, 0f);
        stackCountRect.anchorMax = new Vector2(1f, 0f);
        stackCountRect.pivot = new Vector2(1f, 0f);
        stackCountRect.anchoredPosition = new Vector2(-8f, 8f);
        stackCountRect.sizeDelta = new Vector2(56f, 32f);
        stackCountText.color = Color.white;
        stackCountText.raycastTarget = false;
        Outline stackCountOutline = stackCountText.gameObject.AddComponent<Outline>();
        stackCountOutline.effectColor = Color.black;
        stackCountOutline.effectDistance = new Vector2(1f, -1f);
        stackCountText.gameObject.SetActive(false);

        TacticsInventoryItemCardView bindings = rootObject.AddComponent<TacticsInventoryItemCardView>();
        bindings.Configure(rootRect, button, iconImage, stackCountText, equippedIndicator, tooltipTrigger);

        SavePrefab(rootObject, InventoryItemCardPrefabPath);
    }

    private static GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static Text CreateText(string name, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.96f, 0.94f, 0.89f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static GameObject CreateDivider(Transform parent, string name)
    {
        GameObject divider = CreateUiObject(name, parent);
        Image image = divider.AddComponent<Image>();
        image.color = new Color(0.76f, 0.69f, 0.5f, 1f);
        return divider;
    }

    private static void SavePrefab(GameObject rootObject, string prefabPath)
    {
        string directory = Path.GetDirectoryName(prefabPath);
        if (!string.IsNullOrEmpty(directory))
        {
            EnsureFolder(directory.Replace('\\', '/'));
        }

        PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
        Object.DestroyImmediate(rootObject);
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
