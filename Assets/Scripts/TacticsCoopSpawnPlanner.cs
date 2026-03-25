using System.Collections.Generic;
using UnityEngine;

public static class TacticsCoopSpawnPlanner
{
    private sealed class SpawnRequest
    {
        public SpawnRequest(string characterId, int partyIndex, int slotIndex)
        {
            CharacterId = characterId;
            PartyIndex = partyIndex;
            SlotIndex = slotIndex;
        }

        public string CharacterId { get; }
        public int PartyIndex { get; }
        public int SlotIndex { get; }
    }

    public sealed class PlannedCharacterSpawn
    {
        public PlannedCharacterSpawn(string runtimeId, string characterId, int partyIndex, int slotIndex, Vector2Int spawnTile)
        {
            RuntimeId = runtimeId;
            CharacterId = characterId;
            PartyIndex = partyIndex;
            SlotIndex = slotIndex;
            SpawnTile = spawnTile;
        }

        public string RuntimeId { get; }
        public string CharacterId { get; }
        public int PartyIndex { get; }
        public int SlotIndex { get; }
        public Vector2Int SpawnTile { get; }
    }

    public static List<PlannedCharacterSpawn> BuildPlayerSpawns(
        ProceduralIsometricMapGenerator mapGenerator,
        IReadOnlyList<TacticsCharacterDefinition> hostParty,
        IReadOnlyList<TacticsCharacterDefinition> clientParty,
        IReadOnlyCollection<Vector2Int> blockedTiles = null)
    {
        List<PlannedCharacterSpawn> plannedSpawns = new();
        if (mapGenerator == null)
        {
            return plannedSpawns;
        }

        List<SpawnRequest> requests = BuildSpawnRequests(hostParty, clientParty);
        if (requests.Count == 0)
        {
            return plannedSpawns;
        }

        HashSet<Vector2Int> occupiedTiles = blockedTiles != null
            ? new HashSet<Vector2Int>(blockedTiles)
            : new HashSet<Vector2Int>();
        List<Vector2Int> randomSpawnTiles = mapGenerator.GetRandomSpawnTiles(requests.Count, occupiedTiles);

        for (int i = 0; i < requests.Count; i++)
        {
            SpawnRequest request = requests[i];
            Vector2Int spawnTile = i < randomSpawnTiles.Count
                ? randomSpawnTiles[i]
                : FindAvailableSpawnTile(mapGenerator, occupiedTiles);
            occupiedTiles.Add(spawnTile);
            plannedSpawns.Add(new PlannedCharacterSpawn(
                BuildRuntimeCharacterId(request.PartyIndex, request.SlotIndex, request.CharacterId),
                request.CharacterId,
                request.PartyIndex,
                request.SlotIndex,
                spawnTile));
        }

        return plannedSpawns;
    }

    private static List<SpawnRequest> BuildSpawnRequests(
        IReadOnlyList<TacticsCharacterDefinition> hostParty,
        IReadOnlyList<TacticsCharacterDefinition> clientParty)
    {
        List<SpawnRequest> requests = new();
        AppendPartyRequests(hostParty, partyIndex: 0, requests);
        AppendPartyRequests(clientParty, partyIndex: 1, requests);
        return requests;
    }

    private static void AppendPartyRequests(
        IReadOnlyList<TacticsCharacterDefinition> party,
        int partyIndex,
        List<SpawnRequest> requests)
    {
        if (party == null || requests == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < party.Count; slotIndex++)
        {
            TacticsCharacterDefinition definition = party[slotIndex];
            if (definition == null)
            {
                continue;
            }

            requests.Add(new SpawnRequest(definition.CharacterId, partyIndex, slotIndex));
        }
    }

    private static Vector2Int FindAvailableSpawnTile(
        ProceduralIsometricMapGenerator mapGenerator,
        HashSet<Vector2Int> occupiedTiles)
    {
        if (mapGenerator == null)
        {
            return Vector2Int.zero;
        }

        Vector2Int center = mapGenerator.GetCenterTile();
        int maxRadius = Mathf.Max(mapGenerator.Width, mapGenerator.Length);
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (IsSpawnTileAvailable(mapGenerator, candidate, occupiedTiles))
                    {
                        return candidate;
                    }
                }
            }
        }

        return center;
    }

    private static bool IsSpawnTileAvailable(
        ProceduralIsometricMapGenerator mapGenerator,
        Vector2Int tile,
        HashSet<Vector2Int> occupiedTiles)
    {
        return mapGenerator != null &&
               mapGenerator.IsTraversable(tile.x, tile.y) &&
               (occupiedTiles == null || !occupiedTiles.Contains(tile));
    }

    private static string BuildRuntimeCharacterId(int partyIndex, int slotIndex, string characterId)
    {
        string normalizedId = string.IsNullOrWhiteSpace(characterId) ? "character" : characterId.Trim();
        return $"party_{partyIndex}_slot_{slotIndex}_{normalizedId}";
    }
}
