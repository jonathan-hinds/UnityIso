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
    private readonly List<Vector2Int> reusableTargetTiles = new();
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

        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController target = characters[i];
            if (!IsValidTarget(source, ability, target))
            {
                continue;
            }

            reusableTargetTiles.Add(target.GridPosition);
        }

        return reusableTargetTiles;
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

        TacticsCharacterController target = FindCharacterAt(targetTile);
        if (!IsValidTarget(source, ability, target))
        {
            return false;
        }

        TacticsAbilityExecutionContext context = new TacticsAbilityExecutionContext(
            source,
            target,
            ability,
            targetTile);

        if (!HasApplicableEffect(context))
        {
            RestoreIdleState();
            return false;
        }

        SetState(TacticsCombatState.ResolvingAbility);
        resolveRoutine = StartCoroutine(ResolveAbilityRoutine(context));
        return true;
    }

    public bool CanUseAbility(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        return source != null &&
               ability != null &&
               source.CanUseAbilitiesThisTurn &&
               source.isActiveAndEnabled;
    }

    private bool IsValidTarget(TacticsCharacterController source, TacticsAbilityDefinition ability, TacticsCharacterController target)
    {
        if (source == null || ability == null || target == null || ReferenceEquals(source, target))
        {
            return false;
        }

        if (!target.isActiveAndEnabled || !target.IsAlive)
        {
            return false;
        }

        if (!IsWithinRange(source.GridPosition, target.GridPosition, ability.Range))
        {
            return false;
        }

        return ability.TargetRule switch
        {
            TacticsAbilityTargetRule.HostileUnit => source.Team != target.Team,
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

    private static bool IsWithinRange(Vector2Int source, Vector2Int target, int range)
    {
        int distance = Mathf.Abs(source.x - target.x) + Mathf.Abs(source.y - target.y);
        return distance > 0 && distance <= Mathf.Max(1, range);
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

    private IEnumerator ResolveAbilityRoutine(TacticsAbilityExecutionContext context)
    {
        if (context.Source != null && context.Source.isActiveAndEnabled)
        {
            yield return context.Source.PlayAttackAnimationTowards(context.TargetTile);
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
            context.Source.CommitAbilityUse();
        }

        RestoreIdleState();
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
        TacticsCharacterController target,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile)
    {
        Source = source;
        Target = target;
        Ability = ability;
        TargetTile = targetTile;
    }

    public TacticsCharacterController Source { get; }
    public TacticsCharacterController Target { get; }
    public TacticsAbilityDefinition Ability { get; }
    public Vector2Int TargetTile { get; }
}

public interface ITacticsAbilityEffectProcessor
{
    bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect);
    void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect);
}

public sealed class TacticsDealDamageEffectProcessor : ITacticsAbilityEffectProcessor
{
    public bool CanApply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        return context.Source != null && context.Target != null;
    }

    public void Apply(TacticsAbilityExecutionContext context, TacticsAbilityEffectDefinitionData effect)
    {
        TacticsDealDamageEffectData damage = effect.DealDamage;
        int amount = damage.DamageFormula switch
        {
            TacticsDamageFormula.FlatValue => damage.FlatAmount,
            _ => context.Source.RollBaseDamage()
        };

        amount += TacticsAbilityScalingCalculator.EvaluateDamageBonus(context.Source, damage.Scaling);
        amount = Mathf.Max(0, amount + damage.BonusAmount);
        if (amount <= 0)
        {
            return;
        }

        Vector3? damageSourcePosition = context.Source != null ? context.Source.TurnFocusPoint : null;
        context.Target.ApplyDamage(amount, damageSourcePosition);
    }
}
