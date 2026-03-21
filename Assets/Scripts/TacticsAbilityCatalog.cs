using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsAbilityCatalog", menuName = "Tactics/Combat/Ability Catalog")]
public sealed class TacticsAbilityCatalog : ScriptableObject
{
    [SerializeField] private TacticsAbilityDefinition defaultAttackAbility;
    [SerializeField] private List<TacticsAbilityDefinition> registeredAbilities = new();

    public TacticsAbilityDefinition DefaultAttackAbility => defaultAttackAbility;
    public IReadOnlyList<TacticsAbilityDefinition> RegisteredAbilities => registeredAbilities;

    public static TacticsAbilityCatalog CreateFallback(TacticsAbilityDefinition defaultAttack)
    {
        TacticsAbilityCatalog catalog = CreateInstance<TacticsAbilityCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        catalog.defaultAttackAbility = defaultAttack;
        catalog.registeredAbilities = new List<TacticsAbilityDefinition>();
        if (defaultAttack != null)
        {
            catalog.registeredAbilities.Add(defaultAttack);
        }

        return catalog;
    }
}
