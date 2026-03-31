using UnityEngine;

[CreateAssetMenu(fileName = "TacticsConsumableItem", menuName = "Tactics/Items/Consumable")]
public sealed class TacticsConsumableItemDefinition : TacticsItemDefinition
{
    [Header("Consumable")]
    [SerializeField] private TacticsAbilityDefinition linkedAbility;

    public override TacticsItemKind ItemKind => TacticsItemKind.Consumable;
    public TacticsAbilityDefinition LinkedAbility => linkedAbility;
    public bool IsUsable => linkedAbility != null;
}
