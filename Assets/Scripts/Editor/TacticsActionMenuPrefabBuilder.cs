using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TacticsActionMenuPrefabBuilder
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string UiFolder = "Assets/Resources/UI";
    private const string ActionMenuPrefabPath = "Assets/Resources/UI/TacticsActionMenuPanel.prefab";
    private const string SpellMenuPrefabPath = "Assets/Resources/UI/TacticsSpellMenuPanel.prefab";
    private const string SpellCardPrefabPath = "Assets/Resources/UI/TacticsSpellCard.prefab";

    [MenuItem("Tools/Tactics/Rebuild Action Menu Prefabs")]
    public static void RebuildPrefabs()
    {
        EnsureFolder(ResourcesFolder);
        EnsureFolder(UiFolder);

        BuildActionMenuPrefab();
        BuildSpellCardPrefab();
        BuildSpellMenuPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt tactics action menu prefabs.");
    }

    private static void BuildActionMenuPrefab()
    {
        GameObject rootObject = CreateUiObject("Action Menu");
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(36f, -36f);
        rootRect.sizeDelta = new Vector2(320f, 260f);

        Image background = rootObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        Outline border = rootObject.AddComponent<Outline>();
        border.effectColor = new Color(0.88f, 0.84f, 0.72f, 1f);
        border.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rootObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text characterName = CreateText("Character Name", rootObject.transform, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        characterName.text = "CHARACTER";

        GameObject divider = CreateUiObject("Divider", rootObject.transform);
        LayoutElement dividerLayout = divider.AddComponent<LayoutElement>();
        dividerLayout.preferredHeight = 4f;
        dividerLayout.minHeight = 4f;
        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(0.76f, 0.69f, 0.5f, 1f);

        GameObject actionsRoot = CreateUiObject("Actions", rootObject.transform);
        VerticalLayoutGroup actionsLayout = actionsRoot.AddComponent<VerticalLayoutGroup>();
        actionsLayout.spacing = 8f;
        actionsLayout.childAlignment = TextAnchor.UpperLeft;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = false;
        actionsLayout.childForceExpandHeight = false;

        Button moveButton = CreateButton("Move Button", "MOVE", actionsRoot.transform);
        Button openChestButton = CreateButton("Open Chest Button", "OPEN CHEST", actionsRoot.transform);
        Button abilitiesButton = CreateButton("Abilities Button", "SPELLS", actionsRoot.transform);
        Button endTurnButton = CreateButton("End Turn Button", "END TURN", actionsRoot.transform);

        GameObject spacer = CreateUiObject("Footer Spacer", rootObject.transform);
        LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.preferredHeight = 12f;
        spacerLayout.minHeight = 12f;

        TacticsActionMenuPanelPrefab bindings = rootObject.AddComponent<TacticsActionMenuPanelPrefab>();
        bindings.Configure(
            rootRect,
            characterName,
            moveButton,
            openChestButton,
            abilitiesButton,
            endTurnButton);

        SavePrefab(rootObject, ActionMenuPrefabPath);
    }

    private static void BuildSpellMenuPrefab()
    {
        GameObject rootObject = CreateUiObject("Spell Menu");
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
        titleText.text = "SPELLS";

        Text subtitleText = CreateText("Subtitle", rootObject.transform, 34, FontStyle.Bold, TextAnchor.MiddleLeft);
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.offsetMin = new Vector2(32f, -98f);
        subtitleRect.offsetMax = new Vector2(-32f, -42f);
        subtitleText.text = "ABILITY LOADOUT";

        GameObject divider = CreateUiObject("Divider", rootObject.transform);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.offsetMin = new Vector2(32f, -114f);
        dividerRect.offsetMax = new Vector2(-32f, -110f);
        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(0.76f, 0.69f, 0.5f, 1f);

        GameObject scrollRoot = CreateUiObject("Scroll View", rootObject.transform);
        RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(32f, 32f);
        scrollRectTransform.offsetMax = new Vector2(-32f, -130f);

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
        grid.spacing = new Vector2(20f, 20f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(520f, 180f);
        grid.childAlignment = TextAnchor.UpperLeft;

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
        scrollbar.size = 0.25f;
        scrollbar.targetGraphic = scrollbarTrack;

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

        Text emptyStateText = CreateText("Empty State", viewport.transform, 16, FontStyle.Italic, TextAnchor.MiddleCenter);
        RectTransform emptyStateRect = emptyStateText.rectTransform;
        emptyStateRect.anchorMin = Vector2.zero;
        emptyStateRect.anchorMax = Vector2.one;
        emptyStateRect.offsetMin = new Vector2(24f, 24f);
        emptyStateRect.offsetMax = new Vector2(-24f, -24f);
        emptyStateText.text = "No spells available.";

        TacticsSpellMenuPanelPrefab bindings = rootObject.AddComponent<TacticsSpellMenuPanelPrefab>();
        bindings.Configure(
            rootRect,
            dismissButton,
            titleText,
            subtitleText,
            emptyStateText,
            contentRect);

        SavePrefab(rootObject, SpellMenuPrefabPath);
    }

    private static void BuildSpellCardPrefab()
    {
        GameObject rootObject = CreateUiObject("Spell Card");
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(540f, 180f);

        Image background = rootObject.AddComponent<Image>();
        background.color = new Color(0.11f, 0.13f, 0.16f, 0.98f);

        Button button = rootObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.76f, 0.69f, 0.5f, 1f);
        colors.disabledColor = new Color(0.12f, 0.13f, 0.15f, 0.72f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = background;

        Outline outline = rootObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.84f, 0.72f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        TacticsAbilityTooltipTrigger tooltipTrigger = rootObject.AddComponent<TacticsAbilityTooltipTrigger>();

        VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject selectedIndicator = CreateUiObject("Selected Indicator", rootObject.transform);
        RectTransform selectedRect = selectedIndicator.GetComponent<RectTransform>();
        selectedRect.anchorMin = Vector2.zero;
        selectedRect.anchorMax = Vector2.one;
        selectedRect.offsetMin = new Vector2(4f, 4f);
        selectedRect.offsetMax = new Vector2(-4f, -4f);
        Image selectedImage = selectedIndicator.AddComponent<Image>();
        selectedImage.color = new Color(1f, 1f, 1f, 0f);
        selectedImage.raycastTarget = false;
        Outline selectedOutline = selectedIndicator.AddComponent<Outline>();
        selectedOutline.effectColor = new Color(0.76f, 0.69f, 0.5f, 1f);
        selectedOutline.effectDistance = new Vector2(2f, -2f);
        selectedIndicator.SetActive(false);
        selectedIndicator.transform.SetAsFirstSibling();

        GameObject headerRow = CreateUiObject("Header Row", rootObject.transform);
        HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;
        LayoutElement headerLayoutElement = headerRow.AddComponent<LayoutElement>();
        headerLayoutElement.preferredHeight = 28f;

        Text nameText = CreateText("Name", headerRow.transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;
        nameText.text = "Spell Name";

        Text headerSummaryText = CreateText("Header Summary", headerRow.transform, 18, FontStyle.Bold, TextAnchor.MiddleRight);
        LayoutElement summaryLayout = headerSummaryText.gameObject.AddComponent<LayoutElement>();
        summaryLayout.flexibleWidth = 1f;
        headerSummaryText.text = "12 DMG";

        CreateDivider(rootObject.transform, "Top Divider");
        Text metaText = CreateText("Meta", rootObject.transform, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
        metaText.text = "5 MP    |    3 Tiles";

        GameObject middleDivider = CreateDivider(rootObject.transform, "Middle Divider");
        Text descriptionText = CreateText("Description", rootObject.transform, 15, FontStyle.Normal, TextAnchor.UpperLeft);
        descriptionText.text = "Description";
        descriptionText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        GameObject generatedGroup = CreateUiObject("Generated Group", rootObject.transform);
        VerticalLayoutGroup generatedLayout = generatedGroup.AddComponent<VerticalLayoutGroup>();
        generatedLayout.spacing = 4f;
        generatedLayout.childControlHeight = true;
        generatedLayout.childControlWidth = true;
        generatedLayout.childForceExpandHeight = false;
        generatedLayout.childForceExpandWidth = true;
        Text generatedText = CreateText("Generated", generatedGroup.transform, 12, FontStyle.Italic, TextAnchor.UpperLeft);
        generatedText.text = "Generated details";
        generatedGroup.SetActive(false);

        GameObject statusGroup = CreateUiObject("Status Group", rootObject.transform);
        Text statusText = CreateText("Status", statusGroup.transform, 11, FontStyle.Bold, TextAnchor.LowerLeft);
        statusText.text = "Unavailable";
        statusGroup.SetActive(false);

        TacticsSpellCardView bindings = rootObject.AddComponent<TacticsSpellCardView>();
        bindings.Configure(
            rootRect,
            button,
            nameText,
            headerSummaryText,
            metaText,
            descriptionText,
            generatedGroup,
            middleDivider,
            generatedText,
            statusGroup,
            statusText,
            selectedIndicator,
            tooltipTrigger);

        SavePrefab(rootObject, SpellCardPrefabPath);
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

    private static Text CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = new Color(0.96f, 0.94f, 0.89f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(0f, fontSize + 10f);
        return text;
    }

    private static Button CreateButton(string name, string label, Transform parent)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 42f;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.17f, 0.18f, 0.2f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.27f, 0.28f, 0.31f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.76f, 0.69f, 0.5f, 1f);
        colors.disabledColor = new Color(image.color.r, image.color.g, image.color.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.targetGraphic = image;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.84f, 0.72f, 1f);
        outline.effectDistance = new Vector2(1f, -1f);

        Text text = CreateText("Label", buttonObject.transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        text.text = label;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);
        return button;
    }

    private static GameObject CreateDivider(Transform parent, string name)
    {
        GameObject divider = CreateUiObject(name, parent);
        LayoutElement layout = divider.AddComponent<LayoutElement>();
        layout.preferredHeight = 1f;
        layout.minHeight = 1f;
        Image image = divider.AddComponent<Image>();
        image.color = new Color(0.76f, 0.69f, 0.5f, 0.34f);
        image.raycastTarget = false;
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
