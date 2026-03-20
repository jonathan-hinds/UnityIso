using System;
using UnityEngine;

[Serializable]
public struct TacticsEnemySpawnEntry
{
    [SerializeField] private TacticsCharacterDefinition characterDefinition;
    [SerializeField, Min(1)] private int count;

    public TacticsCharacterDefinition CharacterDefinition => characterDefinition;
    public int Count => Mathf.Max(0, count);
    public bool IsValid => characterDefinition != null && Count > 0;
}
