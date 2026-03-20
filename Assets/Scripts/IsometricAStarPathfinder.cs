using System.Collections.Generic;
using UnityEngine;

public static class IsometricAStarPathfinder
{
    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    public static List<Vector2Int> FindPath(
        ProceduralIsometricMapGenerator map,
        Vector2Int start,
        Vector2Int goal,
        int maxStepUp,
        int maxStepDown,
        System.Func<Vector2Int, bool> isBlocked = null)
    {
        if (map == null || !map.HasGeneratedMap)
        {
            return null;
        }

        if (!map.IsTraversable(start.x, start.y) || !map.IsTraversable(goal.x, goal.y))
        {
            return null;
        }

        List<Vector2Int> openSet = new List<Vector2Int> { start };
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, int> fScore = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, goal) };

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestScoreNode(openSet, fScore);
            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            int currentElevation = map.GetTileElevation(current.x, current.y);
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                Vector2Int neighbor = current + NeighborOffsets[i];
                if (closedSet.Contains(neighbor) || !map.IsTraversable(neighbor.x, neighbor.y))
                {
                    continue;
                }

                if (isBlocked != null && neighbor != goal && isBlocked(neighbor))
                {
                    continue;
                }

                int neighborElevation = map.GetTileElevation(neighbor.x, neighbor.y);
                int elevationDelta = neighborElevation - currentElevation;
                if (elevationDelta > maxStepUp || -elevationDelta > maxStepDown)
                {
                    continue;
                }

                int tentativeGScore = gScore[current] + GetTraversalCost(elevationDelta);
                int neighborGScore = gScore.TryGetValue(neighbor, out int existingGScore) ? existingGScore : int.MaxValue;
                if (tentativeGScore >= neighborGScore)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
            }
        }

        return null;
    }

    private static int GetTraversalCost(int elevationDelta)
    {
        int baseCost = 10;
        if (elevationDelta > 0)
        {
            return baseCost + (elevationDelta * 6);
        }

        return baseCost + Mathf.Abs(elevationDelta * 2);
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * 10;
    }

    private static Vector2Int GetLowestScoreNode(IReadOnlyList<Vector2Int> openSet, IReadOnlyDictionary<Vector2Int, int> fScore)
    {
        Vector2Int bestNode = openSet[0];
        int bestScore = fScore.TryGetValue(bestNode, out int initialScore) ? initialScore : int.MaxValue;

        for (int i = 1; i < openSet.Count; i++)
        {
            Vector2Int candidate = openSet[i];
            int candidateScore = fScore.TryGetValue(candidate, out int score) ? score : int.MaxValue;
            if (candidateScore < bestScore)
            {
                bestNode = candidate;
                bestScore = candidateScore;
            }
        }

        return bestNode;
    }

    private static List<Vector2Int> ReconstructPath(IReadOnlyDictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
