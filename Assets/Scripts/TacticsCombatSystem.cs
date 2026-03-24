using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsCombatSystem : MonoBehaviour
{
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsAbilityCatalog abilityCatalog;

    private readonly Dictionary<TacticsAbilityEffectKind, ITacticsAbilityEffectProcessor> effectProcessors = new();
    private readonly List<TacticsCharacterController> reusableAreaTargets = new();
    private readonly List<Vector2Int> reusableTargetTiles = new();
    private readonly List<Vector2Int> reusableTargetableTiles = new();
    private readonly List<Vector2Int> reusableAreaTiles = new();
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

        abilityCatalog ??= TacticsAbilityCatalogResources.LoadCatalog();
        effectProcessors[TacticsAbilityEffectKind.DealDamage] = new TacticsDealDamageEffectProcessor();
        effectProcessors[TacticsAbilityEffectKind.RestoreHitPoints] = new TacticsRestoreHitPointsEffectProcessor();
    }

    public void AssignMapGenerator(ProceduralIsometricMapGenerator generator)
    {
        mapGenerator = generator;
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
        reusableTargetableTiles.Clear();

        if (!CanUseAbility(source, ability) || mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return reusableTargetableTiles;
        }

        Vector2Int sourceTile = source.GridPosition;
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
                    reusableTargetableTiles.Add(targetTile);
                }
            }
        }

        return reusableTargetableTiles;
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

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            ability,
            targetTile,
            new List<TacticsCharacterController>(affectedTargets));

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
        if (State == TacticsCombatState.ResolvingAbility || resolveRoutine != null)
        {
            return false;
        }

        if (!CanResolveReplicatedAbility(source, ability, targetTile, out List<TacticsCharacterController> affectedTargets))
        {
            return false;
        }

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            ability,
            targetTile,
            new List<TacticsCharacterController>(affectedTargets));

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
        return source != null &&
               ability != null &&
               source.CanUseAbilitiesThisTurn &&
               source.HasResourcesForAbility(ability) &&
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
        reusableAreaTargets.Clear();

        if (!CanUseAbility(source, ability) ||
            !CanTargetTile(source, sourceTile, ability, targetTile) ||
            (ability != null &&
             ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
             !IsValidTarget(source, sourceTile, ability, FindCharacterAt(targetTile))))
        {
            return reusableAreaTargets;
        }

        return GetAffectedTargets(source, ability, targetTile, reusableAreaTargets);
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
        if (source == null || ability == null || target == null || ReferenceEquals(source, target))
        {
            return ability != null &&
                   source != null &&
                   target != null &&
                   ReferenceEquals(source, target) &&
                   CanAffectTarget(source, ability, target);
        }

        if (!target.isActiveAndEnabled || !target.IsAlive)
        {
            return false;
        }

        if (!CanTargetTile(source, sourceTile, ability, target.GridPosition))
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

    private TacticsCharacterController FindCharacterAt(Vector2Int tile)
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character != null && character.isActiveAndEnabled && character.GridPosition == tile)
            {
                return character;
            }
        }

        return null;
    }

    private bool HasValidTargetsAtTile(TacticsCharacterController source, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        if (ability != null &&
            ability.RangeType == TacticsAbilityRangeType.SurroundingAoE &&
            !IsValidTarget(source, source != null ? source.GridPosition : default, ability, FindCharacterAt(targetTile)))
        {
            return false;
        }

        return GetAffectedTargets(source, ability, targetTile, reusableAreaTargets).Count > 0;
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
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController target = characters[i];
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

        List<Vector2Int> traversedTiles = GetLineTiles(sourceTile, targetTile);
        if (traversedTiles.Count <= 2)
        {
            return true;
        }

        int sourceElevation = mapGenerator.GetTileElevation(sourceTile.x, sourceTile.y);
        int targetElevation = mapGenerator.GetTileElevation(targetTile.x, targetTile.y);
        int blockingElevationThreshold = Mathf.Min(sourceElevation, targetElevation);

        for (int i = 1; i < traversedTiles.Count - 1; i++)
        {
            Vector2Int tile = traversedTiles[i];
            int tileElevation = mapGenerator.GetTileElevation(tile.x, tile.y);

            // Ranged line of sight behaves like same-level visibility plus the ability
            // to see onto a single ledge face one elevation higher. Intermediate tiles
            // only block when they rise above the lower endpoint elevation, which makes
            // a one-tile ledge targetable while thicker raised plateaus become walls.
            if (tileElevation > blockingElevationThreshold)
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

    private static List<Vector2Int> GetLineTiles(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> tiles = new();

        int x = start.x;
        int y = start.y;
        int deltaX = Mathf.Abs(end.x - start.x);
        int deltaY = Mathf.Abs(end.y - start.y);
        int stepX = start.x < end.x ? 1 : -1;
        int stepY = start.y < end.y ? 1 : -1;
        int error = deltaX - deltaY;

        while (true)
        {
            tiles.Add(new Vector2Int(x, y));
            if (x == end.x && y == end.y)
            {
                break;
            }

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
        }

        return tiles;
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

        return false;
    }

    private bool CanResolveReplicatedAbility(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
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

        if (!source.HasResourcesForAbility(ability))
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
        return affectedTargets.Count > 0;
    }

    private IEnumerator ResolveAbilityRoutine(TacticsAbilityExecutionContext context)
    {
        if (context.Source != null && context.Source.isActiveAndEnabled)
        {
            yield return context.Source.PlayAttackAnimationTowards(context.TargetTile);
        }

        if (context.Ability != null && context.Ability.UsesProjectilePresentation)
        {
            yield return PlayProjectilePresentationRoutine(context);
        }

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

        if (appliedAnyEffect && context.Source != null && context.Source.isActiveAndEnabled)
        {
            if (context.Source.TrySpendAbilityCost(context.Ability))
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
        IReadOnlyList<TacticsCharacterController> targets)
    {
        Source = source;
        Ability = ability;
        TargetTile = targetTile;
        Targets = targets;
    }

    public TacticsCharacterController Source { get; }
    public TacticsAbilityDefinition Ability { get; }
    public Vector2Int TargetTile { get; }
    public IReadOnlyList<TacticsCharacterController> Targets { get; }
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
        return context.Source != null && context.Targets != null && context.Targets.Count > 0;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsDealDamageEffectData damage = effect.DealDamage;
        TacticsAbilityDamageType damageType = context.Ability != null
            ? context.Ability.DamageType
            : TacticsAbilityDamageType.Melee;
        int amount = damage.DamageFormula switch
        {
            TacticsDamageFormula.FlatValue => damage.FlatAmount,
            _ => context.Source.RollBaseDamage(damageType)
        };

        amount += TacticsAbilityScalingCalculator.EvaluateDamageBonus(context.Source, damage.Scaling);
        amount = Mathf.Max(0, Mathf.RoundToInt(amount * damage.BonusMultiplier));
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
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
            {
                continue;
            }

            target.ApplyDamage(amount, damageSourcePosition, isCriticalHit, context.Source);
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
            if (target != null && target.isActiveAndEnabled && target.IsAlive && target.CurrentHitPoints < target.MaxHitPoints)
            {
                return true;
            }
        }

        return false;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsRestoreHitPointsEffectData restoreHitPoints = effect.RestoreHitPoints;
        int amount = restoreHitPoints.HealingFormula switch
        {
            TacticsDamageFormula.AttackerBaseDamage => context.Source.RollBaseDamage(TacticsAbilityDamageType.Magic),
            _ => restoreHitPoints.FlatAmount
        };

        amount += TacticsAbilityScalingCalculator.EvaluateDamageBonus(context.Source, restoreHitPoints.Scaling);
        amount = Mathf.Max(0, Mathf.RoundToInt(amount * restoreHitPoints.BonusMultiplier));
        if (amount <= 0)
        {
            return;
        }

        IReadOnlyList<TacticsCharacterController> targets = context.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            TacticsCharacterController target = targets[i];
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
            {
                continue;
            }

            target.RestoreHitPoints(amount);
        }
    }
}
