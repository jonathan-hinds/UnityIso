using System;
using UnityEngine;

[Serializable]
public struct TacticsEnemySpawnEntry
{
    [SerializeField] private string enemyId;
    [SerializeField] private TacticsCharacterDefinition characterDefinition;
    [SerializeField, Min(1)] private int count;

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
    public TacticsCharacterDefinition CharacterDefinition => characterDefinition;
    public int Count => Mathf.Max(0, count);
    public bool IsValid => (!string.IsNullOrWhiteSpace(EnemyId) || characterDefinition != null) && Count > 0;
}
