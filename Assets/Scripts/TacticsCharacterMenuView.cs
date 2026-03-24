using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsCharacterMenuView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color panelColor = new Color(0.07f, 0.08f, 0.1f, 0.97f);
    [SerializeField] private Color edgeColor = new Color(0.86f, 0.81f, 0.68f, 1f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.28f);
    [SerializeField] private Color buttonColor = new Color(0.15f, 0.17f, 0.2f, 1f);
    [SerializeField] private Color buttonHighlightColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.72f, 0.74f, 0.79f, 1f);
    [SerializeField] private Color accentColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.44f, 0.49f, 0.56f, 1f);

    private readonly Dictionary<string, CharacterButtonWidgets> characterButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TacticsAbilityScalingStat, StatRowWidgets> primaryStatRows = new();
    private readonly List<Text> derivedValueLabels = new();

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private GameObject characterListRoot;
    private Text subtitleText;
    private Text progressionText;
    private Text resourcesText;
    private Text statusText;
    private Font sharedFont;
    private TacticsCharacterController selectedCharacter;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;

    public event Action<TacticsCharacterController, TacticsAbilityScalingStat> AttributePointRequested;

    public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        EnsureBuilt();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
    }

    private void OnDestroy()
    {
    }

    private void Update()
    {
        if (!IsPanelVisible)
        {
            return;
        }

        if (selectedCharacter == null || !selectedCharacter.isActiveAndEnabled || !selectedCharacter.IsPlayerControlled)
        {
            RefreshCharacterList();
            return;
        }

        RefreshSelectedCharacterDetails();
    }

    public void AssignDependencies(TacticsCharacterProgressionService service, TacticsCoopSessionCoordinator coordinator)
    {
        coopSessionCoordinator = coordinator;
        RefreshCharacterList();
    }

    public void TogglePanelVisibility()
    {
        SetPanelVisible(!IsPanelVisible);
    }

    public void SetPanelVisible(bool visible)
    {
        EnsureBuilt();
        panelRoot.SetActive(visible);
        if (visible)
        {
            RefreshCharacterList();
        }
    }

    public void RefreshCharacterList()
    {
        EnsureBuilt();
        RebuildCharacterButtons();
        RefreshSelectedCharacter();
        RefreshSelectedCharacterDetails();
    }

    private void EnsureBuilt()
    {
        if (panelRoot != null)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 4998;

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

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        panelRoot = CreateUiObject("CharacterMenuPanel", transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-36f, -124f);
        panelRect.sizeDelta = new Vector2(1100f, 760f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = edgeColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);

        Shadow panelShadow = panelRoot.AddComponent<Shadow>();
        panelShadow.effectColor = shadowColor;
        panelShadow.effectDistance = new Vector2(0f, -6f);

        BuildHeader();
        BuildCharacterList();
        BuildStats();
    }

    private void BuildHeader()
    {
        Text titleText = CreateText("Title", panelRoot.transform, 18, FontStyle.Bold, accentColor, TextAnchor.UpperLeft);
        titleText.text = "CHARACTER";
        StretchTop(titleText.rectTransform, -30f, 0f);

        subtitleText = CreateText("Subtitle", panelRoot.transform, 38, FontStyle.Bold, primaryTextColor, TextAnchor.UpperLeft);
        StretchTop(subtitleText.rectTransform, -72f, -18f);

        progressionText = CreateText("Progression", panelRoot.transform, 18, FontStyle.Bold, secondaryTextColor, TextAnchor.UpperLeft);
        StretchTop(progressionText.rectTransform, -106f, -74f);

        resourcesText = CreateText("Resources", panelRoot.transform, 16, FontStyle.Normal, secondaryTextColor, TextAnchor.UpperLeft);
        StretchTop(resourcesText.rectTransform, -136f, -104f);

        statusText = CreateText("Status", panelRoot.transform, 14, FontStyle.Bold, accentColor, TextAnchor.UpperRight);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(28f, -30f);
        statusRect.offsetMax = new Vector2(-28f, 0f);
    }

    private void BuildCharacterList()
    {
        GameObject listPanel = CreateUiObject("CharacterSelector", panelRoot.transform);
        RectTransform listPanelRect = listPanel.GetComponent<RectTransform>();
        listPanelRect.anchorMin = new Vector2(0f, 0f);
        listPanelRect.anchorMax = new Vector2(0f, 1f);
        listPanelRect.pivot = new Vector2(0f, 1f);
        listPanelRect.sizeDelta = new Vector2(230f, -170f);
        listPanelRect.anchoredPosition = new Vector2(28f, -156f);

        Image listImage = listPanel.AddComponent<Image>();
        listImage.color = new Color(1f, 1f, 1f, 0.03f);

        characterListRoot = CreateUiObject("CharacterButtonList", listPanel.transform);
        RectTransform listRect = characterListRoot.GetComponent<RectTransform>();
        listRect.anchorMin = Vector2.zero;
        listRect.anchorMax = Vector2.one;
        listRect.offsetMin = new Vector2(10f, 10f);
        listRect.offsetMax = new Vector2(-10f, -10f);

        VerticalLayoutGroup layout = characterListRoot.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
    }

    private void BuildStats()
    {
        GameObject statRoot = CreateUiObject("StatsRoot", panelRoot.transform);
        RectTransform statRect = statRoot.GetComponent<RectTransform>();
        statRect.anchorMin = new Vector2(0f, 0f);
        statRect.anchorMax = new Vector2(1f, 1f);
        statRect.offsetMin = new Vector2(292f, 24f);
        statRect.offsetMax = new Vector2(-24f, -156f);

        GameObject primaryColumn = CreateColumn(statRoot.transform, "PRIMARY", new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        CreatePrimaryStatRow(primaryColumn.transform, "STAMINA", TacticsAbilityScalingStat.Stamina);
        CreatePrimaryStatRow(primaryColumn.transform, "STRENGTH", TacticsAbilityScalingStat.Strength);
        CreatePrimaryStatRow(primaryColumn.transform, "AGILITY", TacticsAbilityScalingStat.Agility);
        CreatePrimaryStatRow(primaryColumn.transform, "WISDOM", TacticsAbilityScalingStat.Wisdom);
        CreatePrimaryStatRow(primaryColumn.transform, "INTELLECT", TacticsAbilityScalingStat.Intelligence);
        CreateDerivedRow(primaryColumn.transform, "MOVE");
        CreateDerivedRow(primaryColumn.transform, "JUMP");

        GameObject derivedColumn = CreateColumn(statRoot.transform, "DERIVED", new Vector2(0.5f, 0f), new Vector2(1f, 1f));
        CreateDerivedRow(derivedColumn.transform, "MAX HP");
        CreateDerivedRow(derivedColumn.transform, "MAX ST");
        CreateDerivedRow(derivedColumn.transform, "MAX MP");
        CreateDerivedRow(derivedColumn.transform, "MELEE");
        CreateDerivedRow(derivedColumn.transform, "MAGIC");
        CreateDerivedRow(derivedColumn.transform, "MELEE CRIT");
        CreateDerivedRow(derivedColumn.transform, "MAGIC CRIT");
    }

    private GameObject CreateColumn(Transform parent, string title, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject column = CreateUiObject($"{title}Column", parent);
        RectTransform rect = column.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(anchorMin.x > 0f ? 12f : 0f, 0f);
        rect.offsetMax = new Vector2(anchorMax.x < 1f ? -12f : 0f, 0f);

        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Text header = CreateText($"{title}Header", column.transform, 17, FontStyle.Bold, accentColor, TextAnchor.MiddleLeft);
        header.text = title;
        return column;
    }

    private void CreatePrimaryStatRow(Transform parent, string label, TacticsAbilityScalingStat stat)
    {
        GameObject row = CreateUiObject($"{label}Row", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 38f;

        Text labelText = CreateText($"{label}Label", row.transform, 16, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleLeft);
        labelText.text = label;
        AddFixedWidth(labelText.gameObject, 120f);

        Text valueText = CreateText($"{label}Value", row.transform, 16, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        AddFixedWidth(valueText.gameObject, 50f);

        Text bonusText = CreateText($"{label}Bonus", row.transform, 14, FontStyle.Bold, accentColor, TextAnchor.MiddleCenter);
        AddFixedWidth(bonusText.gameObject, 56f);

        Button increaseButton = CreateSlimButton($"{label}Increase", row.transform, "+");
        increaseButton.onClick.AddListener(() =>
        {
            if (selectedCharacter != null)
            {
                AttributePointRequested?.Invoke(selectedCharacter, stat);
            }
        });

        primaryStatRows[stat] = new StatRowWidgets(valueText, bonusText, increaseButton);
    }

    private void CreateDerivedRow(Transform parent, string label)
    {
        GameObject row = CreateUiObject($"{label}Row", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 32f;

        Text labelText = CreateText($"{label}Label", row.transform, 15, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleLeft);
        labelText.text = label;
        AddFixedWidth(labelText.gameObject, 140f);

        Text valueText = CreateText($"{label}Value", row.transform, 16, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleLeft);
        valueText.text = "--";
        derivedValueLabels.Add(valueText);
    }

    private void RebuildCharacterButtons()
    {
        characterButtons.Clear();
        for (int i = characterListRoot.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(characterListRoot.transform.GetChild(i).gameObject);
        }

        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        Array.Sort(characters, CompareCharacters);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null || !character.isActiveAndEnabled || !character.IsPlayerControlled)
            {
                continue;
            }

            Button button = CreateSlimButton(character.DisplayName, characterListRoot.transform, character.DisplayName.ToUpperInvariant());
            button.onClick.AddListener(() =>
            {
                selectedCharacter = character;
                RefreshSelectedCharacterDetails();
            });

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                bool isOwned = coopSessionCoordinator == null || coopSessionCoordinator.CanLocalPlayerControlCharacter(character);
                label.text = $"{character.DisplayName.ToUpperInvariant()}\n<size=12>{(isOwned ? "YOU" : "ALLY")}  LV {character.CurrentLevel:00}</size>";
                label.alignment = TextAnchor.MiddleLeft;
            }

            characterButtons[character.RuntimeCharacterId] = new CharacterButtonWidgets(button, button.targetGraphic as Image);
        }
    }

    private void RefreshSelectedCharacter()
    {
        if (selectedCharacter != null && selectedCharacter.isActiveAndEnabled && selectedCharacter.IsPlayerControlled)
        {
            return;
        }

        selectedCharacter = null;
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        Array.Sort(characters, CompareCharacters);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character != null && character.isActiveAndEnabled && character.IsPlayerControlled)
            {
                selectedCharacter = character;
                return;
            }
        }
    }

    private void RefreshSelectedCharacterDetails()
    {
        UpdateCharacterButtonStates();

        if (selectedCharacter == null)
        {
            subtitleText.text = "NO ACTIVE PARTY";
            progressionText.text = "No player-controlled characters are available.";
            resourcesText.text = string.Empty;
            statusText.text = string.Empty;
            SetDerivedValues(Array.Empty<string>());
            return;
        }

        subtitleText.text = selectedCharacter.DisplayName.ToUpperInvariant();
        progressionText.text = $"LV {selectedCharacter.CurrentLevel:00}   EXP {selectedCharacter.CurrentExperience}/{selectedCharacter.ExperienceToNextLevel}   AP {selectedCharacter.UnspentAttributePoints:00}";
        resourcesText.text = $"HP {selectedCharacter.CurrentHitPoints}/{selectedCharacter.MaxHitPoints}   ST {selectedCharacter.CurrentStamina}/{selectedCharacter.MaxStamina}   MP {selectedCharacter.CurrentMana}/{selectedCharacter.MaxMana}";
        statusText.text = CanEditSelectedCharacter() ? "ALLOCATE POINTS" : "ALLY DATA";

        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Stamina, selectedCharacter.BaseStats.primaryStats.stamina, selectedCharacter.Progression.GetAllocatedValue(TacticsAbilityScalingStat.Stamina));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Strength, selectedCharacter.BaseStats.primaryStats.strength, selectedCharacter.Progression.GetAllocatedValue(TacticsAbilityScalingStat.Strength));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Agility, selectedCharacter.BaseStats.primaryStats.agility, selectedCharacter.Progression.GetAllocatedValue(TacticsAbilityScalingStat.Agility));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Wisdom, selectedCharacter.BaseStats.primaryStats.wisdom, selectedCharacter.Progression.GetAllocatedValue(TacticsAbilityScalingStat.Wisdom));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Intelligence, selectedCharacter.BaseStats.primaryStats.intelligence, selectedCharacter.Progression.GetAllocatedValue(TacticsAbilityScalingStat.Intelligence));

        SetDerivedValues(new[]
        {
            selectedCharacter.MoveRange.ToString(),
            selectedCharacter.JumpHeight.ToString(),
            selectedCharacter.MaxHitPoints.ToString(),
            selectedCharacter.MaxStamina.ToString(),
            selectedCharacter.MaxMana.ToString(),
            $"{selectedCharacter.BaseMeleeDamageMin}-{selectedCharacter.BaseMeleeDamageMax}",
            $"{selectedCharacter.BaseMagicDamageMin}-{selectedCharacter.BaseMagicDamageMax}",
            $"{selectedCharacter.MeleeCriticalHitChance * 100f:0}%",
            $"{selectedCharacter.MagicCriticalHitChance * 100f:0}%"
        });
    }

    private void ApplyPrimaryStatRow(TacticsAbilityScalingStat stat, int value, int allocatedBonus)
    {
        if (!primaryStatRows.TryGetValue(stat, out StatRowWidgets widgets))
        {
            return;
        }

        widgets.Value.text = value.ToString();
        widgets.Bonus.text = allocatedBonus > 0 ? $"+{allocatedBonus}" : string.Empty;
        widgets.IncreaseButton.interactable = CanEditSelectedCharacter() && selectedCharacter.UnspentAttributePoints > 0;
        Image buttonImage = widgets.IncreaseButton.targetGraphic as Image;
        if (buttonImage != null)
        {
            buttonImage.color = widgets.IncreaseButton.interactable ? buttonHighlightColor : lockedColor;
        }
    }

    private void SetDerivedValues(IReadOnlyList<string> values)
    {
        for (int i = 0; i < derivedValueLabels.Count; i++)
        {
            derivedValueLabels[i].text = values != null && i < values.Count ? values[i] : "--";
        }
    }

    private void UpdateCharacterButtonStates()
    {
        foreach (KeyValuePair<string, CharacterButtonWidgets> pair in characterButtons)
        {
            bool isSelected = selectedCharacter != null &&
                              string.Equals(pair.Key, selectedCharacter.RuntimeCharacterId, StringComparison.OrdinalIgnoreCase);
            if (pair.Value.Background != null)
            {
                pair.Value.Background.color = isSelected ? buttonHighlightColor : buttonColor;
            }
        }
    }

    private bool CanEditSelectedCharacter()
    {
        return selectedCharacter != null &&
               (coopSessionCoordinator == null || coopSessionCoordinator.CanLocalPlayerControlCharacter(selectedCharacter));
    }

    private Button CreateSlimButton(string objectName, Transform parent, string labelText)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = edgeColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Text label = CreateText("Label", buttonObject.transform, 14, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        label.text = labelText;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 8f);
        labelRect.offsetMax = new Vector2(-10f, -8f);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 52f;
        return button;
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
        return text;
    }

    private static void StretchTop(RectTransform rect, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(28f, bottom);
        rect.offsetMax = new Vector2(-28f, top);
    }

    private static void AddFixedWidth(GameObject gameObject, float width)
    {
        LayoutElement layout = gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static int CompareCharacters(TacticsCharacterController left, TacticsCharacterController right)
    {
        return string.Compare(left?.RuntimeCharacterId, right?.RuntimeCharacterId, StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct CharacterButtonWidgets
    {
        public CharacterButtonWidgets(Button button, Image background)
        {
            Button = button;
            Background = background;
        }

        public Button Button { get; }
        public Image Background { get; }
    }

    private readonly struct StatRowWidgets
    {
        public StatRowWidgets(Text value, Text bonus, Button increaseButton)
        {
            Value = value;
            Bonus = bonus;
            IncreaseButton = increaseButton;
        }

        public Text Value { get; }
        public Text Bonus { get; }
        public Button IncreaseButton { get; }
    }
}
