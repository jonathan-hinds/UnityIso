using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TacticsMatchGenerationSettings
{
    public const int MaxEnemyKinds = 10;

    [Min(0)] public int seed = 12345;
    [Min(1)] public int width = 10;
    [Min(1)] public int length = 10;
    [Min(0.01f)] public float noiseScale = 5f;
    [Range(1, 6)] public int noiseOctaves = 3;
    [Min(0)] public int minElevation = 0;
    [Min(0)] public int maxElevation = 4;
    public List<TacticsMatchEnemySettings> enemies = new();

    public TacticsMatchGenerationSettings Clone()
    {
        TacticsMatchGenerationSettings clone = new TacticsMatchGenerationSettings
        {
            seed = seed,
            width = width,
            length = length,
            noiseScale = noiseScale,
            noiseOctaves = noiseOctaves,
            minElevation = minElevation,
            maxElevation = maxElevation,
            enemies = new List<TacticsMatchEnemySettings>(enemies.Count)
        };

        for (int i = 0; i < enemies.Count; i++)
        {
            TacticsMatchEnemySettings entry = enemies[i];
            if (entry == null)
            {
                continue;
            }

            clone.enemies.Add(entry.Clone());
        }

        return clone;
    }

    public void Sanitize()
    {
        width = Mathf.Max(1, width);
        length = Mathf.Max(1, length);
        noiseScale = Mathf.Max(0.01f, noiseScale);
        noiseOctaves = Mathf.Clamp(noiseOctaves, 1, 6);
        minElevation = Mathf.Max(0, minElevation);
        maxElevation = Mathf.Max(minElevation, maxElevation);
        enemies ??= new List<TacticsMatchEnemySettings>();

        List<TacticsMatchEnemySettings> sanitized = new(Mathf.Min(enemies.Count, MaxEnemyKinds));
        HashSet<string> seenEnemyIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < enemies.Count && sanitized.Count < MaxEnemyKinds; i++)
        {
            TacticsMatchEnemySettings entry = enemies[i];
            if (entry == null)
            {
                continue;
            }

            entry.Sanitize();
            if (!entry.IsValid || !seenEnemyIds.Add(entry.enemyId))
            {
                continue;
            }

            sanitized.Add(entry);
        }

        enemies = sanitized;
    }
}

[Serializable]
public sealed class TacticsMatchEnemySettings
{
    public string enemyId;
    [Min(1)] public int count = 1;

    public bool IsValid => !string.IsNullOrWhiteSpace(enemyId) && count > 0;

    public TacticsMatchEnemySettings Clone()
    {
        return new TacticsMatchEnemySettings
        {
            enemyId = enemyId,
            count = count
        };
    }

    public void Sanitize()
    {
        enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
        count = Mathf.Max(1, count);
    }
}
