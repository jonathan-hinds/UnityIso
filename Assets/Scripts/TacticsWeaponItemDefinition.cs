using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsWeaponItem", menuName = "Tactics/Items/Weapon")]
public sealed class TacticsWeaponItemDefinition : TacticsEquipmentItemDefinition
{
    [Header("Weapon")]
    [SerializeField] private TacticsWeaponType weaponType = TacticsWeaponType.Sword;
    [SerializeField] private TacticsAbilityDamageType damageType = TacticsAbilityDamageType.Melee;
    [SerializeField, Min(0)] private int baseDamageMinBonus = 1;
    [SerializeField, Min(0)] private int baseDamageMaxBonus = 2;
    [SerializeField] private List<TacticsAbilityScalingDefinitionData> damageScaling = new();

    public TacticsWeaponType WeaponType => weaponType;
    public TacticsAbilityDamageType DamageType => damageType;
    public int BaseDamageMinBonus => Mathf.Max(0, baseDamageMinBonus);
    public int BaseDamageMaxBonus => Mathf.Max(BaseDamageMinBonus, baseDamageMaxBonus);
    public IReadOnlyList<TacticsAbilityScalingDefinitionData> DamageScaling => damageScaling;

    public int EvaluateDamageScalingBonus(TacticsCharacterController character)
    {
        return TacticsAbilityScalingCalculator.EvaluateDamageBonus(character, damageScaling);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        baseDamageMinBonus = Mathf.Max(0, baseDamageMinBonus);
        baseDamageMaxBonus = Mathf.Max(baseDamageMinBonus, baseDamageMaxBonus);
        damageScaling ??= new List<TacticsAbilityScalingDefinitionData>();
    }
}
