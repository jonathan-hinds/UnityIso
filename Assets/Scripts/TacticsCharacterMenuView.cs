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
    [SerializeField] private Color positiveDeltaColor = new Color(0.78f, 0.9f, 0.67f, 1f);

    private readonly Dictionary<string, CharacterButtonWidgets> characterButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TacticsCharacterProgressionPlan> progressionPlansByCharacterId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TacticsAbilityScalingStat, StatRowWidgets> primaryStatRows = new();
    private readonly List<DerivedRowWidgets> derivedRows = new();

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private GameObject characterListRoot;
    private Text subtitleText;
    private Text progressionText;
    private Text resourcesText;
    private Text statusText;
    private Text primaryEditHeaderLabel;
    private Button saveButton;
    private Text saveButtonLabel;
    private Font sharedFont;
    private TacticsCharacterController selectedCharacter;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;

    public event Action<TacticsCharacterController, TacticsCharacterProgressionSnapshot> ProgressionCommitRequested;

    public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        EnsureBuilt();
        SetPanelVisible(false);
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
            return;
        }

        progressionPlansByCharacterId.Clear();
    }

    public void RefreshCharacterList()
    {
        EnsureBuilt();
        RebuildCharacterButtons();
        RefreshSelectedCharacter();
        RefreshSelectedCharacterDetails();
    }

    public void MarkProgressionCommitted(TacticsCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        TacticsCharacterProgressionPlan plan = GetOrCreatePlan(character);
        plan.SyncCommittedSnapshot(character.Progression, preservePendingChanges: false);
        RefreshCharacterList();
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
        panelRect.sizeDelta = new Vector2(1180f, 760f);

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
        statusRect.offsetMax = new Vector2(-190f, 0f);

        saveButton = CreateSlimButton("SaveProgression", panelRoot.transform, "SAVE");
        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(1f, 1f);
        saveRect.anchorMax = new Vector2(1f, 1f);
        saveRect.pivot = new Vector2(1f, 1f);
        saveRect.anchoredPosition = new Vector2(-28f, -32f);
        saveRect.sizeDelta = new Vector2(138f, 44f);
        saveButton.onClick.AddListener(HandleSaveButtonClicked);
        saveButtonLabel = saveButton.GetComponentInChildren<Text>();
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
        CreateComparisonHeader(primaryColumn.transform, includeAdjustmentButtons: true);
        CreatePrimaryStatRow(primaryColumn.transform, "STAMINA", TacticsAbilityScalingStat.Stamina);
        CreatePrimaryStatRow(primaryColumn.transform, "STRENGTH", TacticsAbilityScalingStat.Strength);
        CreatePrimaryStatRow(primaryColumn.transform, "AGILITY", TacticsAbilityScalingStat.Agility);
        CreatePrimaryStatRow(primaryColumn.transform, "WISDOM", TacticsAbilityScalingStat.Wisdom);
        CreatePrimaryStatRow(primaryColumn.transform, "INTELLECT", TacticsAbilityScalingStat.Intelligence);
        CreateDerivedRow(primaryColumn.transform, "MOVE");
        CreateDerivedRow(primaryColumn.transform, "JUMP");

        GameObject derivedColumn = CreateColumn(statRoot.transform, "DERIVED", new Vector2(0.5f, 0f), new Vector2(1f, 1f));
        CreateComparisonHeader(derivedColumn.transform, includeAdjustmentButtons: false);
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

    private void CreateComparisonHeader(Transform parent, bool includeAdjustmentButtons)
    {
        GameObject row = CreateUiObject("ComparisonHeader", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 26f;

        Text spacer = CreateText("Spacer", row.transform, 12, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleLeft);
        spacer.text = string.Empty;
        AddFixedWidth(spacer.gameObject, 120f);

        Text currentText = CreateText("CurrentHeader", row.transform, 12, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleCenter);
        currentText.text = "CURRENT";
        AddFixedWidth(currentText.gameObject, 66f);

        Text previewText = CreateText("PreviewHeader", row.transform, 12, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleCenter);
        previewText.text = "PREVIEW";
        AddFixedWidth(previewText.gameObject, 66f);

        Text deltaText = CreateText("DeltaHeader", row.transform, 12, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleCenter);
        deltaText.text = "SHIFT";
        AddFixedWidth(deltaText.gameObject, 62f);

        if (!includeAdjustmentButtons)
        {
            return;
        }

        primaryEditHeaderLabel = CreateText("SpendHeader", row.transform, 12, FontStyle.Bold, secondaryTextColor, TextAnchor.MiddleCenter);
        primaryEditHeaderLabel.text = "EDIT";
        AddFixedWidth(primaryEditHeaderLabel.gameObject, 84f);
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

        Text currentText = CreateText($"{label}Current", row.transform, 16, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        AddFixedWidth(currentText.gameObject, 66f);

        Text previewText = CreateText($"{label}Preview", row.transform, 16, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        AddFixedWidth(previewText.gameObject, 66f);

        Text deltaText = CreateText($"{label}Delta", row.transform, 14, FontStyle.Bold, accentColor, TextAnchor.MiddleCenter);
        AddFixedWidth(deltaText.gameObject, 62f);

        Button decreaseButton = CreateAdjustmentButton($"{label}Decrease", row.transform, "<");
        decreaseButton.onClick.AddListener(() => HandleDecreaseRequested(stat));

        Button increaseButton = CreateAdjustmentButton($"{label}Increase", row.transform, ">");
        increaseButton.onClick.AddListener(() => HandleIncreaseRequested(stat));

        primaryStatRows[stat] = new StatRowWidgets(currentText, previewText, deltaText, decreaseButton, increaseButton);
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
        AddFixedWidth(labelText.gameObject, 120f);

        Text currentText = CreateText($"{label}Current", row.transform, 15, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        AddFixedWidth(currentText.gameObject, 66f);

        Text previewText = CreateText($"{label}Preview", row.transform, 15, FontStyle.Bold, primaryTextColor, TextAnchor.MiddleCenter);
        AddFixedWidth(previewText.gameObject, 66f);

        Text deltaText = CreateText($"{label}Delta", row.transform, 14, FontStyle.Bold, accentColor, TextAnchor.MiddleCenter);
        AddFixedWidth(deltaText.gameObject, 62f);

        derivedRows.Add(new DerivedRowWidgets(label, currentText, previewText, deltaText));
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
                TacticsCharacterProgressionPlan plan = GetOrCreatePlan(character);
                string pendingSuffix = plan.HasPendingChanges ? "\n<size=12>UNSAVED</size>" : string.Empty;
                label.text = $"{character.DisplayName.ToUpperInvariant()}\n<size=12>{(isOwned ? "YOU" : "ALLY")}  LV {character.CurrentLevel:00}</size>{pendingSuffix}";
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
            SetEmptyRows();
            SetEditControlsVisible(false);
            UpdateSaveButtonState(null);
            return;
        }

        TacticsCharacterProgressionPlan plan = GetOrCreatePlan(selectedCharacter);
        plan.SyncCommittedSnapshot(selectedCharacter.Progression, preservePendingChanges: true);

        TacticsCharacterStats committedStats = selectedCharacter.GetStatsForProgression(plan.CommittedSnapshot);
        TacticsCharacterStats previewStats = selectedCharacter.GetStatsForProgression(plan.WorkingSnapshot);
        TacticsCharacterDerivedStats committedDerived = committedStats.CalculateDerivedStats();
        TacticsCharacterDerivedStats previewDerived = previewStats.CalculateDerivedStats();

        subtitleText.text = selectedCharacter.DisplayName.ToUpperInvariant();
        progressionText.text =
            $"LV {selectedCharacter.CurrentLevel:00}   EXP {selectedCharacter.CurrentExperience}/{selectedCharacter.ExperienceToNextLevel}   AP {plan.CommittedSnapshot.UnspentAttributePoints:00} -> {plan.WorkingSnapshot.UnspentAttributePoints:00}";
        resourcesText.text =
            $"HP {selectedCharacter.CurrentHitPoints}/{selectedCharacter.MaxHitPoints}   ST {selectedCharacter.CurrentStamina}/{selectedCharacter.MaxStamina}   MP {selectedCharacter.CurrentMana}/{selectedCharacter.MaxMana}";
        statusText.text = BuildStatusText(plan);
        SetEditControlsVisible(ShouldShowEditControls(plan));

        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Stamina, committedStats.primaryStats.stamina, previewStats.primaryStats.stamina, plan.CanDecrease(TacticsAbilityScalingStat.Stamina), plan.CanIncrease(TacticsAbilityScalingStat.Stamina));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Strength, committedStats.primaryStats.strength, previewStats.primaryStats.strength, plan.CanDecrease(TacticsAbilityScalingStat.Strength), plan.CanIncrease(TacticsAbilityScalingStat.Strength));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Agility, committedStats.primaryStats.agility, previewStats.primaryStats.agility, plan.CanDecrease(TacticsAbilityScalingStat.Agility), plan.CanIncrease(TacticsAbilityScalingStat.Agility));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Wisdom, committedStats.primaryStats.wisdom, previewStats.primaryStats.wisdom, plan.CanDecrease(TacticsAbilityScalingStat.Wisdom), plan.CanIncrease(TacticsAbilityScalingStat.Wisdom));
        ApplyPrimaryStatRow(TacticsAbilityScalingStat.Intelligence, committedStats.primaryStats.intelligence, previewStats.primaryStats.intelligence, plan.CanDecrease(TacticsAbilityScalingStat.Intelligence), plan.CanIncrease(TacticsAbilityScalingStat.Intelligence));

        SetDerivedValue("MOVE", committedStats.MoveRange.ToString(), previewStats.MoveRange.ToString());
        SetDerivedValue("JUMP", committedStats.JumpHeight.ToString(), previewStats.JumpHeight.ToString());
        SetDerivedValue("MAX HP", committedDerived.maxHitPoints.ToString(), previewDerived.maxHitPoints.ToString());
        SetDerivedValue("MAX ST", committedDerived.maxStamina.ToString(), previewDerived.maxStamina.ToString());
        SetDerivedValue("MAX MP", committedDerived.maxMana.ToString(), previewDerived.maxMana.ToString());
        SetDerivedValue("MELEE", $"{committedDerived.baseMeleeDamageMin}-{committedDerived.baseMeleeDamageMax}", $"{previewDerived.baseMeleeDamageMin}-{previewDerived.baseMeleeDamageMax}");
        SetDerivedValue("MAGIC", $"{committedDerived.baseMagicDamageMin}-{committedDerived.baseMagicDamageMax}", $"{previewDerived.baseMagicDamageMin}-{previewDerived.baseMagicDamageMax}");
        SetDerivedValue("MELEE CRIT", $"{committedDerived.meleeCriticalHitChance * 100f:0}%", $"{previewDerived.meleeCriticalHitChance * 100f:0}%");
        SetDerivedValue("MAGIC CRIT", $"{committedDerived.magicCriticalHitChance * 100f:0}%", $"{previewDerived.magicCriticalHitChance * 100f:0}%");

        UpdateSaveButtonState(plan);
    }

    private void HandleIncreaseRequested(TacticsAbilityScalingStat stat)
    {
        if (!CanEditSelectedCharacter())
        {
            return;
        }

        TacticsCharacterProgressionPlan plan = GetOrCreatePlan(selectedCharacter);
        if (!plan.TryIncrease(stat))
        {
            return;
        }

        RefreshCharacterList();
    }

    private void HandleDecreaseRequested(TacticsAbilityScalingStat stat)
    {
        if (!CanEditSelectedCharacter())
        {
            return;
        }

        TacticsCharacterProgressionPlan plan = GetOrCreatePlan(selectedCharacter);
        if (!plan.TryDecrease(stat))
        {
            return;
        }

        RefreshCharacterList();
    }

    private void HandleSaveButtonClicked()
    {
        if (!CanEditSelectedCharacter() || selectedCharacter == null)
        {
            return;
        }

        TacticsCharacterProgressionPlan plan = GetOrCreatePlan(selectedCharacter);
        if (!plan.HasPendingChanges)
        {
            return;
        }

        ProgressionCommitRequested?.Invoke(selectedCharacter, plan.WorkingSnapshot);
    }

    private void ApplyPrimaryStatRow(TacticsAbilityScalingStat stat, int currentValue, int previewValue, bool canDecrease, bool canIncrease)
    {
        if (!primaryStatRows.TryGetValue(stat, out StatRowWidgets widgets))
        {
            return;
        }

        widgets.CurrentValue.text = currentValue.ToString();
        widgets.PreviewValue.text = previewValue.ToString();
        ApplyDeltaText(widgets.DeltaValue, previewValue - currentValue);
        SetButtonState(widgets.DecreaseButton, CanEditSelectedCharacter() && canDecrease);
        SetButtonState(widgets.IncreaseButton, CanEditSelectedCharacter() && canIncrease);
    }

    private void SetDerivedValue(string label, string currentValue, string previewValue)
    {
        for (int i = 0; i < derivedRows.Count; i++)
        {
            if (!string.Equals(derivedRows[i].Label, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            derivedRows[i].CurrentValue.text = currentValue;
            derivedRows[i].PreviewValue.text = previewValue;
            ApplyDerivedDeltaText(derivedRows[i].DeltaValue, currentValue, previewValue);
            return;
        }
    }

    private void SetEmptyRows()
    {
        foreach (KeyValuePair<TacticsAbilityScalingStat, StatRowWidgets> pair in primaryStatRows)
        {
            pair.Value.CurrentValue.text = "--";
            pair.Value.PreviewValue.text = "--";
            pair.Value.DeltaValue.text = string.Empty;
            SetButtonState(pair.Value.DecreaseButton, false);
            SetButtonState(pair.Value.IncreaseButton, false);
        }

        for (int i = 0; i < derivedRows.Count; i++)
        {
            derivedRows[i].CurrentValue.text = "--";
            derivedRows[i].PreviewValue.text = "--";
            derivedRows[i].DeltaValue.text = string.Empty;
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

    private void UpdateSaveButtonState(TacticsCharacterProgressionPlan plan)
    {
        bool showEditControls = ShouldShowEditControls(plan);
        if (saveButton != null)
        {
            saveButton.gameObject.SetActive(showEditControls);
        }

        bool canSave = showEditControls && plan != null && CanEditSelectedCharacter() && plan.HasPendingChanges;
        saveButton.interactable = canSave;
        Image buttonImage = saveButton.targetGraphic as Image;
        if (buttonImage != null)
        {
            buttonImage.color = canSave ? buttonHighlightColor : lockedColor;
        }

        if (saveButtonLabel != null)
        {
            saveButtonLabel.text = canSave ? "SAVE" : "SAVED";
        }
    }

    private string BuildStatusText(TacticsCharacterProgressionPlan plan)
    {
        if (!CanEditSelectedCharacter())
        {
            return "ALLY DATA";
        }

        if (plan == null)
        {
            return "READY";
        }

        return plan.HasPendingChanges
            ? $"PREVIEWING {plan.WorkingSnapshot.UnspentAttributePoints:00} AP LEFT"
            : "READY TO TUNE";
    }

    private bool ShouldShowEditControls(TacticsCharacterProgressionPlan plan)
    {
        if (!CanEditSelectedCharacter() || plan == null)
        {
            return false;
        }

        return plan.CommittedSnapshot.UnspentAttributePoints > 0 || plan.HasPendingChanges;
    }

    private void SetEditControlsVisible(bool visible)
    {
        if (primaryEditHeaderLabel != null)
        {
            primaryEditHeaderLabel.gameObject.SetActive(visible);
        }

        foreach (KeyValuePair<TacticsAbilityScalingStat, StatRowWidgets> pair in primaryStatRows)
        {
            if (pair.Value.DecreaseButton != null)
            {
                pair.Value.DecreaseButton.gameObject.SetActive(visible);
            }

            if (pair.Value.IncreaseButton != null)
            {
                pair.Value.IncreaseButton.gameObject.SetActive(visible);
            }
        }
    }

    private TacticsCharacterProgressionPlan GetOrCreatePlan(TacticsCharacterController character)
    {
        if (character == null)
        {
            return new TacticsCharacterProgressionPlan(TacticsCharacterProgressionSnapshot.CreateDefault(string.Empty));
        }

        string key = character.RuntimeCharacterId;
        if (!progressionPlansByCharacterId.TryGetValue(key, out TacticsCharacterProgressionPlan plan))
        {
            plan = new TacticsCharacterProgressionPlan(character.Progression);
            progressionPlansByCharacterId[key] = plan;
        }

        return plan;
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

    private Button CreateAdjustmentButton(string objectName, Transform parent, string labelText)
    {
        Button button = CreateSlimButton(objectName, parent, labelText);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(38f, 34f);

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 34f;
        layoutElement.preferredWidth = 38f;
        layoutElement.minWidth = 38f;
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

    private void SetButtonState(Button button, bool enabled)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = enabled;
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.color = enabled ? buttonHighlightColor : lockedColor;
        }
    }

    private void ApplyDeltaText(Text label, int delta)
    {
        if (label == null)
        {
            return;
        }

        if (delta == 0)
        {
            label.text = string.Empty;
            label.color = accentColor;
            return;
        }

        label.text = delta > 0 ? $"+{delta}" : delta.ToString();
        label.color = delta > 0 ? positiveDeltaColor : lockedColor;
    }

    private void ApplyDerivedDeltaText(Text label, string currentValue, string previewValue)
    {
        if (label == null)
        {
            return;
        }

        if (string.Equals(currentValue, previewValue, StringComparison.OrdinalIgnoreCase))
        {
            label.text = string.Empty;
            label.color = accentColor;
            return;
        }

        if (TryFormatRangeDelta(currentValue, previewValue, out string rangeDelta))
        {
            label.text = rangeDelta;
            label.color = positiveDeltaColor;
            return;
        }

        if (TryFormatPercentDelta(currentValue, previewValue, out string percentDelta))
        {
            label.text = percentDelta;
            label.color = positiveDeltaColor;
            return;
        }

        if (int.TryParse(currentValue, out int currentInt) && int.TryParse(previewValue, out int previewInt))
        {
            ApplyDeltaText(label, previewInt - currentInt);
            return;
        }

        label.text = "NEW";
        label.color = positiveDeltaColor;
    }

    private static bool TryFormatPercentDelta(string currentValue, string previewValue, out string formattedDelta)
    {
        formattedDelta = string.Empty;
        string currentTrimmed = currentValue?.Replace("%", string.Empty).Trim();
        string previewTrimmed = previewValue?.Replace("%", string.Empty).Trim();
        if (!float.TryParse(currentTrimmed, out float currentPercent) || !float.TryParse(previewTrimmed, out float previewPercent))
        {
            return false;
        }

        float delta = previewPercent - currentPercent;
        if (Mathf.Approximately(delta, 0f))
        {
            return true;
        }

        formattedDelta = delta > 0f ? $"+{delta:0.#}%" : $"{delta:0.#}%";
        return true;
    }

    private static bool TryFormatRangeDelta(string currentValue, string previewValue, out string formattedDelta)
    {
        formattedDelta = string.Empty;
        string[] currentTokens = currentValue?.Split('-') ?? Array.Empty<string>();
        string[] previewTokens = previewValue?.Split('-') ?? Array.Empty<string>();
        if (currentTokens.Length != 2 || previewTokens.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(currentTokens[0].Trim(), out int currentMin) ||
            !int.TryParse(currentTokens[1].Trim(), out int currentMax) ||
            !int.TryParse(previewTokens[0].Trim(), out int previewMin) ||
            !int.TryParse(previewTokens[1].Trim(), out int previewMax))
        {
            return false;
        }

        int minDelta = previewMin - currentMin;
        int maxDelta = previewMax - currentMax;
        if (minDelta == 0 && maxDelta == 0)
        {
            return true;
        }

        formattedDelta = $"{FormatSignedNumber(minDelta)}/{FormatSignedNumber(maxDelta)}";
        return true;
    }

    private static string FormatSignedNumber(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
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
        public StatRowWidgets(Text currentValue, Text previewValue, Text deltaValue, Button decreaseButton, Button increaseButton)
        {
            CurrentValue = currentValue;
            PreviewValue = previewValue;
            DeltaValue = deltaValue;
            DecreaseButton = decreaseButton;
            IncreaseButton = increaseButton;
        }

        public Text CurrentValue { get; }
        public Text PreviewValue { get; }
        public Text DeltaValue { get; }
        public Button DecreaseButton { get; }
        public Button IncreaseButton { get; }
    }

    private sealed class DerivedRowWidgets
    {
        public DerivedRowWidgets(string label, Text currentValue, Text previewValue, Text deltaValue)
        {
            Label = label;
            CurrentValue = currentValue;
            PreviewValue = previewValue;
            DeltaValue = deltaValue;
        }

        public string Label { get; }
        public Text CurrentValue { get; }
        public Text PreviewValue { get; }
        public Text DeltaValue { get; }
    }
}
