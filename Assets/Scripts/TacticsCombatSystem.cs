using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsCombatSystem : MonoBehaviour
{
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsAbilityCatalog abilityCatalog;
    [SerializeField] private TacticsCharacterRegistry characterRegistry;

    private readonly Dictionary<TacticsAbilityEffectKind, ITacticsAbilityEffectProcessor> effectProcessors = new();
    private readonly TacticsApplyStatusEffectProcessor statusEffectProcessor = new();
    private readonly List<TacticsCharacterController> reusableAreaTargets = new();
    private readonly List<Vector2Int> reusableTargetTiles = new();
    private readonly List<Vector2Int> reusableTargetableTiles = new();
    private readonly List<Vector2Int> reusableThrowCandidateTiles = new();
    private readonly List<Vector2Int> reusableThrowDestinationTiles = new();
    private readonly List<Vector2Int> reusableAreaTiles = new();
    private readonly List<TacticsCharacterController> reusableCharacterBuffer = new();
    private readonly List<TacticsCharacterController> reusableTauntBuffer = new();
    private readonly List<TacticsKnockbackPlan> reusableKnockbackPlans = new();
    private readonly List<TacticsThrowPlan> reusableThrowPlans = new();
    private readonly List<TacticsCharacterController> reusableTargetCandidateBuffer = new();
    private readonly Dictionary<TacticsCharacterController, TacticsAttackResolution> reusableAttackResolutions = new();
    private Coroutine resolveRoutine;

    public event Action StateChanged;

    public TacticsCombatState State { get; private set; } = TacticsCombatState.Idle;
    public TacticsCharacterController TargetingCharacter { get; private set; }
    public TacticsAbilityDefinition TargetingAbility { get; private set; }

    private void Awake()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        }

        if (characterRegistry == null)
        {
            characterRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        }

        abilityCatalog ??= TacticsAbilityCatalogResources.LoadCatalog();
        effectProcessors[TacticsAbilityEffectKind.DealDamage] = new TacticsDealDamageEffectProcessor();
        effectProcessors[TacticsAbilityEffectKind.RestoreHitPoints] = new TacticsRestoreHitPointsEffectProcessor();
        effectProcessors[TacticsAbilityEffectKind.RestoreResource] = new TacticsRestoreResourceEffectProcessor();
    }

    public void AssignMapGenerator(ProceduralIsometricMapGenerator generator)
    {
        mapGenerator = generator;
    }

    public void AssignCharacterRegistry(TacticsCharacterRegistry registry)
    {
        characterRegistry = registry;
    }

    public TacticsAbilityDefinition GetDefaultAttackAbility()
    {
        return abilityCatalog != null ? abilityCatalog.DefaultAttackAbility : null;
    }

    public bool BeginTargeting(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        if (!CanUseAbility(source, ability))
        {
            return false;
        }

        TargetingCharacter = source;
        TargetingAbility = ability;
        SetState(TacticsCombatState.TargetingAbility);
        return true;
    }

    public void CancelTargeting()
    {
        if (State != TacticsCombatState.TargetingAbility)
        {
            return;
        }

        TargetingCharacter = null;
        TargetingAbility = null;
        SetState(TacticsCombatState.Idle);
    }

    public IReadOnlyList<Vector2Int> GetValidTargetTiles(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        reusableTargetTiles.Clear();

        if (!CanUseAbility(source, ability))
        {
            return reusableTargetTiles;
        }

        IReadOnlyList<Vector2Int> targetableTiles = GetTargetableTiles(source, ability);
        for (int i = 0; i < targetableTiles.Count; i++)
        {
            Vector2Int targetTile = targetableTiles[i];
            if (!HasValidTargetsAtTile(source, ability, targetTile))
            {
                continue;
            }

            reusableTargetTiles.Add(targetTile);
        }

        return reusableTargetTiles;
    }

    public IReadOnlyList<Vector2Int> GetTargetableTiles(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        return GetTargetableTilesFromTile(source, source != null ? source.GridPosition : default, ability, reusableTargetableTiles);
    }

    private IReadOnlyList<Vector2Int> GetTargetableTilesFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        List<Vector2Int> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();

        if (!CanUseAbility(source, ability) || mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return results;
        }

        for (int x = 0; x < mapGenerator.Width; x++)
        {
            for (int y = 0; y < mapGenerator.Length; y++)
            {
                if (!mapGenerator.IsTraversable(x, y))
                {
                    continue;
                }

                Vector2Int targetTile = new Vector2Int(x, y);
                if (CanTargetTile(source, sourceTile, ability, targetTile))
                {
                    results.Add(targetTile);
                }
            }
        }

        return results;
    }

    public bool TryExecuteTargetAt(Vector2Int targetTile)
    {
        if (State != TacticsCombatState.TargetingAbility)
        {
            return false;
        }

        return TryUseAbility(TargetingCharacter, TargetingAbility, targetTile);
    }

    public bool TryUseAbility(TacticsCharacterController source, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        return TryUseAbility(source, ability, targetTile, null);
    }

    public bool TryUseAbility(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        Vector2Int? throwDestination)
    {
        if (State == TacticsCombatState.ResolvingAbility || resolveRoutine != null)
        {
            return false;
        }

        if (!CanUseAbility(source, ability))
        {
            return false;
        }

        if (!CanTargetTile(source, source.GridPosition, ability, targetTile))
        {
            return false;
        }

        if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
            !IsValidTarget(source, source.GridPosition, ability, FindCharacterAt(targetTile)))
        {
            return false;
        }

        List<TacticsCharacterController> affectedTargets = GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
        if (affectedTargets.Count == 0)
        {
            return false;
        }

        if (!source.TryGetAbilityCostPayment(ability, out TacticsAbilityCostPayment payment))
        {
            return false;
        }

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            ability,
            targetTile,
            new List<TacticsCharacterController>(affectedTargets),
            payment,
            throwDestination);

        if (!HasApplicableEffect(context))
        {
            RestoreIdleState();
            return false;
        }

        SetState(TacticsCombatState.ResolvingAbility);
        resolveRoutine = StartCoroutine(ResolveAbilityRoutine(context));
        return true;
    }

    public bool ApplyReplicatedAbility(TacticsCharacterController source, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        return ApplyReplicatedAbility(source, ability, targetTile, null);
    }

    public bool ApplyReplicatedAbility(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        Vector2Int? throwDestination)
    {
        if (State == TacticsCombatState.ResolvingAbility || resolveRoutine != null)
        {
            return false;
        }

        if (!CanResolveReplicatedAbility(source, ability, targetTile, throwDestination, out List<TacticsCharacterController> affectedTargets))
        {
            return false;
        }

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            ability,
            targetTile,
            new List<TacticsCharacterController>(affectedTargets),
            ResolveReplicatedAbilityCostPayment(source, ability),
            throwDestination);

        if (!HasApplicableEffect(context))
        {
            RestoreIdleState();
            return false;
        }

        SetState(TacticsCombatState.ResolvingAbility);
        resolveRoutine = StartCoroutine(ResolveAbilityRoutine(context));
        return true;
    }

    public bool CanTargetFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        TacticsCharacterController target)
    {
        return IsValidTarget(source, sourceTile, ability, target);
    }

    public bool CanUseAbility(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        return CanUseAbility(source, ability, source != null && source.HasMovementAvailableForAbilityCost);
    }

    public bool CanUseAbility(TacticsCharacterController source, TacticsAbilityDefinition ability, bool movementAvailable)
    {
        return source != null &&
               ability != null &&
               source.CanUseAbilitiesThisTurn &&
               source.CanPayAbilityCost(ability, movementAvailable) &&
               source.isActiveAndEnabled;
    }

    public IReadOnlyList<TacticsCharacterController> GetPreviewTargets(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        reusableAreaTargets.Clear();

        if (!CanUseAbility(source, ability) ||
            !CanTargetTile(source, source != null ? source.GridPosition : default, ability, targetTile) ||
            (ability != null &&
             ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
             !IsValidTarget(source, source != null ? source.GridPosition : default, ability, FindCharacterAt(targetTile))))
        {
            return reusableAreaTargets;
        }

        return GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
    }

    public bool CanTargetTileFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        return CanTargetTile(source, sourceTile, ability, targetTile);
    }

    public IReadOnlyList<TacticsCharacterController> GetPreviewTargetsFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        return GetPreviewTargetsFromTile(
            source,
            sourceTile,
            ability,
            targetTile,
            source != null && source.HasMovementAvailableForAbilityCost);
    }

    public IReadOnlyList<TacticsCharacterController> GetPreviewTargetsFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        bool movementAvailable)
    {
        reusableAreaTargets.Clear();

        if (!CanUseAbility(source, ability, movementAvailable) ||
            !CanTargetTile(source, sourceTile, ability, targetTile) ||
            (ability != null &&
             ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
             !IsValidTarget(source, sourceTile, ability, FindCharacterAt(targetTile))))
        {
            return reusableAreaTargets;
        }

        return GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
    }

    public IReadOnlyList<TacticsCharacterController> GetPrimaryTargetCandidatesFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        List<TacticsCharacterController> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();
        if (source == null || ability == null || characterRegistry == null)
        {
            return results;
        }

        GetPotentialTargetsForAbility(source, ability, reusableTargetCandidateBuffer);
        for (int i = 0; i < reusableTargetCandidateBuffer.Count; i++)
        {
            TacticsCharacterController candidate = reusableTargetCandidateBuffer[i];
            if (IsValidTarget(source, sourceTile, ability, candidate))
            {
                results.Add(candidate);
            }
        }

        return results;
    }

    public IReadOnlyList<Vector2Int> GetAreaTiles(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        reusableAreaTiles.Clear();

        if (!CanUseAbility(source, ability) || mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return reusableAreaTiles;
        }

        if (!ability.UsesAreaOfEffect)
        {
            reusableAreaTiles.Add(targetTile);
            return reusableAreaTiles;
        }

        Vector2Int areaCenter = ability.RangeType == TacticsAbilityRangeType.SurroundingAoE
            ? source.GridPosition
            : targetTile;
        int areaRadius = ability.AreaOfEffectRadius;

        for (int x = areaCenter.x - areaRadius; x <= areaCenter.x + areaRadius; x++)
        {
            for (int y = areaCenter.y - areaRadius; y <= areaCenter.y + areaRadius; y++)
            {
                if (x < 0 || x >= mapGenerator.Width || y < 0 || y >= mapGenerator.Length)
                {
                    continue;
                }

                if (!mapGenerator.IsTraversable(x, y))
                {
                    continue;
                }

                if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
                    !AreTilesOnSameElevation(source.GridPosition, new Vector2Int(x, y)))
                {
                    continue;
                }

                reusableAreaTiles.Add(new Vector2Int(x, y));
            }
        }

        return reusableAreaTiles;
    }

    public bool TryGetKnockbackDestination(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        out Vector2Int destination)
    {
        return TryGetKnockbackDestination(source, sourceTile, target, ability, null, null, out destination);
    }

    public IReadOnlyList<Vector2Int> GetValidThrowDestinations(
        TacticsCharacterController source,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        List<Vector2Int> results)
    {
        return GetValidThrowDestinationsFromTile(source, source != null ? source.GridPosition : default, target, ability, results);
    }

    public IReadOnlyList<Vector2Int> GetValidThrowDestinationsFromTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        List<Vector2Int> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();
        if (source == null ||
            target == null ||
            ability == null ||
            !ability.AppliesThrowing ||
            mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            !target.isActiveAndEnabled ||
            !target.IsAlive ||
            !IsValidTarget(source, sourceTile, ability, target))
        {
            return results;
        }

        int minX = Mathf.Max(0, sourceTile.x - ability.Range);
        int maxX = Mathf.Min(mapGenerator.Width - 1, sourceTile.x + ability.Range);
        int minY = Mathf.Max(0, sourceTile.y - ability.Range);
        int maxY = Mathf.Min(mapGenerator.Length - 1, sourceTile.y + ability.Range);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int candidateTile = new Vector2Int(x, y);
                if (candidateTile == target.GridPosition ||
                    !mapGenerator.IsTraversable(x, y) ||
                    !CanTargetTile(source, sourceTile, ability, candidateTile) ||
                    !CanOccupyThrowDestination(source, target, candidateTile))
                {
                    continue;
                }

                results.Add(candidateTile);
            }
        }

        return results;
    }

    private bool IsValidTarget(TacticsCharacterController source, TacticsAbilityDefinition ability, TacticsCharacterController target)
    {
        return IsValidTarget(source, source != null ? source.GridPosition : default, ability, target);
    }

    private bool IsValidTarget(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        TacticsCharacterController target)
    {
        if (source == null || ability == null || target == null)
        {
            return false;
        }

        if (ReferenceEquals(source, target))
        {
            return IsTargetRelationshipValid(source, ability, target);
        }

        if (!target.isActiveAndEnabled || !target.IsAlive)
        {
            return false;
        }

        if (!CanTargetTile(source, sourceTile, ability, target.GridPosition))
        {
            return false;
        }

        if (!IsTargetRelationshipValid(source, ability, target))
        {
            return false;
        }

        return !IsBlockedByTaunt(source, sourceTile, ability, target);
    }

    private TacticsCharacterController FindCharacterAt(Vector2Int tile)
    {
        return characterRegistry != null &&
               characterRegistry.TryGetCharacterAtTile(tile, out TacticsCharacterController character)
            ? character
            : null;
    }

    private bool HasValidTargetsAtTile(TacticsCharacterController source, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        if (ability != null &&
            ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
            !IsValidTarget(source, source != null ? source.GridPosition : default, ability, FindCharacterAt(targetTile)))
        {
            return false;
        }

        List<TacticsCharacterController> affectedTargets = GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
        if (affectedTargets.Count == 0)
        {
            return false;
        }

        if (!ability.AppliesThrowing)
        {
            return true;
        }

        TacticsCharacterController directTarget = FindCharacterAt(targetTile);
        return GetValidThrowDestinations(source, directTarget, ability, reusableThrowDestinationTiles).Count > 0;
    }

    private List<TacticsCharacterController> GetAffectedTargets(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        List<TacticsCharacterController> results)
    {
        results.Clear();

        if (source == null || ability == null)
        {
            return results;
        }

        if (!ability.UsesAreaOfEffect)
        {
            TacticsCharacterController directTarget = FindCharacterAt(targetTile);
            if (IsValidTarget(source, ability, directTarget))
            {
                results.Add(directTarget);
            }

            return results;
        }

        int areaRadius = ability.AreaOfEffectRadius;
        Vector2Int areaCenter = ability.RangeType == TacticsAbilityRangeType.SurroundingAoE
            ? source.GridPosition
            : targetTile;
        int sourceElevation = TryGetTileElevation(source.GridPosition, out int resolvedSourceElevation)
            ? resolvedSourceElevation
            : int.MinValue;
        if (characterRegistry == null)
        {
            return results;
        }

        GetPotentialTargetsForAbility(source, ability, reusableCharacterBuffer);
        for (int i = 0; i < reusableCharacterBuffer.Count; i++)
        {
            TacticsCharacterController target = reusableCharacterBuffer[i];
            if (!CanAffectTarget(source, ability, target))
            {
                continue;
            }

            if (Mathf.Abs(target.GridPosition.x - areaCenter.x) > areaRadius ||
                Mathf.Abs(target.GridPosition.y - areaCenter.y) > areaRadius)
            {
                continue;
            }

            if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
                (!TryGetTileElevation(target.GridPosition, out int targetElevation) || targetElevation != sourceElevation))
            {
                continue;
            }

            results.Add(target);
        }

        return results;
    }

    private static bool CanAffectTarget(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsCharacterController target)
    {
        if (source == null || ability == null || target == null || ReferenceEquals(source, target))
        {
            return ability != null &&
                   source != null &&
                   target != null &&
                   ReferenceEquals(source, target) &&
                   ability.TargetRule is TacticsAbilityTargetRule.AlliedUnitOrSelf or TacticsAbilityTargetRule.Self;
        }

        if (!target.isActiveAndEnabled || !target.IsAlive)
        {
            return false;
        }

        return ability.TargetRule switch
        {
            TacticsAbilityTargetRule.HostileUnit => source.Team != target.Team,
            TacticsAbilityTargetRule.AlliedUnit => source.Team == target.Team && !ReferenceEquals(source, target),
            TacticsAbilityTargetRule.AlliedUnitOrSelf => source.Team == target.Team,
            TacticsAbilityTargetRule.Self => ReferenceEquals(source, target),
            _ => false
        };
    }

    private static bool IsTargetRelationshipValid(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsCharacterController target)
    {
        if (source == null || ability == null || target == null)
        {
            return false;
        }

        return ability.TargetRule switch
        {
            TacticsAbilityTargetRule.HostileUnit => source.Team != target.Team,
            TacticsAbilityTargetRule.AlliedUnit => source.Team == target.Team && !ReferenceEquals(source, target),
            TacticsAbilityTargetRule.AlliedUnitOrSelf => source.Team == target.Team,
            TacticsAbilityTargetRule.Self => ReferenceEquals(source, target),
            _ => false
        };
    }

    private bool IsBlockedByTaunt(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        TacticsCharacterController target)
    {
        if (source == null ||
            ability == null ||
            target == null ||
            ability.TargetRule != TacticsAbilityTargetRule.HostileUnit ||
            target.IsTaunting ||
            characterRegistry == null)
        {
            return false;
        }

        characterRegistry.GetHostileCharacters(source, reusableTauntBuffer);
        for (int i = 0; i < reusableTauntBuffer.Count; i++)
        {
            TacticsCharacterController candidate = reusableTauntBuffer[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.IsAlive ||
                !candidate.IsTaunting)
            {
                continue;
            }

            if (CanTargetTile(source, sourceTile, ability, candidate.GridPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanTargetTile(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        if (source == null || ability == null || mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return false;
        }

        if (!mapGenerator.IsTraversable(targetTile.x, targetTile.y))
        {
            return false;
        }

        int distance = GetTileDistance(sourceTile, targetTile);
        if (distance <= 0)
        {
            return ability.TargetRule is TacticsAbilityTargetRule.AlliedUnitOrSelf or TacticsAbilityTargetRule.Self &&
                   targetTile == sourceTile;
        }

        switch (ability.RangeType)
        {
            case TacticsAbilityRangeType.Melee:
                return distance == 1 && AreTilesOnSameElevation(sourceTile, targetTile);

            case TacticsAbilityRangeType.Ranged:
            case TacticsAbilityRangeType.RangedAoE:
                return distance <= ability.Range && HasLineOfSight(source, sourceTile, targetTile);

            case TacticsAbilityRangeType.AbsoluteRanged:
            case TacticsAbilityRangeType.AbsoluteAoE:
                return distance <= ability.Range;

            case TacticsAbilityRangeType.SurroundingAoE:
                return distance == 1 && AreTilesOnSameElevation(sourceTile, targetTile);

            default:
                return false;
        }
    }

    private bool HasLineOfSight(TacticsCharacterController source, Vector2Int sourceTile, Vector2Int targetTile)
    {
        if (mapGenerator == null)
        {
            return false;
        }

        int x = sourceTile.x;
        int y = sourceTile.y;
        int deltaX = Mathf.Abs(targetTile.x - sourceTile.x);
        int deltaY = Mathf.Abs(targetTile.y - sourceTile.y);
        int stepX = sourceTile.x < targetTile.x ? 1 : -1;
        int stepY = sourceTile.y < targetTile.y ? 1 : -1;
        int error = deltaX - deltaY;

        if (deltaX + deltaY <= 1)
        {
            return true;
        }

        int sourceElevation = mapGenerator.GetTileElevation(sourceTile.x, sourceTile.y);
        int targetElevation = mapGenerator.GetTileElevation(targetTile.x, targetTile.y);
        int blockingElevationThreshold = Mathf.Min(sourceElevation, targetElevation);

        while (true)
        {
            int doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            if (x == targetTile.x && y == targetTile.y)
            {
                break;
            }

            Vector2Int tile = new Vector2Int(x, y);
            int tileElevation = mapGenerator.GetTileElevation(tile.x, tile.y);

            // Ranged line of sight behaves like same-level visibility plus the ability
            // to see onto a single ledge face one elevation higher. Intermediate tiles
            // only block when they rise above the lower endpoint elevation, which makes
            // a one-tile ledge targetable while thicker raised plateaus become walls.
            if (tileElevation > blockingElevationThreshold)
            {
                return false;
            }

            if (TacticsTileBlockerUtility.IsBlockingTile(tile))
            {
                return false;
            }

            TacticsCharacterController blockingCharacter = FindCharacterAt(tile);
            if (blockingCharacter != null &&
                blockingCharacter.isActiveAndEnabled &&
                blockingCharacter.IsAlive &&
                !ReferenceEquals(blockingCharacter, source))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetTileDistance(Vector2Int source, Vector2Int target)
    {
        return Mathf.Abs(source.x - target.x) + Mathf.Abs(source.y - target.y);
    }

    private bool AreTilesOnSameElevation(Vector2Int firstTile, Vector2Int secondTile)
    {
        return TryGetTileElevation(firstTile, out int firstElevation) &&
               TryGetTileElevation(secondTile, out int secondElevation) &&
               firstElevation == secondElevation;
    }

    private bool TryGetTileElevation(Vector2Int tile, out int elevation)
    {
        elevation = 0;
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return false;
        }

        if (tile.x < 0 || tile.x >= mapGenerator.Width || tile.y < 0 || tile.y >= mapGenerator.Length)
        {
            return false;
        }

        elevation = mapGenerator.GetTileElevation(tile.x, tile.y);
        return true;
    }

    private void GetPotentialTargetsForAbility(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        List<TacticsCharacterController> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();
        if (source == null || ability == null || characterRegistry == null)
        {
            return;
        }

        switch (ability.TargetRule)
        {
            case TacticsAbilityTargetRule.HostileUnit:
                characterRegistry.GetHostileCharacters(source, results);
                break;

            case TacticsAbilityTargetRule.AlliedUnit:
                characterRegistry.GetAlliedCharacters(source, includeSelf: false, results);
                break;

            case TacticsAbilityTargetRule.AlliedUnitOrSelf:
                characterRegistry.GetAlliedCharacters(source, includeSelf: true, results);
                break;

            case TacticsAbilityTargetRule.Self:
                results.Add(source);
                break;
        }
    }

    private void RestoreIdleState()
    {
        resolveRoutine = null;
        TargetingCharacter = null;
        TargetingAbility = null;
        SetState(TacticsCombatState.Idle);
    }

    private bool HasApplicableEffect(TacticsAbilityExecutionContext context)
    {
        if (TryBuildThrowPlans(context).Count > 0)
        {
            return true;
        }

        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = context.Ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            if (!effectProcessors.TryGetValue(effect.EffectKind, out ITacticsAbilityEffectProcessor processor))
            {
                continue;
            }

            if (processor.CanApply(context, effect))
            {
                return true;
            }
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = context.Ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffectProcessor.CanApply(context, statusEffects[i]))
            {
                return true;
            }
        }

        return BuildKnockbackPlans(context).Count > 0;
    }

    private List<TacticsKnockbackPlan> BuildKnockbackPlans(TacticsAbilityExecutionContext context)
    {
        reusableKnockbackPlans.Clear();
        if (context.Source == null ||
            context.Ability == null ||
            context.Ability.AppliesThrowing ||
            !context.Ability.AppliesKnockback ||
            context.Targets == null ||
            context.Targets.Count == 0)
        {
            return reusableKnockbackPlans;
        }

        HashSet<TacticsCharacterController> displacedTargets = new();
        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target != null && target.isActiveAndEnabled && target.IsAlive)
            {
                displacedTargets.Add(target);
            }
        }

        HashSet<Vector2Int> reservedDestinations = new();
        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            if (!TryGetKnockbackDestination(
                    context.Source,
                    context.Source.GridPosition,
                    target,
                    context.Ability,
                    reservedDestinations,
                    displacedTargets,
                    out Vector2Int destination))
            {
                continue;
            }

            if (!TacticsKnockbackUtility.TryGetStepDelta(context.Source.GridPosition, target.GridPosition, out Vector2Int stepDelta))
            {
                continue;
            }

            TacticsMovementDirection travelDirection = TacticsKnockbackUtility.GetMovementDirection(stepDelta);
            reservedDestinations.Add(destination);
            reusableKnockbackPlans.Add(new TacticsKnockbackPlan(
                target,
                destination,
                travelDirection,
                TacticsKnockbackUtility.GetOppositeDirection(travelDirection),
                context.Ability.Knockback));
        }

        return reusableKnockbackPlans;
    }

    private List<TacticsThrowPlan> TryBuildThrowPlans(TacticsAbilityExecutionContext context)
    {
        reusableThrowPlans.Clear();
        if (context.Source == null ||
            context.Ability == null ||
            !context.Ability.AppliesThrowing ||
            context.Targets == null ||
            context.Targets.Count == 0 ||
            !context.ThrowDestination.HasValue)
        {
            return reusableThrowPlans;
        }

        TacticsCharacterController target = FindCharacterAt(context.TargetTile);
        if (target == null || !ReferenceEquals(target, context.Targets[0]))
        {
            target = context.Targets[0];
        }

        if (!context.CanApplyEffectsTo(target))
        {
            return reusableThrowPlans;
        }

        if (!TryBuildThrowPlan(
                context.Source,
                context.Source.GridPosition,
                target,
                context.Ability,
                context.ThrowDestination.Value,
                out TacticsThrowPlan throwPlan))
        {
            return reusableThrowPlans;
        }

        reusableThrowPlans.Add(throwPlan);
        return reusableThrowPlans;
    }

    private bool TryGetKnockbackDestination(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        ISet<Vector2Int> reservedDestinations,
        ISet<TacticsCharacterController> displacedTargets,
        out Vector2Int destination)
    {
        destination = default;
        if (source == null ||
            target == null ||
            ability == null ||
            !ability.AppliesKnockback ||
            mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            !target.isActiveAndEnabled ||
            !target.IsAlive ||
            !TacticsKnockbackUtility.TryGetStepDelta(sourceTile, target.GridPosition, out Vector2Int stepDelta))
        {
            return false;
        }

        bool ignoreSourceOccupancy = source.GridPosition != sourceTile;
        Vector2Int currentTile = target.GridPosition;
        Vector2Int furthestReachableTile = currentTile;
        for (int step = 0; step < ability.Knockback.DistanceInTiles; step++)
        {
            Vector2Int nextTile = currentTile + stepDelta;
            if (!CanOccupyKnockbackTile(
                    source,
                    target,
                    currentTile,
                    nextTile,
                    ignoreSourceOccupancy,
                    reservedDestinations,
                    displacedTargets))
            {
                break;
            }

            furthestReachableTile = nextTile;
            currentTile = nextTile;
        }

        if (furthestReachableTile == target.GridPosition)
        {
            return false;
        }

        destination = furthestReachableTile;
        return true;
    }

    private bool CanOccupyKnockbackTile(
        TacticsCharacterController source,
        TacticsCharacterController target,
        Vector2Int fromTile,
        Vector2Int nextTile,
        bool ignoreSourceOccupancy,
        ISet<Vector2Int> reservedDestinations,
        ISet<TacticsCharacterController> displacedTargets)
    {
        if (!mapGenerator.IsWithinBounds(nextTile.x, nextTile.y) ||
            !mapGenerator.IsTraversable(nextTile.x, nextTile.y) ||
            TacticsTileBlockerUtility.IsBlockingTile(nextTile) ||
            !target.CanTraverseForcedMovementStep(fromTile, nextTile) ||
            (reservedDestinations != null && reservedDestinations.Contains(nextTile)))
        {
            return false;
        }

        if (characterRegistry == null ||
            !characterRegistry.TryGetCharacterAtTile(nextTile, out TacticsCharacterController occupant, target))
        {
            return true;
        }

        if (ignoreSourceOccupancy && ReferenceEquals(occupant, source))
        {
            return true;
        }

        return displacedTargets != null && displacedTargets.Contains(occupant);
    }

    private bool TryBuildThrowPlan(
        TacticsCharacterController source,
        Vector2Int sourceTile,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        Vector2Int throwDestination,
        out TacticsThrowPlan plan)
    {
        plan = default;
        if (source == null ||
            target == null ||
            ability == null ||
            !ability.AppliesThrowing ||
            !IsValidTarget(source, sourceTile, ability, target) ||
            throwDestination == target.GridPosition ||
            !CanTargetTile(source, sourceTile, ability, throwDestination) ||
            !CanOccupyThrowDestination(source, target, throwDestination) ||
            !TacticsKnockbackUtility.TryGetStepDelta(target.GridPosition, throwDestination, out Vector2Int stepDelta))
        {
            return false;
        }

        TacticsMovementDirection travelDirection = TacticsKnockbackUtility.GetMovementDirection(stepDelta);
        plan = new TacticsThrowPlan(
            target,
            throwDestination,
            travelDirection,
            TacticsKnockbackUtility.GetOppositeDirection(travelDirection),
            ability.Throwing);
        return true;
    }

    private bool CanOccupyThrowDestination(
        TacticsCharacterController source,
        TacticsCharacterController target,
        Vector2Int destination)
    {
        if (mapGenerator == null ||
            !mapGenerator.HasGeneratedMap ||
            !mapGenerator.IsWithinBounds(destination.x, destination.y) ||
            !mapGenerator.IsTraversable(destination.x, destination.y) ||
            TacticsTileBlockerUtility.IsBlockingTile(destination) ||
            target == null ||
            !target.CanTraverseForcedMovementStep(target.GridPosition, destination))
        {
            return false;
        }

        if (characterRegistry == null ||
            !characterRegistry.TryGetCharacterAtTile(destination, out TacticsCharacterController occupant, target))
        {
            return true;
        }

        return ReferenceEquals(occupant, source) && source.GridPosition == destination;
    }

    private IEnumerator ResolveKnockbackRoutine(List<TacticsKnockbackPlan> plans, TacticsCharacterController damageSource)
    {
        if (plans == null || plans.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < plans.Count; i++)
        {
            TacticsKnockbackPlan plan = plans[i];
            TacticsCharacterController target = plan.Target;
            if (target == null ||
                !target.isActiveAndEnabled ||
                !target.IsAlive ||
                target.GridPosition == plan.Destination ||
                !target.TryBeginKnockback(plan.Destination, plan.TravelDirection, plan.AnimationDirection, plan.Settings))
            {
                continue;
            }

            while (target != null && target.IsMoving)
            {
                yield return null;
            }

            if (target != null && target.isActiveAndEnabled && target.IsAlive)
            {
                Vector3? damageSourcePosition = damageSource != null ? damageSource.TurnFocusPoint : null;
                target.PlayDamageImpact(damageSourcePosition);
            }
        }
    }

    private IEnumerator ResolveThrowRoutine(List<TacticsThrowPlan> plans, TacticsCharacterController damageSource)
    {
        if (plans == null || plans.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < plans.Count; i++)
        {
            TacticsThrowPlan plan = plans[i];
            TacticsCharacterController target = plan.Target;
            if (target == null ||
                !target.isActiveAndEnabled ||
                !target.IsAlive ||
                target.GridPosition == plan.Destination ||
                !target.TryBeginThrow(plan.Destination, plan.TravelDirection, plan.AnimationDirection, plan.Settings))
            {
                continue;
            }

            while (target != null && target.IsMoving)
            {
                yield return null;
            }

            if (target != null && target.isActiveAndEnabled && target.IsAlive)
            {
                Vector3? damageSourcePosition = damageSource != null ? damageSource.TurnFocusPoint : null;
                target.PlayDamageImpact(damageSourcePosition);
            }
        }
    }

    private bool CanResolveReplicatedAbility(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        Vector2Int? throwDestination,
        out List<TacticsCharacterController> affectedTargets)
    {
        affectedTargets = reusableAreaTargets;
        affectedTargets.Clear();

        if (source == null || ability == null || !source.isActiveAndEnabled || !source.IsAlive)
        {
            return false;
        }

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return false;
        }

        if (!source.TryGetAbilityCostPayment(ability, out _))
        {
            return false;
        }

        if (!CanTargetTile(source, source.GridPosition, ability, targetTile))
        {
            return false;
        }

        if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
            !IsValidTarget(source, source.GridPosition, ability, FindCharacterAt(targetTile)))
        {
            return false;
        }

        affectedTargets = GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
        if (affectedTargets.Count == 0)
        {
            return false;
        }

        if (!ability.AppliesThrowing)
        {
            return true;
        }

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            ability,
            targetTile,
            new List<TacticsCharacterController>(affectedTargets),
            ResolveReplicatedAbilityCostPayment(source, ability),
            throwDestination);
        return TryBuildThrowPlans(context).Count > 0;
    }

    private IEnumerator ResolveAbilityRoutine(TacticsAbilityExecutionContext context)
    {
        IReadOnlyDictionary<TacticsCharacterController, TacticsAttackResolution> targetResolutions = ResolveAttackResolutions(context);
        if (targetResolutions != null)
        {
            context = new TacticsAbilityExecutionContext(
                context.Source,
                context.Ability,
                context.TargetTile,
                context.Targets,
                context.CostPayment,
                context.ThrowDestination,
                context.DelayedImpactTargets,
                targetResolutions);
        }

        List<TacticsThrowPlan> throwPlans = TryBuildThrowPlans(context);
        List<TacticsKnockbackPlan> knockbackPlans = BuildKnockbackPlans(context);
        HashSet<TacticsCharacterController> delayedImpactTargets = throwPlans.Count > 0 || knockbackPlans.Count > 0
            ? new HashSet<TacticsCharacterController>()
            : null;
        if (delayedImpactTargets != null)
        {
            for (int i = 0; i < throwPlans.Count; i++)
            {
                delayedImpactTargets.Add(throwPlans[i].Target);
            }

            for (int i = 0; i < knockbackPlans.Count; i++)
            {
                delayedImpactTargets.Add(knockbackPlans[i].Target);
            }

            context = new TacticsAbilityExecutionContext(
                context.Source,
                context.Ability,
                context.TargetTile,
                context.Targets,
                context.CostPayment,
                context.ThrowDestination,
                delayedImpactTargets,
                context.TargetResolutions);
        }

        if (context.Source != null && context.Source.isActiveAndEnabled)
        {
            yield return context.Source.PlayAttackAnimationTowards(context.TargetTile);
        }

        if (context.Ability != null && context.Ability.UsesProjectilePresentation)
        {
            yield return PlayProjectilePresentationRoutine(context);
        }

        ShowAttackOutcomeText(context);
        PlayHitEffectPresentation(context);

        bool appliedAnyEffect = false;
        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = context.Ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            if (!effectProcessors.TryGetValue(effect.EffectKind, out ITacticsAbilityEffectProcessor processor))
            {
                Debug.LogWarning($"No combat effect processor is registered for '{effect.EffectKind}'.");
                continue;
            }

            if (!processor.CanApply(context, effect))
            {
                continue;
            }

            processor.Apply(context, effect);
            appliedAnyEffect = true;
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = context.Ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            TacticsApplyStatusEffectData statusEffect = statusEffects[i];
            if (!statusEffectProcessor.CanApply(context, statusEffect))
            {
                continue;
            }

            statusEffectProcessor.Apply(context, statusEffect);
            appliedAnyEffect = true;
        }

        if (throwPlans.Count > 0)
        {
            appliedAnyEffect = true;
            yield return ResolveThrowRoutine(throwPlans, context.Source);
        }

        if (knockbackPlans.Count > 0)
        {
            appliedAnyEffect = true;
            yield return ResolveKnockbackRoutine(knockbackPlans, context.Source);
        }

        bool shouldConsumeAbility = appliedAnyEffect || HasResolvedAttackOutcome(context);
        if (shouldConsumeAbility && context.Source != null && context.Source.isActiveAndEnabled)
        {
            if (context.Source.TrySpendAbilityCost(context.CostPayment))
            {
                context.Source.CommitAbilityUse();
            }
        }

        RestoreIdleState();
    }

    private IEnumerator PlayProjectilePresentationRoutine(TacticsAbilityExecutionContext context)
    {
        if (context.Source == null ||
            context.Ability == null ||
            context.Ability.ProjectilePrefab == null)
        {
            yield break;
        }

        Vector3 launchPosition = context.Source.GetProjectileLaunchPosition();
        Vector3 impactPosition = ResolveProjectileImpactPosition(context);
        int sortingLayerId = context.Source.GetCombatTextSortingLayerId();
        int sortingOrder = context.Source.GetCombatTextSortingOrder();

        TacticsAbilityProjectile projectile = Instantiate(
            context.Ability.ProjectilePrefab,
            launchPosition,
            Quaternion.identity);

        yield return projectile.Play(new TacticsAbilityProjectileFlight(
            launchPosition,
            impactPosition,
            sortingLayerId,
            sortingOrder));
    }

    private static void PlayHitEffectPresentation(TacticsAbilityExecutionContext context)
    {
        if (context.Ability == null || !context.Ability.UsesHitEffectPresentation || context.Targets == null)
        {
            return;
        }

        TacticsAbilityHitEffectDefinition hitEffect = context.Ability.HitEffect;
        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            TacticsAbilityHitEffectSystem.Show(hitEffect, target);
        }
    }

    private Vector3 ResolveProjectileImpactPosition(TacticsAbilityExecutionContext context)
    {
        if (!context.Ability.UsesAreaOfEffect && context.Targets != null && context.Targets.Count > 0)
        {
            TacticsCharacterController target = context.Targets[0];
            if (target != null && target.isActiveAndEnabled)
            {
                return target.GetProjectileImpactPosition();
            }
        }

        if (mapGenerator != null && mapGenerator.HasGeneratedMap)
        {
            int impactElevation = mapGenerator.GetTileElevation(context.TargetTile.x, context.TargetTile.y);
            Vector3 impactTilePosition = mapGenerator.GridToWorldPosition(context.TargetTile.x, context.TargetTile.y, impactElevation);
            return impactTilePosition + new Vector3(0f, mapGenerator.TileHeight * 0.5f, 0f);
        }

        return context.Source != null ? context.Source.TurnFocusPoint : Vector3.zero;
    }

    private static TacticsAbilityCostPayment ResolveReplicatedAbilityCostPayment(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability)
    {
        if (source != null && source.TryGetAbilityCostPayment(ability, out TacticsAbilityCostPayment payment))
        {
            return payment;
        }

        return ability != null && ability.HasMovementCost
            ? new TacticsAbilityCostPayment(TacticsAbilityResourceType.Movement, 1)
            : ability != null && ability.HasResourceCost
                ? new TacticsAbilityCostPayment(ability.CostResourceType, ability.CostAmount)
                : TacticsAbilityCostPayment.None;
    }

    private IReadOnlyDictionary<TacticsCharacterController, TacticsAttackResolution> ResolveAttackResolutions(TacticsAbilityExecutionContext context)
    {
        reusableAttackResolutions.Clear();
        if (context.Targets == null || context.Targets.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
            {
                continue;
            }

            TacticsAttackResolution resolution = TacticsCombatResolutionUtility.Resolve(context.Source, target, context.Ability);
            if (resolution.Outcome != TacticsAttackOutcome.Hit)
            {
                reusableAttackResolutions[target] = resolution;
            }
        }

        return reusableAttackResolutions.Count > 0 ? reusableAttackResolutions : null;
    }

    private static void ShowAttackOutcomeText(TacticsAbilityExecutionContext context)
    {
        if (context.Targets == null || context.Targets.Count == 0 || context.TargetResolutions == null)
        {
            return;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target == null || !context.TryGetAttackResolution(target, out TacticsAttackResolution resolution) || resolution.DidLand)
            {
                continue;
            }

            TacticsCombatTextSystem.ShowAttackOutcome(target, resolution.Outcome);
        }
    }

    private static bool HasResolvedAttackOutcome(TacticsAbilityExecutionContext context)
    {
        return context.TargetResolutions != null && context.TargetResolutions.Count > 0;
    }

    private void SetState(TacticsCombatState nextState)
    {
        State = nextState;
        StateChanged?.Invoke();
    }
}

public enum TacticsCombatState
{
    Idle = 0,
    TargetingAbility = 1,
    ResolvingAbility = 2
}

public readonly struct TacticsAbilityExecutionContext
{
    public TacticsAbilityExecutionContext(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        IReadOnlyList<TacticsCharacterController> targets,
        TacticsAbilityCostPayment costPayment,
        Vector2Int? throwDestination = null,
        IReadOnlyCollection<TacticsCharacterController> delayedImpactTargets = null,
        IReadOnlyDictionary<TacticsCharacterController, TacticsAttackResolution> targetResolutions = null)
    {
        Source = source;
        Ability = ability;
        TargetTile = targetTile;
        Targets = targets;
        CostPayment = costPayment;
        ThrowDestination = throwDestination;
        DelayedImpactTargets = delayedImpactTargets;
        TargetResolutions = targetResolutions;
    }

    public TacticsCharacterController Source { get; }
    public TacticsAbilityDefinition Ability { get; }
    public Vector2Int TargetTile { get; }
    public IReadOnlyList<TacticsCharacterController> Targets { get; }
    public TacticsAbilityCostPayment CostPayment { get; }
    public Vector2Int? ThrowDestination { get; }
    public IReadOnlyCollection<TacticsCharacterController> DelayedImpactTargets { get; }
    public IReadOnlyDictionary<TacticsCharacterController, TacticsAttackResolution> TargetResolutions { get; }

    public bool ShouldDelayImpactFor(TacticsCharacterController target)
    {
        if (target == null || DelayedImpactTargets == null)
        {
            return false;
        }

        foreach (TacticsCharacterController delayedTarget in DelayedImpactTargets)
        {
            if (ReferenceEquals(delayedTarget, target))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanApplyEffectsTo(TacticsCharacterController target)
    {
        return target != null &&
               (!TryGetAttackResolution(target, out TacticsAttackResolution resolution) || resolution.DidLand);
    }

    public bool TryGetAttackResolution(TacticsCharacterController target, out TacticsAttackResolution resolution)
    {
        resolution = default;
        return target != null &&
               TargetResolutions != null &&
               TargetResolutions.TryGetValue(target, out resolution);
    }
}

public interface ITacticsAbilityEffectProcessor
{
    bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect);
    void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect);
}

public sealed class TacticsDealDamageEffectProcessor : ITacticsAbilityEffectProcessor
{
    private const float CriticalHitDamageMultiplier = 1.5f;

    public bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        if (context.Source == null || context.Targets == null || context.Targets.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target != null && target.isActiveAndEnabled && target.IsAlive && context.CanApplyEffectsTo(target))
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsDealDamageEffectData damage = effect.DealDamage;
        TacticsAbilityDamageType damageType = context.Ability != null
            ? context.Ability.DamageType
            : TacticsAbilityDamageType.Melee;
        int amount = TacticsAbilityEffectMath.EvaluateDamageAmount(context.Source, context.Ability, damage, useAverageRoll: false);
        if (amount <= 0)
        {
            return;
        }

        bool isCriticalHit = context.Source.RollCriticalHit(damageType);
        if (isCriticalHit)
        {
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * CriticalHitDamageMultiplier));
        }

        Vector3? damageSourcePosition = context.Source != null ? context.Source.TurnFocusPoint : null;
        IReadOnlyList<TacticsCharacterController> targets = context.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            TacticsCharacterController target = targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            bool playImpactAnimation = !context.ShouldDelayImpactFor(target);
            target.ApplyDamage(amount, damageSourcePosition, isCriticalHit, context.Source, playImpactAnimation);
        }
    }
}

public sealed class TacticsRestoreHitPointsEffectProcessor : ITacticsAbilityEffectProcessor
{
    public bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        if (context.Source == null || context.Targets == null || context.Targets.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target != null &&
                target.isActiveAndEnabled &&
                target.IsAlive &&
                context.CanApplyEffectsTo(target) &&
                target.CurrentHitPoints < target.MaxHitPoints)
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsRestoreHitPointsEffectData restoreHitPoints = effect.RestoreHitPoints;
        int amount = TacticsAbilityEffectMath.EvaluateRestoreHitPointsAmount(context.Source, restoreHitPoints, useAverageRoll: false);
        if (amount <= 0)
        {
            return;
        }

        IReadOnlyList<TacticsCharacterController> targets = context.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            TacticsCharacterController target = targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            target.RestoreHitPoints(amount);
        }
    }
}

public sealed class TacticsRestoreResourceEffectProcessor : ITacticsAbilityEffectProcessor
{
    public bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        if (context.Source == null || context.Targets == null || context.Targets.Count == 0)
        {
            return false;
        }

        TacticsAbilityResourceType resourceType = effect.RestoreResource.ResourceType;
        if (resourceType == TacticsAbilityResourceType.None)
        {
            return false;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target != null &&
                target.isActiveAndEnabled &&
                target.IsAlive &&
                context.CanApplyEffectsTo(target) &&
                target.GetCurrentResource(resourceType) < target.GetMaxResource(resourceType))
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsRestoreResourceEffectData restoreResource = effect.RestoreResource;
        int amount = TacticsAbilityEffectMath.EvaluateRestoreResourceAmount(context.Source, restoreResource, useAverageRoll: false);
        if (amount <= 0)
        {
            return;
        }

        IReadOnlyList<TacticsCharacterController> targets = context.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            TacticsCharacterController target = targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            target.RestoreResource(restoreResource.ResourceType, amount);
        }
    }
}

public sealed class TacticsApplyStatusEffectProcessor
{
    public bool CanApply(TacticsAbilityExecutionContext context, TacticsApplyStatusEffectData effect)
    {
        if (context.Source == null || context.Targets == null || context.Targets.Count == 0)
        {
            return false;
        }

        if (effect.DurationTurns <= 0)
        {
            return false;
        }

        for (int i = 0; i < context.Targets.Count; i++)
        {
            TacticsCharacterController target = context.Targets[i];
            if (target != null && target.isActiveAndEnabled && target.IsAlive && context.CanApplyEffectsTo(target))
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsApplyStatusEffectData statusEffect)
    {
        int potency = TacticsAbilityEffectMath.EvaluateStatusPotency(
            context.Source,
            context.Ability,
            statusEffect,
            useAverageRoll: false);

        IReadOnlyList<TacticsCharacterController> targets = context.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            TacticsCharacterController target = targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || !context.CanApplyEffectsTo(target))
            {
                continue;
            }

            int resolvedPotency = statusEffect.StatusEffectType == TacticsStatusEffectType.Poison
                ? TacticsAbilityEffectMath.EvaluateStatusPotency(
                    context.Source,
                    target,
                    context.Ability,
                    statusEffect,
                    useAverageRoll: false)
                : potency;
            target.ApplyStatusEffect(statusEffect, resolvedPotency, context.Source);
        }
    }
}
