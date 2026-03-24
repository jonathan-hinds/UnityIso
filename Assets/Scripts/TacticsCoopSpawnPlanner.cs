using System.Collections.Generic;
using UnityEngine;

public static class TacticsCoopSpawnPlanner
{
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
        IReadOnlyList<TacticsCharacterDefinition> clientParty)
    {
        List<PlannedCharacterSpawn> plannedSpawns = new();
        HashSet<Vector2Int> occupiedTiles = new();

        AppendPartySpawns(mapGenerator, hostParty, partyIndex: 0, occupiedTiles, plannedSpawns);
        AppendPartySpawns(mapGenerator, clientParty, partyIndex: 1, occupiedTiles, plannedSpawns);

        return plannedSpawns;
    }

    private static void AppendPartySpawns(
        ProceduralIsometricMapGenerator mapGenerator,
        IReadOnlyList<TacticsCharacterDefinition> party,
        int partyIndex,
        HashSet<Vector2Int> occupiedTiles,
        List<PlannedCharacterSpawn> plannedSpawns)
    {
        if (mapGenerator == null || party == null || occupiedTiles == null || plannedSpawns == null)
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

            Vector2Int requestedTile = ResolvePartySpawnAnchor(mapGenerator, definition.PreferredSpawnTile, partyIndex);
            Vector2Int resolvedTile = FindAvailableSpawnTile(mapGenerator, requestedTile, occupiedTiles);
            occupiedTiles.Add(resolvedTile);
            plannedSpawns.Add(new PlannedCharacterSpawn(
                BuildRuntimeCharacterId(partyIndex, slotIndex, definition.CharacterId),
                definition.CharacterId,
                partyIndex,
                slotIndex,
                resolvedTile));
        }
    }

    private static Vector2Int ResolvePartySpawnAnchor(
        ProceduralIsometricMapGenerator mapGenerator,
        Vector2Int preferredTile,
        int partyIndex)
    {
        if (mapGenerator == null || partyIndex == 0)
        {
            return preferredTile;
        }

        Vector2Int fallback = preferredTile == default
            ? mapGenerator.GetCenterTile()
            : preferredTile;
        return new Vector2Int(
            Mathf.Clamp((mapGenerator.Width - 1) - fallback.x, 0, Mathf.Max(0, mapGenerator.Width - 1)),
            Mathf.Clamp((mapGenerator.Length - 1) - fallback.y, 0, Mathf.Max(0, mapGenerator.Length - 1)));
    }

    private static Vector2Int FindAvailableSpawnTile(
        ProceduralIsometricMapGenerator mapGenerator,
        Vector2Int requestedTile,
        HashSet<Vector2Int> occupiedTiles)
    {
        if (mapGenerator == null)
        {
            return Vector2Int.zero;
        }

        if (IsSpawnTileAvailable(mapGenerator, requestedTile, occupiedTiles))
        {
            return requestedTile;
        }

        Vector2Int center = requestedTile == default ? mapGenerator.GetCenterTile() : requestedTile;
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

        return mapGenerator.GetCenterTile();
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
