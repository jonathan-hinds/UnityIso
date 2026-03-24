using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsChestEncounterService : MonoBehaviour
{
    private const string MimicEnemyId = "mimic";

    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsEnemyTable enemyTable;
    [SerializeField] private TacticsTurnManager turnManager;

    public void AssignDependencies(
        ProceduralIsometricMapGenerator generator,
        TacticsEnemyTable availableEnemyTable,
        TacticsTurnManager manager)
    {
        mapGenerator = generator;
        enemyTable = availableEnemyTable;
        turnManager = manager;
    }

    public bool TryResolveChestOpen(
        TacticsCharacterController opener,
        TacticsChestController chest,
        int goldReward,
        string mimicRuntimeCharacterId,
        out TacticsChestResolutionResult result)
    {
        result = default;
        if (opener == null || chest == null)
        {
            return false;
        }

        if (chest.ContainsMimic)
        {
            if (!TrySpawnMimic(chest, mimicRuntimeCharacterId, out TacticsCharacterController mimic))
            {
                return false;
            }

            if (!chest.TryRevealMimic(opener))
            {
                Destroy(mimic.gameObject);
                return false;
            }

            if (mimic.TryGetComponent(out TacticsEnemyController enemyController))
            {
                enemyController.SetPriorityTarget(opener);
            }

            turnManager ??= FindFirstObjectByType<TacticsTurnManager>();
            if (turnManager != null)
            {
                turnManager.RegisterParticipant(mimic);
                turnManager.QueuePriorityTurn(mimic);
            }

            result = new TacticsChestResolutionResult(true, 0, mimic);
            return true;
        }

        int resolvedGoldReward = Mathf.Max(0, goldReward);
        if (!chest.TryOpen(opener, resolvedGoldReward))
        {
            return false;
        }

        result = new TacticsChestResolutionResult(false, resolvedGoldReward, null);
        return true;
    }

    private bool TrySpawnMimic(
        TacticsChestController chest,
        string runtimeCharacterId,
        out TacticsCharacterController mimic)
    {
        mimic = null;
        mapGenerator ??= FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null || chest == null)
        {
            return false;
        }

        enemyTable ??= Resources.Load<TacticsEnemyTable>("Tactics/EnemyTable");
        if (enemyTable == null || !enemyTable.TryGetCharacterData(MimicEnemyId, out TacticsCharacterData mimicData))
        {
            Debug.LogWarning($"Chest encounter service could not resolve enemy '{MimicEnemyId}'.");
            return false;
        }

        Transform enemyRoot = mapGenerator.GetOrCreateGeneratedAttachmentRoot("Enemies");
        string resolvedRuntimeId = string.IsNullOrWhiteSpace(runtimeCharacterId)
            ? $"enemy_{MimicEnemyId}_{chest.RuntimeChestId}"
            : runtimeCharacterId.Trim();

        mimic = TacticsCharacterSpawner.SpawnCharacter(
            mapGenerator,
            mimicData,
            chest.GridPosition,
            enemyRoot,
            runtimeCharacterId: resolvedRuntimeId);
        return mimic != null;
    }
}

public readonly struct TacticsChestResolutionResult
{
    public TacticsChestResolutionResult(
        bool revealedMimic,
        int goldReward,
        TacticsCharacterController spawnedMimic)
    {
        RevealedMimic = revealedMimic;
        GoldReward = goldReward;
        SpawnedMimic = spawnedMimic;
    }

    public bool RevealedMimic { get; }
    public int GoldReward { get; }
    public TacticsCharacterController SpawnedMimic { get; }
}
