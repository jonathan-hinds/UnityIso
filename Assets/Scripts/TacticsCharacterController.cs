using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterController : MonoBehaviour, ITacticsSelectionHudTarget, ITacticsTurnParticipant, ITacticsCombatTextAnchor
{
    [Header("References")]
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsCharacterAnimator characterAnimator;
    [SerializeField] private TacticsCharacterDefinition characterDefinition;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.75f;
    [SerializeField, Min(0.01f)] private float jumpDuration = 0.25f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 0.22f;
    [SerializeField, Min(0)] private int maxStepUp = 1;
    [SerializeField, Min(0)] private int maxStepDown = 1;

    [Header("Spawn")]
    [SerializeField] private Vector2Int startingGridPosition;

    [Header("Alignment")]
    [SerializeField] private Vector2 tileAnchorOffset = new Vector2(0.18f, 0.125f);
    [SerializeField, Range(0.5f, 0.95f)] private float movementSortHandoffNormalizedTime = 0.72f;

    private Coroutine movementRoutine;
    private TacticsMovementDirection currentDirection = TacticsMovementDirection.SouthWest;
    private Vector2 lastAppliedTileAnchorOffset;
    private TacticsCharacterData characterData;
    private TacticsCharacterStats effectiveStats;
    private TacticsCharacterDerivedStats derivedStats;
    private TacticsCharacterRuntimeResources runtimeResources;
    private TacticsCharacterProgressionSnapshot progression;
    private TacticsTurnManager turnManager;
    private TacticsCharacterRegistry characterRegistry;
    private readonly List<TacticsAbilityDefinition> abilities = new();
    private readonly List<TacticsStatusEffectInstance> activeStatusEffects = new();
    private readonly List<TacticsInventoryItemSaveData> inventoryItems = new();
    private readonly Dictionary<TacticsEquipmentSlot, TacticsInventoryItemSaveData> equippedItemsBySlot = new();
    private readonly List<TacticsInventoryResolvedItem> reusableResolvedInventoryItems = new();
    private bool isPerformingAction;
    private bool isActionLockedThisTurn;

    public Vector2Int GridPosition { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsMoving => movementRoutine != null;
    public bool IsPerformingAction => isPerformingAction;
    public TacticsCharacterDefinition CharacterDefinition => characterDefinition;
    public TacticsCharacterData CharacterData => characterData;
    public string DisplayName => characterData != null ? characterData.DisplayName : (characterDefinition != null ? characterDefinition.DisplayName : name);
    public string RuntimeCharacterId { get; private set; }
    public string TurnOrderKey => BuildTurnOrderKey();
    public TacticsUnitTeam Team => characterData != null ? characterData.Team : (characterDefinition != null ? characterDefinition.Team : TacticsUnitTeam.Player);
    public bool IsPlayerControlled => Team == TacticsUnitTeam.Player;
    public TacticsCharacterDerivedStats DerivedStats => derivedStats;
    public TacticsCharacterRuntimeResources RuntimeResources => runtimeResources;
    public int CurrentHitPoints => runtimeResources.hitPoints;
    public int MaxHitPoints => derivedStats.maxHitPoints;
    public int CurrentStamina => runtimeResources.stamina;
    public int MaxStamina => derivedStats.maxStamina;
    public int CurrentMana => runtimeResources.mana;
    public int MaxMana => derivedStats.maxMana;
    public int BaseMeleeDamageMin => derivedStats.baseMeleeDamageMin;
    public int BaseMeleeDamageMax => derivedStats.baseMeleeDamageMax;
    public int BaseMagicDamageMin => derivedStats.baseMagicDamageMin;
    public int BaseMagicDamageMax => derivedStats.baseMagicDamageMax;
    public float MeleeCriticalHitChance => derivedStats.meleeCriticalHitChance;
    public float MagicCriticalHitChance => derivedStats.magicCriticalHitChance;
    public TacticsCharacterStats BaseStats => effectiveStats;
    public TacticsCharacterStats DefinitionBaseStats => characterData != null ? characterData.BaseStats : TacticsCharacterStats.Default();
    public int MoveRange => BaseStats.MoveRange;
    public int JumpHeight => BaseStats.JumpHeight;
    public int CurrentElevation => mapGenerator != null ? mapGenerator.GetTileElevation(GridPosition.x, GridPosition.y) : 0;
    public int CurrentExperience { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int ExperienceToNextLevel { get; private set; }
    public bool SupportsExperience => IsPlayerControlled && ExperienceToNextLevel > 0;
    public int UnspentAttributePoints => progression.UnspentAttributePoints;
    public TacticsCharacterProgressionSnapshot Progression => progression;
    public bool HasMovedThisTurn { get; private set; }
    public bool HasActedThisTurn { get; private set; }
    public bool IsTurnActive { get; private set; }
    public bool IsAlive => CurrentHitPoints > 0;
    public bool IsPresentationVisible { get; private set; } = true;
    public bool CanReceiveCommands => mapGenerator != null && mapGenerator.HasGeneratedMap && !IsMoving && !IsPerformingAction;
    public bool CanMoveThisTurn => IsTurnActive && !HasMovedThisTurn && !isActionLockedThisTurn && CanReceiveCommands;
    public bool CanUseAbilitiesThisTurn => IsTurnActive && !HasActedThisTurn && !isActionLockedThisTurn && CanReceiveCommands && IsAlive;
    public bool HasMovementAvailableForAbilityCost => IsTurnActive && !HasMovedThisTurn && IsAlive;
    public bool CanInteractThisTurn => IsTurnActive && !HasActedThisTurn && !isActionLockedThisTurn && CanReceiveCommands && IsAlive;
    public bool CanEndTurn => IsTurnActive && !IsMoving && !IsPerformingAction;
    public bool IsTurnEligible => isActiveAndEnabled && IsAlive;
    public Vector3 TurnFocusPoint => transform.position;
    public IReadOnlyList<TacticsAbilityDefinition> Abilities => abilities;
    public IReadOnlyList<TacticsStatusEffectInstance> ActiveStatusEffects => activeStatusEffects;
    public int InventoryItemCount => inventoryItems.Count;
    public bool IsActionLockedThisTurn => isActionLockedThisTurn;
    public bool IsTaunting => HasStatusEffect(TacticsStatusEffectType.Taunt);

    public event Action<ITacticsTurnParticipant> TurnEnded;
    public event Action<ITacticsTurnParticipant> TurnStateChanged;
    public event Action<TacticsCharacterController> ProgressionChanged;
    public event Action<TacticsCharacterController> InventoryChanged;
    public event Action<TacticsCharacterController, TacticsInventoryItemAddedEvent> InventoryItemAdded;

    public TacticsSelectionHudData BuildSelectionHudData()
    {
        return new TacticsSelectionHudData(
            DisplayName,
            string.Empty,
            CurrentLevel,
            new TacticsSelectionHudResourceData("HP", CurrentHitPoints, MaxHitPoints, new Color(0.72f, 0.23f, 0.27f, 1f)),
            new TacticsSelectionHudResourceData("MP", CurrentMana, MaxMana, new Color(0.25f, 0.49f, 0.77f, 1f)),
            new TacticsSelectionHudResourceData("ST", CurrentStamina, MaxStamina, new Color(0.34f, 0.62f, 0.42f, 1f)),
            new TacticsSelectionHudResourceData("EXP", CurrentExperience, ExperienceToNextLevel, new Color(0.58f, 0.32f, 0.82f, 1f), SupportsExperience),
            new TacticsSelectionHudCounterData("ACT", CanUseAbilitiesThisTurn ? 1 : 0, 1, true),
            new TacticsSelectionHudCounterData("MOV", CanMoveThisTurn ? 1 : 0, 1, true),
            characterData != null ? characterData.SelectedColor : Color.white);
    }

    public void Initialize(
        ProceduralIsometricMapGenerator generator,
        TacticsCharacterAnimator animator,
        TacticsCharacterDefinition definition,
        Vector2Int spawnTile,
        string runtimeCharacterId = "",
        TacticsCharacterProgressionSnapshot startingProgression = default,
        TacticsCharacterInventorySnapshot startingInventory = default)
    {
        Initialize(
            generator,
            animator,
            definition != null ? definition.BuildRuntimeData() : null,
            spawnTile,
            definition,
            runtimeCharacterId,
            startingProgression,
            startingInventory);
    }

    public void Initialize(
        ProceduralIsometricMapGenerator generator,
        TacticsCharacterAnimator animator,
        TacticsCharacterData data,
        Vector2Int spawnTile,
        TacticsCharacterDefinition definition = null,
        string runtimeCharacterId = "",
        TacticsCharacterProgressionSnapshot startingProgression = default,
        TacticsCharacterInventorySnapshot startingInventory = default)
    {
        mapGenerator = generator;
        characterAnimator = animator;
        characterDefinition = definition;
        characterData = data ?? definition?.BuildRuntimeData();
        RuntimeCharacterId = string.IsNullOrWhiteSpace(runtimeCharacterId)
            ? BuildFallbackRuntimeCharacterId(characterData, definition)
            : runtimeCharacterId.Trim();
        ApplyCharacterData(characterData, startingProgression, startingInventory);
        startingGridPosition = spawnTile;
        SubscribeToMap();
        SnapToTile(GetBestValidTile(spawnTile));
        RefreshCharacterRegistryState();
    }

    private void OnEnable()
    {
        SubscribeToMap();
        ResolveCharacterRegistry();
        RefreshCharacterRegistryState();
        RegisterWithTurnManager();
    }

    private void Start()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        }

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<TacticsCharacterAnimator>();
        }

        if (characterData == null && characterDefinition != null)
        {
            characterData = characterDefinition.BuildRuntimeData();
        }

        ApplyCharacterData(characterData, progression, BuildInventorySnapshot());
        ResolveCharacterRegistry();

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return;
        }

        SnapToTile(GetBestValidTile(startingGridPosition));
        RefreshCharacterRegistryState();
        RegisterWithTurnManager();
    }

    private void Update()
    {
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap || IsMoving)
        {
            return;
        }

        if (lastAppliedTileAnchorOffset != tileAnchorOffset)
        {
            SnapToTile(GridPosition);
        }
    }

    private void OnDisable()
    {
        isPerformingAction = false;
        StopMovementImmediately();
        UnsubscribeFromMap();
        if (turnManager != null)
        {
            turnManager.UnregisterParticipant(this);
        }

        characterRegistry?.Unregister(this);
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
        characterAnimator?.SetSelected(isSelected);
    }

    public void SetTargeted(bool isTargeted)
    {
        characterAnimator?.SetTargeted(isTargeted);
    }

    public void SetLocallyOwned(bool isLocallyOwned)
    {
        characterAnimator?.SetLocallyOwned(isLocallyOwned);
    }

    public void SetTargetHoverPreview(bool isActive)
    {
        characterAnimator?.SetTargetHoverPreview(isActive);
    }

    public bool TryMoveTo(Vector2Int destination)
    {
        if (!CanMoveThisTurn || !IsAlive)
        {
            return false;
        }

        if (!TryGetPathTo(destination, out List<Vector2Int> path))
        {
            return false;
        }

        return BeginMovement(path, consumeTurnMovement: true);
    }

    public void BeginTurn()
    {
        if (!IsAlive)
        {
            return;
        }

        StopMovementImmediately();
        IsTurnActive = true;
        characterAnimator?.SetTurnHighlight(true);
        isActionLockedThisTurn = false;
        HasMovedThisTurn = false;
        HasActedThisTurn = false;
        ProcessStartOfTurnStatusEffects();
        if (isActionLockedThisTurn)
        {
            HasMovedThisTurn = true;
            HasActedThisTurn = true;
        }
        NotifyTurnStateChanged();
    }

    public bool TryEndTurn()
    {
        if (!CanEndTurn)
        {
            return false;
        }

        RestoreEndTurnResources();
        IsTurnActive = false;
        characterAnimator?.SetTurnHighlight(false);
        NotifyTurnStateChanged();
        TurnEnded?.Invoke(this);
        return true;
    }

    public bool ApplyReplicatedMove(Vector2Int destination)
    {
        if (!IsAlive || IsMoving)
        {
            return false;
        }

        if (!TryGetPathTo(destination, out List<Vector2Int> path))
        {
            return false;
        }

        return BeginMovement(path, consumeTurnMovement: true);
    }

    public bool ApplyReplicatedEndTurn()
    {
        if (!IsAlive)
        {
            return false;
        }

        if (IsMoving || IsPerformingAction)
        {
            return false;
        }

        RestoreEndTurnResources();
        IsTurnActive = false;
        characterAnimator?.SetTurnHighlight(false);
        NotifyTurnStateChanged();
        TurnEnded?.Invoke(this);
        return true;
    }

    public bool TryGetMovementPath(Vector2Int destination, out List<Vector2Int> path)
    {
        return TryGetPathTo(destination, out path, enforceMoveRange: true);
    }

    public bool TryBuildMovementPreview(Vector2Int destination, List<Vector2Int> previewTiles, out int movementCost)
    {
        movementCost = 0;

        if (previewTiles == null)
        {
            throw new ArgumentNullException(nameof(previewTiles));
        }

        previewTiles.Clear();

        if (MoveRange <= 0 || !TryGetPathTo(destination, out List<Vector2Int> fullPath, enforceMoveRange: false))
        {
            return false;
        }

        movementCost = fullPath.Count - 1;
        int previewStepCount = Mathf.Min(MoveRange, fullPath.Count - 1);
        if (previewStepCount <= 0)
        {
            return false;
        }

        for (int i = 1; i <= previewStepCount; i++)
        {
            previewTiles.Add(fullPath[i]);
        }

        return previewTiles.Count > 0;
    }

    public bool TryConsumeInteraction()
    {
        if (!CanInteractThisTurn)
        {
            return false;
        }

        return CommitActionUse();
    }

    public bool HasStatusEffect(TacticsStatusEffectType statusEffectType)
    {
        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            if (activeStatusEffects[i].StatusEffectType == statusEffectType &&
                !activeStatusEffects[i].IsExpired)
            {
                return true;
            }
        }

        return false;
    }

    public int GetStatusEffectRemainingTurns(TacticsStatusEffectType statusEffectType)
    {
        int remainingTurns = 0;
        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            TacticsStatusEffectInstance statusEffect = activeStatusEffects[i];
            if (statusEffect.IsExpired || statusEffect.StatusEffectType != statusEffectType)
            {
                continue;
            }

            remainingTurns = Mathf.Max(remainingTurns, statusEffect.RemainingTurns);
        }

        return remainingTurns;
    }

    public bool ApplyStatusEffect(
        TacticsApplyStatusEffectData statusEffectData,
        int potency,
        TacticsCharacterController sourceCharacter = null)
    {
        if (!IsAlive || statusEffectData.DurationTurns <= 0)
        {
            return false;
        }

        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffectData.StatusEffectType);
        int resolvedPotency = TacticsStatusEffectLibrary.NormalizePotency(descriptor.StatusEffectType, potency);
        bool refreshedExistingEffect = false;
        string effectKey = TacticsStatusEffectLibrary.BuildEffectKey(statusEffectData);

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            TacticsStatusEffectInstance existingEffect = activeStatusEffects[i];
            if (existingEffect.StatusEffectType != statusEffectData.StatusEffectType ||
                !string.Equals(existingEffect.EffectKey, effectKey, StringComparison.Ordinal))
            {
                continue;
            }

            activeStatusEffects[i] = existingEffect.Refresh(
                Mathf.Max(existingEffect.RemainingTurns, statusEffectData.DurationTurns),
                TacticsStatusEffectLibrary.MergePotency(
                    existingEffect.StatusEffectType,
                    existingEffect.Potency,
                    resolvedPotency));
            refreshedExistingEffect = true;
            break;
        }

        if (!refreshedExistingEffect)
        {
            activeStatusEffects.Add(new TacticsStatusEffectInstance(statusEffectData, statusEffectData.DurationTurns, resolvedPotency));
        }

        if (descriptor.BlocksActions && IsTurnActive)
        {
            isActionLockedThisTurn = true;
            HasMovedThisTurn = true;
            HasActedThisTurn = true;
        }

        RefreshStatsFromStatusEffects();
        TacticsCombatTextSystem.ShowStatusEffectApplied(this, statusEffectData);
        TacticsOverheadHealthBar.ShowFor(this);
        NotifyTurnStateChanged();
        return true;
    }

    public bool ApplyReplicatedInteraction()
    {
        if (!IsAlive || IsMoving || IsPerformingAction || !IsTurnActive || HasActedThisTurn)
        {
            return false;
        }

        return CommitActionUse();
    }

    public TacticsAbilityDefinition GetPrimaryActionAbility()
    {
        return abilities.Count > 0 ? abilities[0] : null;
    }

    public int RollBaseDamage(TacticsAbilityDamageType damageType)
    {
        int minimumDamage = damageType == TacticsAbilityDamageType.Magic
            ? Mathf.Max(0, BaseMagicDamageMin)
            : Mathf.Max(0, BaseMeleeDamageMin);
        int maximumDamage = damageType == TacticsAbilityDamageType.Magic
            ? Mathf.Max(minimumDamage, BaseMagicDamageMax)
            : Mathf.Max(minimumDamage, BaseMeleeDamageMax);
        return UnityEngine.Random.Range(minimumDamage, maximumDamage + 1);
    }

    public bool RollCriticalHit(TacticsAbilityDamageType damageType)
    {
        float criticalHitChance = damageType == TacticsAbilityDamageType.Magic
            ? MagicCriticalHitChance
            : MeleeCriticalHitChance;
        return UnityEngine.Random.value < Mathf.Clamp01(criticalHitChance);
    }

    public int GetPrimaryStat(TacticsAbilityScalingStat stat)
    {
        return BaseStats.GetPrimaryStat(stat);
    }

    public TacticsCharacterInventorySnapshot BuildInventorySnapshot()
    {
        TacticsCharacterInventorySnapshot snapshot = new TacticsCharacterInventorySnapshot
        {
            characterId = characterData != null ? characterData.CharacterId : string.Empty,
            items = new List<TacticsInventoryItemSaveData>(inventoryItems.Count),
        equippedItems = new List<TacticsEquippedItemSaveData>(equippedItemsBySlot.Count)
        };

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            snapshot.items.Add(inventoryItems[i]?.Clone());
        }

        foreach (KeyValuePair<TacticsEquipmentSlot, TacticsInventoryItemSaveData> pair in equippedItemsBySlot)
        {
            snapshot.equippedItems.Add(new TacticsEquippedItemSaveData
            {
                slot = pair.Key,
                instanceId = pair.Value.instanceId,
                itemId = pair.Value.itemId,
                quantity = pair.Value.quantity
            });
        }

        return snapshot.Sanitize();
    }

    public IReadOnlyList<TacticsInventoryResolvedItem> GetResolvedInventoryItems()
    {
        reusableResolvedInventoryItems.Clear();
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            TacticsInventoryItemSaveData item = inventoryItems[i];
            if (item == null || !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition))
            {
                continue;
            }

            reusableResolvedInventoryItems.Add(new TacticsInventoryResolvedItem(
                item,
                definition,
                isEquipped: false,
                item.quantity));
        }

        return reusableResolvedInventoryItems;
    }

    public bool IsItemEquipped(string itemInstanceId)
    {
        if (string.IsNullOrWhiteSpace(itemInstanceId))
        {
            return false;
        }

        foreach (KeyValuePair<TacticsEquipmentSlot, TacticsInventoryItemSaveData> pair in equippedItemsBySlot)
        {
            if (pair.Value != null &&
                string.Equals(pair.Value.instanceId, itemInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetEquippedItem(TacticsEquipmentSlot slot, out TacticsEquipmentRuntimeSummary summary)
    {
        summary = default;
        if (!equippedItemsBySlot.TryGetValue(slot, out TacticsInventoryItemSaveData item) || item == null)
        {
            return false;
        }

        if (!TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
            definition is not TacticsEquipmentItemDefinition equipment)
        {
            return false;
        }

        summary = new TacticsEquipmentRuntimeSummary(equipment, item);
        return summary.IsValid;
    }

    public TacticsInventoryActionKind GetDefaultInventoryAction(string itemInstanceId)
    {
        TacticsInventoryItemSaveData item = FindInventoryItem(itemInstanceId);
        if (item == null || !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition))
        {
            return TacticsInventoryActionKind.None;
        }

        return definition switch
        {
            TacticsConsumableItemDefinition => TacticsInventoryActionKind.UseConsumable,
            TacticsEquipmentItemDefinition => IsItemEquipped(item.instanceId)
                ? TacticsInventoryActionKind.Unequip
                : TacticsInventoryActionKind.Equip,
            _ => TacticsInventoryActionKind.None
        };
    }

    public bool TryEquipItem(string itemInstanceId)
    {
        TacticsInventoryItemSaveData item = FindInventoryItem(itemInstanceId);
        if (item == null ||
            !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
            definition is not TacticsEquipmentItemDefinition equipment)
        {
            return false;
        }

        TransferEquippedItemToInventory(equipment.Slot);
        inventoryItems.Remove(item);
        equippedItemsBySlot[equipment.Slot] = item;
        RefreshStatsFromStatusEffects();
        InventoryChanged?.Invoke(this);
        NotifyTurnStateChanged();
        return true;
    }

    public bool TryUnequipItem(TacticsEquipmentSlot slot)
    {
        if (!TransferEquippedItemToInventory(slot))
        {
            return false;
        }

        RefreshStatsFromStatusEffects();
        InventoryChanged?.Invoke(this);
        NotifyTurnStateChanged();
        return true;
    }

    public bool TryAddInventoryItem(TacticsInventoryItemSaveData item)
    {
        TacticsInventoryItemSaveData cloned = item?.Clone();
        if (cloned == null ||
            string.IsNullOrWhiteSpace(cloned.itemId) ||
            !TacticsItemCatalogResources.TryGetItem(cloned.itemId, out TacticsItemDefinition definition))
        {
            return false;
        }

        cloned.quantity = Mathf.Max(1, cloned.quantity);
        if (definition is TacticsConsumableItemDefinition)
        {
            TacticsInventoryItemSaveData existingStack = FindConsumableStack(cloned.itemId);
            if (existingStack != null)
            {
                int quantityAdded = cloned.quantity;
                existingStack.quantity = AddInventoryQuantity(existingStack.quantity, quantityAdded);
                RaiseInventoryItemAdded(existingStack, definition, quantityAdded, mergedIntoExistingStack: true);
                InventoryChanged?.Invoke(this);
                return true;
            }

            if (string.IsNullOrWhiteSpace(cloned.instanceId) || FindInventoryItem(cloned.instanceId) != null)
            {
                cloned.instanceId = CreateUniqueInventoryInstanceId();
            }
        }
        else if (string.IsNullOrWhiteSpace(cloned.instanceId) || ContainsItemInstanceId(cloned.instanceId))
        {
            return false;
        }

        if (definition is not TacticsConsumableItemDefinition)
        {
            cloned.quantity = 1;
        }

        inventoryItems.Add(cloned);
        RaiseInventoryItemAdded(cloned, definition, cloned.quantity, mergedIntoExistingStack: false);
        InventoryChanged?.Invoke(this);
        return true;
    }

    public bool TryUseConsumableItem(string itemInstanceId, TacticsCombatSystem combat, bool replicated)
    {
        TacticsInventoryItemSaveData item = FindInventoryItem(itemInstanceId);
        if (item == null ||
            !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
            definition is not TacticsConsumableItemDefinition consumable ||
            consumable.LinkedAbility == null ||
            combat == null)
        {
            return false;
        }

        bool success = replicated
            ? combat.ApplyReplicatedAbility(this, consumable.LinkedAbility, GridPosition)
            : combat.TryUseAbility(this, consumable.LinkedAbility, GridPosition);
        if (!success)
        {
            return false;
        }

        if (item.quantity > 1)
        {
            item.quantity--;
        }
        else
        {
            RemoveInventoryItem(item.instanceId);
        }

        InventoryChanged?.Invoke(this);
        return true;
    }

    public bool ApplyDamage(
        int damageAmount,
        Vector3? damageSourceWorldPosition = null,
        bool isCriticalHit = false,
        TacticsCharacterController damageSourceCharacter = null,
        bool playImpactAnimation = true)
    {
        if (!IsAlive || damageAmount <= 0)
        {
            return false;
        }

        runtimeResources.hitPoints = Mathf.Max(0, runtimeResources.hitPoints - damageAmount);
        if (playImpactAnimation)
        {
            characterAnimator?.PlayDamageImpact(damageSourceWorldPosition);
        }
        TacticsCombatTextSystem.ShowDamage(this, damageAmount, isCriticalHit);
        TacticsOverheadHealthBar.ShowFor(this);
        NotifyTurnStateChanged();

        if (runtimeResources.hitPoints == 0)
        {
            HandleDefeat(damageSourceCharacter);
        }

        return true;
    }

    public int RestoreHitPoints(int amount)
    {
        if (!IsAlive || amount <= 0 || CurrentHitPoints >= MaxHitPoints)
        {
            return 0;
        }

        int restoredAmount = Mathf.Min(amount, MaxHitPoints - CurrentHitPoints);
        runtimeResources.hitPoints = Mathf.Min(MaxHitPoints, runtimeResources.hitPoints + restoredAmount);
        TacticsCombatTextSystem.ShowHealing(this, restoredAmount);
        TacticsOverheadHealthBar.ShowFor(this);
        NotifyTurnStateChanged();
        return restoredAmount;
    }

    public int RestoreResource(TacticsAbilityResourceType resourceType, int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return 0;
        }

        int restoredAmount = resourceType switch
        {
            TacticsAbilityResourceType.Stamina => RestoreRuntimeResource(ref runtimeResources.stamina, MaxStamina, amount),
            TacticsAbilityResourceType.Mana => RestoreRuntimeResource(ref runtimeResources.mana, MaxMana, amount),
            _ => 0
        };

        if (restoredAmount <= 0)
        {
            return 0;
        }

        TacticsCombatTextSystem.ShowResourceRestore(this, resourceType, restoredAmount);
        NotifyTurnStateChanged();
        return restoredAmount;
    }

    public bool HasResourcesForAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null || !ability.HasResourceCost)
        {
            return true;
        }

        return GetCurrentResource(ability.CostResourceType) >= ability.CostAmount;
    }

    public bool CanPayAbilityCost(TacticsAbilityDefinition ability)
    {
        return TryGetAbilityCostPayment(ability, HasMovementAvailableForAbilityCost, out _);
    }

    public bool CanPayAbilityCost(TacticsAbilityDefinition ability, bool movementAvailable)
    {
        return TryGetAbilityCostPayment(ability, movementAvailable, out _);
    }

    public bool TryGetAbilityCostPayment(TacticsAbilityDefinition ability, out TacticsAbilityCostPayment payment)
    {
        return TryGetAbilityCostPayment(ability, HasMovementAvailableForAbilityCost, out payment);
    }

    public bool TryGetAbilityCostPayment(
        TacticsAbilityDefinition ability,
        bool movementAvailable,
        out TacticsAbilityCostPayment payment)
    {
        payment = TacticsAbilityCostPayment.None;

        if (ability == null || !ability.HasCost)
        {
            return true;
        }

        if (ability.HasMovementCost)
        {
            if (!movementAvailable)
            {
                return false;
            }

            payment = new TacticsAbilityCostPayment(TacticsAbilityResourceType.Movement, 1);
            return true;
        }

        if (ability.HasResourceCost && HasResourcesForAbility(ability))
        {
            payment = new TacticsAbilityCostPayment(ability.CostResourceType, ability.CostAmount);
            return true;
        }

        if (ability.AllowsMovementAsAlternateCost && movementAvailable)
        {
            payment = new TacticsAbilityCostPayment(TacticsAbilityResourceType.Movement, 1);
            return true;
        }

        return false;
    }

    public bool TrySpendAbilityCost(TacticsAbilityDefinition ability)
    {
        if (!TryGetAbilityCostPayment(ability, out TacticsAbilityCostPayment payment))
        {
            return ability == null || !ability.HasCost;
        }

        return TrySpendAbilityCost(payment);
    }

    public bool TrySpendAbilityCost(TacticsAbilityCostPayment payment)
    {
        if (!payment.HasCost)
        {
            return true;
        }

        switch (payment.ResourceType)
        {
            case TacticsAbilityResourceType.Stamina:
                if (runtimeResources.stamina < payment.Amount)
                {
                    return false;
                }

                runtimeResources.stamina = Mathf.Max(0, runtimeResources.stamina - payment.Amount);
                break;

            case TacticsAbilityResourceType.Mana:
                if (runtimeResources.mana < payment.Amount)
                {
                    return false;
                }

                runtimeResources.mana = Mathf.Max(0, runtimeResources.mana - payment.Amount);
                break;

            case TacticsAbilityResourceType.Movement:
                if (!HasMovementAvailableForAbilityCost)
                {
                    return false;
                }

                HasMovedThisTurn = true;
                break;
        }

        NotifyTurnStateChanged();
        return true;
    }

    public bool TryAwardExperience(int amount)
    {
        if (!SupportsExperience || amount <= 0)
        {
            return false;
        }

        if (!progression.TryAwardExperience(amount, ExperienceToNextLevel, out TacticsCharacterProgressionSnapshot updatedProgression, out int levelsGained))
        {
            return false;
        }

        bool progressionChanged = levelsGained > 0 || updatedProgression.CurrentExperience != progression.CurrentExperience;
        ApplyProgressionSnapshot(updatedProgression, preserveResourceRatios: levelsGained <= 0, emitNotification: progressionChanged);
        return progressionChanged;
    }

    public bool TryAllocateAttributePoint(TacticsAbilityScalingStat stat)
    {
        if (!SupportsExperience)
        {
            return false;
        }

        if (!progression.TryAllocatePoint(stat, out TacticsCharacterProgressionSnapshot updatedProgression))
        {
            return false;
        }

        ApplyProgressionSnapshot(updatedProgression, preserveResourceRatios: true, emitNotification: true);
        return true;
    }

    public bool TryCommitProgression(TacticsCharacterProgressionSnapshot updatedProgression)
    {
        if (!SupportsExperience)
        {
            return false;
        }

        TacticsCharacterProgressionSnapshot resolvedProgression =
            ResolveProgression(updatedProgression, characterData != null ? characterData.CharacterId : string.Empty);
        if (AreEquivalent(progression, resolvedProgression))
        {
            return false;
        }

        ApplyProgressionSnapshot(resolvedProgression, preserveResourceRatios: true, emitNotification: true);
        return true;
    }

    public TacticsCharacterStats GetStatsForProgression(TacticsCharacterProgressionSnapshot snapshot)
    {
        TacticsCharacterProgressionSnapshot resolvedProgression =
            ResolveProgression(snapshot, characterData != null ? characterData.CharacterId : string.Empty);
        TacticsCharacterStats resolvedStats =
            resolvedProgression.ApplyTo(characterData != null ? characterData.BaseStats : TacticsCharacterStats.Default());

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            TacticsStatusEffectLibrary.TryApplyPersistentStatModifier(ref resolvedStats, activeStatusEffects[i]);
        }

        ApplyEquipmentBonusesToStats(ref resolvedStats);

        return resolvedStats;
    }

    public TacticsCharacterDerivedStats GetDerivedStatsForProgression(TacticsCharacterProgressionSnapshot snapshot)
    {
        TacticsCharacterStats resolvedStats = GetStatsForProgression(snapshot);
        TacticsCharacterDerivedStats resolvedDerivedStats = resolvedStats.CalculateDerivedStats();
        ApplyEquipmentBonusesToDerivedStats(ref resolvedDerivedStats);
        return resolvedDerivedStats;
    }

    public void CommitAbilityUse()
    {
        CommitActionUse();
    }

    public IEnumerator PlayAttackAnimationTowards(Vector2Int targetTile)
    {
        currentDirection = GetDirection(GridPosition, targetTile);
        isPerformingAction = true;
        NotifyTurnStateChanged();

        if (characterAnimator != null)
        {
            yield return characterAnimator.PlayAttack(currentDirection);
        }

        isPerformingAction = false;
        characterAnimator?.SetIdle(currentDirection);
        NotifyTurnStateChanged();
    }

    public bool CanTraverseForcedMovementStep(Vector2Int from, Vector2Int to)
    {
        if (mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            !mapGenerator.IsTraversable(to.x, to.y))
        {
            return false;
        }

        int startElevation = mapGenerator.GetTileElevation(from.x, from.y);
        int endElevation = mapGenerator.GetTileElevation(to.x, to.y);
        int elevationDelta = endElevation - startElevation;
        return elevationDelta <= maxStepUp && elevationDelta >= -maxStepDown;
    }

    public bool TryBeginKnockback(
        Vector2Int destination,
        TacticsMovementDirection travelDirection,
        TacticsMovementDirection animationDirection,
        TacticsAbilityKnockbackData settings)
    {
        if (!IsAlive ||
            movementRoutine != null ||
            mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            destination == GridPosition ||
            !mapGenerator.TryGetTileWorldPosition(destination.x, destination.y, out _))
        {
            return false;
        }

        movementRoutine = StartCoroutine(PlayKnockbackRoutine(destination, travelDirection, animationDirection, settings));
        NotifyTurnStateChanged();
        return true;
    }

    public bool TryBeginThrow(
        Vector2Int destination,
        TacticsMovementDirection travelDirection,
        TacticsMovementDirection animationDirection,
        TacticsAbilityThrowData settings)
    {
        if (!IsAlive ||
            movementRoutine != null ||
            mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            destination == GridPosition ||
            !mapGenerator.TryGetTileWorldPosition(destination.x, destination.y, out _))
        {
            return false;
        }

        movementRoutine = StartCoroutine(PlayThrowRoutine(destination, travelDirection, animationDirection, settings));
        NotifyTurnStateChanged();
        return true;
    }

    public void PlayDamageImpact(Vector3? damageSourceWorldPosition = null)
    {
        if (!IsAlive)
        {
            return;
        }

        characterAnimator?.PlayDamageImpact(damageSourceWorldPosition);
    }

    public bool TryGetPathTo(Vector2Int destination, out List<Vector2Int> path, bool enforceMoveRange = true)
    {
        path = null;

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap || IsMoving)
        {
            return false;
        }

        if (TacticsTileBlockerUtility.IsBlockingTile(destination))
        {
            return false;
        }

        path = IsometricAStarPathfinder.FindPath(
            mapGenerator,
            GridPosition,
            destination,
            maxStepUp,
            maxStepDown,
            IsTileBlockedForMovement);

        if (path == null || path.Count <= 1)
        {
            path = null;
            return false;
        }

        if (enforceMoveRange && MoveRange > 0 && (path.Count - 1) > MoveRange)
        {
            path = null;
            return false;
        }

        return true;
    }

    private IEnumerator FollowPath(IReadOnlyList<Vector2Int> path)
    {
        characterAnimator?.ResetWalkCycle();

        for (int i = 1; i < path.Count; i++)
        {
            yield return MoveBetweenTiles(path[i - 1], path[i]);
            GridPosition = path[i];
            RefreshCharacterRegistryState();
            ResolveTriggeredStatusEffects(TacticsStatusEffectTrigger.TileMoved);
            if (!IsAlive)
            {
                yield break;
            }
        }

        movementRoutine = null;
        characterAnimator?.SetIdle(currentDirection);
        NotifyTurnStateChanged();
    }

    private bool BeginMovement(IReadOnlyList<Vector2Int> path, bool consumeTurnMovement)
    {
        if (path == null || path.Count <= 1)
        {
            return false;
        }

        if (consumeTurnMovement)
        {
            HasMovedThisTurn = true;
        }

        NotifyTurnStateChanged();
        movementRoutine = StartCoroutine(FollowPath(path));
        return true;
    }

    private bool CommitActionUse()
    {
        if (HasActedThisTurn)
        {
            return false;
        }

        HasActedThisTurn = true;
        ResolveTriggeredStatusEffects(TacticsStatusEffectTrigger.ActionPerformed);
        if (IsAlive)
        {
            NotifyTurnStateChanged();
        }

        return true;
    }

    private void ResolveTriggeredStatusEffects(TacticsStatusEffectTrigger trigger)
    {
        if (!IsAlive || activeStatusEffects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            TacticsStatusEffectInstance statusEffect = activeStatusEffects[i];
            if (statusEffect.IsExpired)
            {
                continue;
            }

            int damageAmount = TacticsStatusEffectLibrary.GetTriggeredDamage(statusEffect, trigger);
            if (damageAmount <= 0)
            {
                continue;
            }

            ApplyDamage(damageAmount);
            if (!IsAlive)
            {
                return;
            }
        }
    }

    private IEnumerator MoveBetweenTiles(Vector2Int from, Vector2Int to)
    {
        int startElevation = mapGenerator.GetTileElevation(from.x, from.y);
        int endElevation = mapGenerator.GetTileElevation(to.x, to.y);
        Vector3 startPosition = GetTileAnchorWorldPosition(from, startElevation);
        Vector3 endPosition = GetTileAnchorWorldPosition(to, endElevation);

        currentDirection = GetDirection(from, to);
        bool changesElevation = endElevation != startElevation;
        float duration = changesElevation ? jumpDuration : Vector3.Distance(startPosition, endPosition) / moveSpeed;
        duration = Mathf.Max(0.01f, duration);

        int sortingLayerId = characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
        int startSortingOrder = mapGenerator.GetCharacterSortingOrder(from.x, from.y, startElevation);
        int endSortingOrder = mapGenerator.GetCharacterSortingOrder(to.x, to.y, endElevation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            if (changesElevation)
            {
                position.y += Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                characterAnimator?.SetJump(currentDirection);
            }
            else
            {
                characterAnimator?.SetWalk(currentDirection, Time.deltaTime);
            }

            transform.position = position;
            ApplySorting(sortingLayerId, ResolveMovementSortingOrder(startSortingOrder, endSortingOrder, t, movementSortHandoffNormalizedTime));
            yield return null;
        }

        transform.position = endPosition;
        ApplySorting(sortingLayerId, endSortingOrder);
    }

    private IEnumerator PlayKnockbackRoutine(
        Vector2Int destination,
        TacticsMovementDirection travelDirection,
        TacticsMovementDirection animationDirection,
        TacticsAbilityKnockbackData settings)
    {
        Vector2Int startTile = GridPosition;
        int startElevation = mapGenerator.GetTileElevation(startTile.x, startTile.y);
        int endElevation = mapGenerator.GetTileElevation(destination.x, destination.y);
        Vector3 startPosition = GetTileAnchorWorldPosition(startTile, startElevation);
        Vector3 endPosition = GetTileAnchorWorldPosition(destination, endElevation);
        int tileDistance = Mathf.Abs(destination.x - startTile.x) + Mathf.Abs(destination.y - startTile.y);
        float duration = Mathf.Max(0.01f, settings.Duration);
        float arcHeight = Mathf.Max(jumpArcHeight, settings.ArcHeight);

        currentDirection = travelDirection;
        int sortingLayerId = characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
        int startSortingOrder = mapGenerator.GetCharacterSortingOrder(startTile.x, startTile.y, startElevation);
        int endSortingOrder = mapGenerator.GetCharacterSortingOrder(destination.x, destination.y, endElevation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = position;
            ApplySorting(sortingLayerId, ResolveMovementSortingOrder(startSortingOrder, endSortingOrder, t, movementSortHandoffNormalizedTime));
            characterAnimator?.SetJump(animationDirection);
            yield return null;
        }

        transform.position = endPosition;
        ApplySorting(sortingLayerId, endSortingOrder);
        GridPosition = destination;
        RefreshCharacterRegistryState();
        ResolveTriggeredStatusEffects(TacticsStatusEffectTrigger.TileMoved);
        if (!IsAlive)
        {
            yield break;
        }

        movementRoutine = null;
        currentDirection = animationDirection;
        characterAnimator?.SetIdle(currentDirection);
        NotifyTurnStateChanged();
    }

    private IEnumerator PlayThrowRoutine(
        Vector2Int destination,
        TacticsMovementDirection travelDirection,
        TacticsMovementDirection animationDirection,
        TacticsAbilityThrowData settings)
    {
        Vector2Int startTile = GridPosition;
        int startElevation = mapGenerator.GetTileElevation(startTile.x, startTile.y);
        int endElevation = mapGenerator.GetTileElevation(destination.x, destination.y);
        Vector3 startPosition = GetTileAnchorWorldPosition(startTile, startElevation);
        Vector3 endPosition = GetTileAnchorWorldPosition(destination, endElevation);
        float duration = Mathf.Max(0.01f, settings.Duration);
        float arcHeight = Mathf.Max(jumpArcHeight, settings.ArcHeight);

        currentDirection = travelDirection;
        int sortingLayerId = characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
        int startSortingOrder = mapGenerator.GetCharacterSortingOrder(startTile.x, startTile.y, startElevation);
        int endSortingOrder = mapGenerator.GetCharacterSortingOrder(destination.x, destination.y, endElevation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = position;
            ApplySorting(sortingLayerId, ResolveMovementSortingOrder(startSortingOrder, endSortingOrder, t, movementSortHandoffNormalizedTime));
            characterAnimator?.SetJump(animationDirection);
            yield return null;
        }

        transform.position = endPosition;
        ApplySorting(sortingLayerId, endSortingOrder);
        GridPosition = destination;
        RefreshCharacterRegistryState();
        ResolveTriggeredStatusEffects(TacticsStatusEffectTrigger.TileMoved);
        if (!IsAlive)
        {
            yield break;
        }

        movementRoutine = null;
        currentDirection = animationDirection;
        characterAnimator?.SetIdle(currentDirection);
        NotifyTurnStateChanged();
    }

    private static int ResolveMovementSortingOrder(
        int startSortingOrder,
        int endSortingOrder,
        float normalizedProgress,
        float handoffNormalizedTime)
    {
        if (startSortingOrder == endSortingOrder)
        {
            return startSortingOrder;
        }

        // Use the front-most of the two valid standing buckets during the transition so
        // neither the tile we're leaving nor the tile we're entering briefly renders on top.
        int transitionSortingOrder = Mathf.Max(startSortingOrder, endSortingOrder);
        return normalizedProgress < handoffNormalizedTime ? transitionSortingOrder : endSortingOrder;
    }

    private void ApplySorting(int sortingLayerId, int sortingOrder)
    {
        if (characterAnimator == null)
        {
            return;
        }

        characterAnimator.SetSorting(sortingLayerId, sortingOrder);
    }

    private bool IsTileBlockedForMovement(Vector2Int tile)
    {
        if (TacticsTileBlockerUtility.IsBlockingTile(tile))
        {
            return true;
        }

        ResolveCharacterRegistry();
        return characterRegistry != null &&
               characterRegistry.TryGetCharacterAtTile(tile, out _, this);
    }

    private void HandleMapGenerated()
    {
        SnapToTile(GetBestValidTile(GridPosition == default ? startingGridPosition : GridPosition));
    }

    private void SnapToTile(Vector2Int tile)
    {
        if (mapGenerator == null || !mapGenerator.TryGetTileWorldPosition(tile.x, tile.y, out Vector3 worldPosition))
        {
            return;
        }

        GridPosition = tile;
        transform.position = worldPosition + GetTileAnchorOffset();
        lastAppliedTileAnchorOffset = tileAnchorOffset;
        RefreshCharacterRegistryState();
        ApplySorting(
            characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default"),
            mapGenerator.GetCharacterSortingOrder(tile.x, tile.y, mapGenerator.GetTileElevation(tile.x, tile.y)));
        characterAnimator?.SetTurnHighlight(IsTurnActive);
        characterAnimator?.SetIdle(currentDirection);
    }

    private Vector2Int GetBestValidTile(Vector2Int requestedTile)
    {
        if (mapGenerator != null && mapGenerator.IsTraversable(requestedTile.x, requestedTile.y))
        {
            return requestedTile;
        }

        Vector2Int center = mapGenerator != null ? mapGenerator.GetCenterTile() : Vector2Int.zero;
        int maxRadius = mapGenerator != null ? Mathf.Max(mapGenerator.Width, mapGenerator.Length) : 0;

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    if (mapGenerator != null && mapGenerator.IsTraversable(x, y))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return Vector2Int.zero;
    }

    private TacticsMovementDirection GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (delta.x > 0)
        {
            return TacticsMovementDirection.NorthEast;
        }

        if (delta.x < 0)
        {
            return TacticsMovementDirection.SouthWest;
        }

        if (delta.y > 0)
        {
            return TacticsMovementDirection.NorthWest;
        }

        return TacticsMovementDirection.SouthEast;
    }

    private void SubscribeToMap()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
            mapGenerator.MapGenerated += HandleMapGenerated;
        }
    }

    private void UnsubscribeFromMap()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }
    }

    private Vector3 GetTileAnchorWorldPosition(Vector2Int tile, int elevation)
    {
        return mapGenerator.GridToWorldPosition(tile.x, tile.y, elevation) + GetTileAnchorOffset();
    }

    private Vector3 GetTileAnchorOffset()
    {
        return new Vector3(tileAnchorOffset.x, tileAnchorOffset.y, 0f);
    }

    public Vector3 GetCombatTextSpawnPosition(float verticalPadding = 0.18f)
    {
        SpriteRenderer targetRenderer = characterAnimator != null ? characterAnimator.TargetRenderer : null;
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return new Vector3(
                bounds.center.x,
                bounds.max.y + Mathf.Max(verticalPadding, bounds.size.y * 0.12f),
                transform.position.z);
        }

        return transform.position + new Vector3(0f, 0.75f + verticalPadding, 0f);
    }

    public int GetCombatTextSortingLayerId()
    {
        return characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
    }

    public int GetCombatTextSortingOrder()
    {
        return characterAnimator != null ? characterAnimator.CurrentSortingOrder : 0;
    }

    public SpriteRenderer GetPreviewSpriteRenderer()
    {
        return characterAnimator != null ? characterAnimator.TargetRenderer : null;
    }

    public Vector3 GetProjectileLaunchPosition(float normalizedHeight = 0.58f)
    {
        return GetVisualAnchorPosition(normalizedHeight, new Vector3(0f, 0.55f, 0f));
    }

    public Vector3 GetProjectileImpactPosition(float normalizedHeight = 0.5f)
    {
        return GetVisualAnchorPosition(normalizedHeight, new Vector3(0f, 0.45f, 0f));
    }

    public Vector3 GetHitEffectAnchorPosition(float normalizedHeight = 0.72f)
    {
        return GetVisualAnchorPosition(normalizedHeight, new Vector3(0f, 0.9f, 0f));
    }

    public int GetCurrentResource(TacticsAbilityResourceType resourceType)
    {
        return resourceType switch
        {
            TacticsAbilityResourceType.Stamina => CurrentStamina,
            TacticsAbilityResourceType.Mana => CurrentMana,
            TacticsAbilityResourceType.Movement => HasMovementAvailableForAbilityCost ? 1 : 0,
            _ => 0
        };
    }

    public int GetMaxResource(TacticsAbilityResourceType resourceType)
    {
        return resourceType switch
        {
            TacticsAbilityResourceType.Stamina => MaxStamina,
            TacticsAbilityResourceType.Mana => MaxMana,
            TacticsAbilityResourceType.Movement => 1,
            _ => 0
        };
    }

    public int GetMissingResource(TacticsAbilityResourceType resourceType)
    {
        return Mathf.Max(0, GetMaxResource(resourceType) - GetCurrentResource(resourceType));
    }

    private void ProcessStartOfTurnStatusEffects()
    {
        if (!IsAlive || activeStatusEffects.Count == 0)
        {
            return;
        }

        bool statsChanged = false;
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            if (i >= activeStatusEffects.Count)
            {
                continue;
            }

            TacticsStatusEffectInstance statusEffect = activeStatusEffects[i];
            if (statusEffect.IsExpired)
            {
                activeStatusEffects.RemoveAt(i);
                statsChanged = true;
                continue;
            }

            TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect.StatusEffectType);
            if (descriptor.AppliesAtTurnStart)
            {
                ResolveStartOfTurnStatusEffect(statusEffect, descriptor);
                if (!IsAlive || i >= activeStatusEffects.Count)
                {
                    break;
                }

                statusEffect = activeStatusEffects[i];
                if (statusEffect.IsExpired)
                {
                    activeStatusEffects.RemoveAt(i);
                    statsChanged = true;
                    continue;
                }
            }

            statusEffect = statusEffect.WithRemainingTurns(statusEffect.RemainingTurns - 1);
            if (statusEffect.IsExpired)
            {
                activeStatusEffects.RemoveAt(i);
                statsChanged = true;
            }
            else
            {
                activeStatusEffects[i] = statusEffect;
            }
        }

        if (statsChanged)
        {
            RefreshStatsFromStatusEffects();
        }
    }

    private void ResolveStartOfTurnStatusEffect(
        TacticsStatusEffectInstance statusEffect,
        TacticsStatusEffectDescriptor descriptor)
    {
        switch (statusEffect.StatusEffectType)
        {
            case TacticsStatusEffectType.Cleanse:
                if (statusEffect.Potency > 0)
                {
                    RestoreHitPoints(statusEffect.Potency);
                }
                break;

            case TacticsStatusEffectType.Stun:
                if (descriptor.BlocksActions)
                {
                    isActionLockedThisTurn = true;
                }
                break;

            case TacticsStatusEffectType.Poison:
            case TacticsStatusEffectType.Fire:
                if (statusEffect.Potency > 0)
                {
                    ApplyDamage(statusEffect.Potency);
                }
                break;
        }
    }

    private void ApplyCharacterData(
        TacticsCharacterData data,
        TacticsCharacterProgressionSnapshot startingProgression,
        TacticsCharacterInventorySnapshot startingInventory)
    {
        if (data == null)
        {
            return;
        }

        characterData = data;
        moveSpeed = data.MoveSpeed;
        jumpDuration = data.JumpDuration;
        jumpArcHeight = data.JumpArcHeight;
        maxStepUp = data.MaxStepUp;
        maxStepDown = data.MaxStepDown;
        tileAnchorOffset = data.TileAnchorOffset;
        ExperienceToNextLevel = data.Team == TacticsUnitTeam.Player ? Mathf.Max(1, data.ExperienceToNextLevel) : 0;
        progression = data.Team == TacticsUnitTeam.Player
            ? ResolveProgression(startingProgression, data.CharacterId)
            : TacticsCharacterProgressionSnapshot.CreateDefault(data.CharacterId);
        ApplyInventorySnapshot(startingInventory, data.CharacterId);
        RefreshEffectiveStats(ref runtimeResources, refillResources: true);
        RebuildAbilities(data);
    }

    private TacticsCharacterProgressionSnapshot ResolveProgression(TacticsCharacterProgressionSnapshot startingProgression, string characterId)
    {
        TacticsCharacterProgressionSnapshot resolved = startingProgression.Sanitize();
        if (string.IsNullOrEmpty(resolved.CharacterId))
        {
            resolved = TacticsCharacterProgressionSnapshot.CreateDefault(characterId);
        }
        else if (!string.Equals(resolved.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
        {
            resolved = resolved.WithCharacterId(characterId);
        }

        return resolved.Sanitize();
    }

    private void ApplyProgressionSnapshot(
        TacticsCharacterProgressionSnapshot updatedProgression,
        bool preserveResourceRatios,
        bool emitNotification)
    {
        progression = ResolveProgression(updatedProgression, characterData != null ? characterData.CharacterId : string.Empty);
        TacticsCharacterRuntimeResources adjustedResources = runtimeResources;
        RefreshEffectiveStats(ref adjustedResources, refillResources: !preserveResourceRatios);
        runtimeResources = adjustedResources;

        if (emitNotification)
        {
            ProgressionChanged?.Invoke(this);
        }

        NotifyTurnStateChanged();
    }

    private void RefreshStatsFromStatusEffects()
    {
        TacticsCharacterRuntimeResources adjustedResources = runtimeResources;
        RefreshEffectiveStats(ref adjustedResources, refillResources: false);
        runtimeResources = adjustedResources;
    }

    private void RefreshEffectiveStats(ref TacticsCharacterRuntimeResources adjustedResources, bool refillResources)
    {
        TacticsCharacterDerivedStats previousDerivedStats = derivedStats;
        effectiveStats = progression.ApplyTo(characterData != null ? characterData.BaseStats : TacticsCharacterStats.Default());
        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            TacticsStatusEffectLibrary.TryApplyPersistentStatModifier(ref effectiveStats, activeStatusEffects[i]);
        }

        ApplyEquipmentBonusesToStats(ref effectiveStats);
        derivedStats = effectiveStats.CalculateDerivedStats();
        ApplyEquipmentBonusesToDerivedStats(ref derivedStats);
        CurrentLevel = progression.Level;
        CurrentExperience = progression.CurrentExperience;

        if (refillResources || previousDerivedStats.maxHitPoints <= 0)
        {
            adjustedResources = effectiveStats.CreateRuntimeResources();
            return;
        }

        adjustedResources.hitPoints = RecalculateCurrentResource(adjustedResources.hitPoints, previousDerivedStats.maxHitPoints, derivedStats.maxHitPoints);
        adjustedResources.stamina = RecalculateCurrentResource(adjustedResources.stamina, previousDerivedStats.maxStamina, derivedStats.maxStamina);
        adjustedResources.mana = RecalculateCurrentResource(adjustedResources.mana, previousDerivedStats.maxMana, derivedStats.maxMana);
    }

    private static bool AreEquivalent(TacticsCharacterProgressionSnapshot left, TacticsCharacterProgressionSnapshot right)
    {
        TacticsCharacterProgressionSnapshot lhs = left.Sanitize();
        TacticsCharacterProgressionSnapshot rhs = right.Sanitize();
        return string.Equals(lhs.CharacterId, rhs.CharacterId, StringComparison.OrdinalIgnoreCase) &&
               lhs.Level == rhs.Level &&
               lhs.CurrentExperience == rhs.CurrentExperience &&
               lhs.UnspentAttributePoints == rhs.UnspentAttributePoints &&
               lhs.allocatedPrimaryStats.stamina == rhs.allocatedPrimaryStats.stamina &&
               lhs.allocatedPrimaryStats.strength == rhs.allocatedPrimaryStats.strength &&
               lhs.allocatedPrimaryStats.agility == rhs.allocatedPrimaryStats.agility &&
               lhs.allocatedPrimaryStats.wisdom == rhs.allocatedPrimaryStats.wisdom &&
               lhs.allocatedPrimaryStats.intelligence == rhs.allocatedPrimaryStats.intelligence;
    }

    private static int RecalculateCurrentResource(int currentValue, int previousMax, int nextMax)
    {
        if (nextMax <= 0)
        {
            return 0;
        }

        if (previousMax <= 0)
        {
            return nextMax;
        }

        float normalizedValue = Mathf.Clamp01(currentValue / (float)previousMax);
        return Mathf.Clamp(Mathf.RoundToInt(nextMax * normalizedValue), 0, nextMax);
    }

    private Vector3 GetVisualAnchorPosition(float normalizedHeight, Vector3 fallbackOffset)
    {
        SpriteRenderer targetRenderer = characterAnimator != null ? characterAnimator.TargetRenderer : null;
        if (targetRenderer != null && targetRenderer.sprite != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return new Vector3(
                bounds.center.x,
                Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(normalizedHeight)),
                transform.position.z);
        }

        return transform.position + fallbackOffset;
    }

    public void SetPresentationVisible(bool isVisible)
    {
        IsPresentationVisible = isVisible;
    }

    private void RegisterWithTurnManager()
    {
        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        turnManager?.RegisterParticipant(this);
    }

    private void ResolveCharacterRegistry()
    {
        if (characterRegistry == null)
        {
            characterRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        }
    }

    private void RefreshCharacterRegistryState()
    {
        ResolveCharacterRegistry();
        characterRegistry?.Refresh(this);
    }

    private void NotifyTurnStateChanged()
    {
        TurnStateChanged?.Invoke(this);
    }

    private void RestoreEndTurnResources()
    {
        RestoreResourceByPercent(ref runtimeResources.stamina, MaxStamina, 0.1f);
        RestoreResourceByPercent(ref runtimeResources.mana, MaxMana, 0.1f);
    }

    private static void RestoreResourceByPercent(ref int currentValue, int maxValue, float percent)
    {
        if (maxValue <= 0 || percent <= 0f || currentValue >= maxValue)
        {
            return;
        }

        int restoreAmount = Mathf.Max(1, Mathf.CeilToInt(maxValue * percent));
        currentValue = Mathf.Min(maxValue, currentValue + restoreAmount);
    }

    private static int RestoreRuntimeResource(ref int currentValue, int maxValue, int amount)
    {
        if (maxValue <= 0 || amount <= 0 || currentValue >= maxValue)
        {
            return 0;
        }

        int restoredAmount = Mathf.Min(amount, maxValue - currentValue);
        currentValue = Mathf.Min(maxValue, currentValue + restoredAmount);
        return restoredAmount;
    }

    private void RebuildAbilities(TacticsCharacterData data)
    {
        abilities.Clear();

        if (data != null)
        {
            IReadOnlyList<TacticsAbilityDefinition> configuredAbilities = data.StartingAbilities;
            for (int i = 0; i < configuredAbilities.Count; i++)
            {
                AddAbilityIfMissing(configuredAbilities[i]);
            }
        }

    }

    private void AddAbilityIfMissing(TacticsAbilityDefinition ability)
    {
        if (ability == null || abilities.Contains(ability))
        {
            return;
        }

        abilities.Add(ability);
    }

    private void ApplyInventorySnapshot(TacticsCharacterInventorySnapshot snapshot, string characterId)
    {
        inventoryItems.Clear();
        equippedItemsBySlot.Clear();

        TacticsCharacterInventorySnapshot sanitized = snapshot.WithCharacterId(characterId).Sanitize();
        List<TacticsInventoryItemSaveData> normalizedItems = NormalizeInventoryItems(sanitized.items);
        for (int i = 0; i < sanitized.equippedItems.Count; i++)
        {
            TacticsEquippedItemSaveData equipped = sanitized.equippedItems[i];
            if (!TryResolveEquippedInventoryItem(equipped, normalizedItems, out TacticsInventoryItemSaveData resolvedItem) ||
                !TacticsItemCatalogResources.TryGetItem(resolvedItem.itemId, out TacticsItemDefinition definition) ||
                definition is not TacticsEquipmentItemDefinition equipment ||
                equipment.Slot != equipped.slot)
            {
                continue;
            }

            RemoveInventoryItemFromList(normalizedItems, resolvedItem.instanceId);
            equippedItemsBySlot[equipped.slot] = resolvedItem;
        }

        for (int i = 0; i < normalizedItems.Count; i++)
        {
            inventoryItems.Add(normalizedItems[i]);
        }
    }

    private void ApplyEquipmentBonusesToStats(ref TacticsCharacterStats stats)
    {
        foreach (TacticsInventoryItemSaveData item in equippedItemsBySlot.Values)
        {
            if (item == null ||
                !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
                definition is not TacticsEquipmentItemDefinition equipment)
            {
                continue;
            }

            TacticsPrimaryStatBonuses primaryBonuses = equipment.PrimaryStatBonuses;
            primaryBonuses.Apply(ref stats);
            equipment.DerivedStatBonuses.ApplyToBaseStats(ref stats);
        }
    }

    private void ApplyEquipmentBonusesToDerivedStats(ref TacticsCharacterDerivedStats stats)
    {
        foreach (TacticsInventoryItemSaveData item in equippedItemsBySlot.Values)
        {
            if (item == null ||
                !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
                definition is not TacticsEquipmentItemDefinition equipment)
            {
                continue;
            }

            equipment.DerivedStatBonuses.ApplyToDerivedStats(ref stats);
            if (equipment is not TacticsWeaponItemDefinition weapon)
            {
                continue;
            }

            int scalingBonus = weapon.EvaluateDamageScalingBonus(this);
            if (weapon.DamageType == TacticsAbilityDamageType.Magic)
            {
                stats.baseMagicDamageMin = Mathf.Max(0, stats.baseMagicDamageMin + weapon.BaseDamageMinBonus + scalingBonus);
                stats.baseMagicDamageMax = Mathf.Max(stats.baseMagicDamageMin, stats.baseMagicDamageMax + weapon.BaseDamageMaxBonus + scalingBonus);
            }
            else
            {
                stats.baseMeleeDamageMin = Mathf.Max(0, stats.baseMeleeDamageMin + weapon.BaseDamageMinBonus + scalingBonus);
                stats.baseMeleeDamageMax = Mathf.Max(stats.baseMeleeDamageMin, stats.baseMeleeDamageMax + weapon.BaseDamageMaxBonus + scalingBonus);
            }
        }
    }

    private TacticsInventoryItemSaveData FindInventoryItem(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            TacticsInventoryItemSaveData item = inventoryItems[i];
            if (item != null &&
                string.Equals(item.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private TacticsInventoryItemSaveData FindConsumableStack(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            TacticsInventoryItemSaveData item = inventoryItems[i];
            if (item == null ||
                !string.Equals(item.itemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                !TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) ||
                definition is not TacticsConsumableItemDefinition)
            {
                continue;
            }

            return item;
        }

        return null;
    }

    private bool RemoveInventoryItem(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        bool removed = false;
        for (int i = inventoryItems.Count - 1; i >= 0; i--)
        {
            TacticsInventoryItemSaveData item = inventoryItems[i];
            if (item == null ||
                !string.Equals(item.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            inventoryItems.RemoveAt(i);
            removed = true;
        }

        if (!removed)
        {
            return false;
        }

        RefreshStatsFromStatusEffects();
        return true;
    }

    private bool TryResolveEquippedInventoryItem(
        TacticsEquippedItemSaveData equipped,
        List<TacticsInventoryItemSaveData> normalizedItems,
        out TacticsInventoryItemSaveData resolvedItem)
    {
        resolvedItem = null;
        if (equipped == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(equipped.itemId))
        {
            resolvedItem = new TacticsInventoryItemSaveData
            {
                instanceId = equipped.instanceId,
                itemId = equipped.itemId,
                quantity = Mathf.Max(1, equipped.quantity)
            };
            return true;
        }

        resolvedItem = FindInventoryItem(normalizedItems, equipped.instanceId)?.Clone();
        return resolvedItem != null;
    }

    private bool TransferEquippedItemToInventory(TacticsEquipmentSlot slot)
    {
        if (!equippedItemsBySlot.TryGetValue(slot, out TacticsInventoryItemSaveData equippedItem) || equippedItem == null)
        {
            return false;
        }

        equippedItemsBySlot.Remove(slot);
        inventoryItems.Add(equippedItem);
        return true;
    }

    private bool ContainsItemInstanceId(string instanceId)
    {
        return FindInventoryItem(instanceId) != null || FindEquippedItemByInstanceId(instanceId) != null;
    }

    private TacticsInventoryItemSaveData FindEquippedItemByInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        foreach (TacticsInventoryItemSaveData item in equippedItemsBySlot.Values)
        {
            if (item != null &&
                string.Equals(item.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static TacticsInventoryItemSaveData FindInventoryItem(List<TacticsInventoryItemSaveData> items, string instanceId)
    {
        if (items == null || string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            TacticsInventoryItemSaveData item = items[i];
            if (item != null &&
                string.Equals(item.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static bool RemoveInventoryItemFromList(List<TacticsInventoryItemSaveData> items, string instanceId)
    {
        if (items == null || string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            TacticsInventoryItemSaveData item = items[i];
            if (item != null &&
                string.Equals(item.instanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private List<TacticsInventoryItemSaveData> NormalizeInventoryItems(List<TacticsInventoryItemSaveData> items)
    {
        List<TacticsInventoryItemSaveData> normalizedItems = new List<TacticsInventoryItemSaveData>();
        Dictionary<string, TacticsInventoryItemSaveData> consumableStacks =
            new Dictionary<string, TacticsInventoryItemSaveData>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (items == null)
        {
            return normalizedItems;
        }

        for (int i = 0; i < items.Count; i++)
        {
            TacticsInventoryItemSaveData cloned = items[i]?.Clone();
            if (cloned == null ||
                string.IsNullOrWhiteSpace(cloned.itemId) ||
                !TacticsItemCatalogResources.TryGetItem(cloned.itemId, out TacticsItemDefinition definition))
            {
                continue;
            }

            cloned.quantity = Mathf.Max(1, cloned.quantity);
            if (definition is TacticsConsumableItemDefinition)
            {
                if (consumableStacks.TryGetValue(cloned.itemId, out TacticsInventoryItemSaveData existingStack))
                {
                    existingStack.quantity = AddInventoryQuantity(existingStack.quantity, cloned.quantity);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cloned.instanceId) || !seenInstanceIds.Add(cloned.instanceId))
                {
                    cloned.instanceId = CreateUniqueInventoryInstanceId(seenInstanceIds);
                }

                consumableStacks[cloned.itemId] = cloned;
                normalizedItems.Add(cloned);
                continue;
            }

            if (string.IsNullOrWhiteSpace(cloned.instanceId) || !seenInstanceIds.Add(cloned.instanceId))
            {
                continue;
            }

            cloned.quantity = 1;
            normalizedItems.Add(cloned);
        }

        return normalizedItems;
    }

    private string CreateUniqueInventoryInstanceId(HashSet<string> reservedInstanceIds = null)
    {
        HashSet<string> usedInstanceIds = reservedInstanceIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (reservedInstanceIds == null)
        {
            for (int i = 0; i < inventoryItems.Count; i++)
            {
                TacticsInventoryItemSaveData item = inventoryItems[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.instanceId))
                {
                    usedInstanceIds.Add(item.instanceId);
                }
            }

            foreach (TacticsInventoryItemSaveData item in equippedItemsBySlot.Values)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.instanceId))
                {
                    usedInstanceIds.Add(item.instanceId);
                }
            }
        }

        string instanceId;
        do
        {
            instanceId = Guid.NewGuid().ToString("N");
        }
        while (!usedInstanceIds.Add(instanceId));

        return instanceId;
    }

    private static int AddInventoryQuantity(int currentQuantity, int quantityToAdd)
    {
        long total = (long)Mathf.Max(1, currentQuantity) + Mathf.Max(1, quantityToAdd);
        return (int)Math.Min(int.MaxValue, total);
    }

    private void RaiseInventoryItemAdded(
        TacticsInventoryItemSaveData itemData,
        TacticsItemDefinition itemDefinition,
        int quantityAdded,
        bool mergedIntoExistingStack)
    {
        if (itemData == null || itemDefinition == null)
        {
            return;
        }

        InventoryItemAdded?.Invoke(
            this,
            new TacticsInventoryItemAddedEvent(
                itemData,
                itemDefinition,
                quantityAdded,
                mergedIntoExistingStack));
    }

    private string BuildFallbackRuntimeCharacterId(TacticsCharacterData data, TacticsCharacterDefinition definition)
    {
        string baseId = data != null
            ? data.CharacterId
            : (definition != null ? definition.CharacterId : name);
        baseId = string.IsNullOrWhiteSpace(baseId) ? "character" : baseId.Trim();
        return $"{baseId}_{GetInstanceID()}";
    }

    private string BuildTurnOrderKey()
    {
        if (!string.IsNullOrWhiteSpace(RuntimeCharacterId))
        {
            return RuntimeCharacterId;
        }

        string characterId = characterData != null
            ? characterData.CharacterId
            : characterDefinition != null
                ? characterDefinition.CharacterId
                : string.Empty;
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            return characterId.Trim();
        }

        return string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName.Trim();
    }

    private void HandleDefeat(TacticsCharacterController defeatingCharacter)
    {
        if (Team == TacticsUnitTeam.Enemy && defeatingCharacter != null)
        {
            int experienceReward = characterData != null ? characterData.RollExperienceReward() : 0;
            if (experienceReward > 0 && defeatingCharacter.TryAwardExperience(experienceReward))
            {
                TacticsCombatTextSystem.ShowExperienceReward(defeatingCharacter, experienceReward);
            }
        }

        StopMovementImmediately();
        isPerformingAction = false;
        isActionLockedThisTurn = false;
        activeStatusEffects.Clear();
        RefreshStatsFromStatusEffects();

        IsTurnActive = false;
        HasMovedThisTurn = true;
        HasActedThisTurn = true;
        SetSelected(false);
        SetTargeted(false);
        characterAnimator?.SetTurnHighlight(false);
        NotifyTurnStateChanged();
        characterRegistry?.Refresh(this);
        gameObject.SetActive(false);
    }

    private void StopMovementImmediately()
    {
        if (movementRoutine == null)
        {
            return;
        }

        StopCoroutine(movementRoutine);
        movementRoutine = null;
        SnapToTile(GridPosition);
        characterAnimator?.SetIdle(currentDirection);
    }
}

public readonly struct TacticsAbilityCostPayment
{
    public static readonly TacticsAbilityCostPayment None = new(TacticsAbilityResourceType.None, 0);

    public TacticsAbilityCostPayment(TacticsAbilityResourceType resourceType, int amount)
    {
        ResourceType = resourceType;
        Amount = resourceType == TacticsAbilityResourceType.Movement
            ? 1
            : Mathf.Max(0, amount);
    }

    public TacticsAbilityResourceType ResourceType { get; }
    public int Amount { get; }
    public bool HasCost => ResourceType != TacticsAbilityResourceType.None && Amount > 0;
    public bool UsesMovement => ResourceType == TacticsAbilityResourceType.Movement;
}
