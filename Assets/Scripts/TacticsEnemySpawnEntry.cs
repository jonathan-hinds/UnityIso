using System;
using UnityEngine;

[Serializable]
public struct TacticsEnemySpawnEntry
{
    [SerializeField] private string enemyId;
    [SerializeField, Min(1)] private int count;

    public TacticsEnemySpawnEntry(string enemyId, int count)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
        this.count = Mathf.Max(1, count);
    }

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
    public int Count => Mathf.Max(0, count);
    public bool IsValid => !string.IsNullOrWhiteSpace(EnemyId) && Count > 0;
}
