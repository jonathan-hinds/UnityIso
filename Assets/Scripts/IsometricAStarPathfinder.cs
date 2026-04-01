using System.Collections.Generic;
using UnityEngine;

public static class IsometricAStarPathfinder
{
    private static readonly Stack<List<Vector2Int>> ListPool = new();
    private static readonly Stack<HashSet<Vector2Int>> HashSetPool = new();
    private static readonly Stack<Dictionary<Vector2Int, Vector2Int>> ParentMapPool = new();
    private static readonly Stack<Dictionary<Vector2Int, int>> ScoreMapPool = new();

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

        List<Vector2Int> openSet = GetList();
        HashSet<Vector2Int> openLookup = GetHashSet();
        HashSet<Vector2Int> closedSet = GetHashSet();
        Dictionary<Vector2Int, Vector2Int> cameFrom = GetParentMap();
        Dictionary<Vector2Int, int> gScore = GetScoreMap();
        Dictionary<Vector2Int, int> fScore = GetScoreMap();

        openSet.Add(start);
        openLookup.Add(start);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        try
        {
            while (openSet.Count > 0)
            {
                Vector2Int current = GetLowestScoreNode(openSet, fScore);
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);
                openLookup.Remove(current);
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

                    if (!openLookup.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                        openLookup.Add(neighbor);
                    }
                }
            }

            return null;
        }
        finally
        {
            ReturnList(openSet);
            ReturnHashSet(openLookup);
            ReturnHashSet(closedSet);
            ReturnParentMap(cameFrom);
            ReturnScoreMap(gScore);
            ReturnScoreMap(fScore);
        }
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

    private static List<Vector2Int> GetList()
    {
        if (ListPool.Count == 0)
        {
            return new List<Vector2Int>();
        }

        List<Vector2Int> list = ListPool.Pop();
        list.Clear();
        return list;
    }

    private static void ReturnList(List<Vector2Int> list)
    {
        if (list == null)
        {
            return;
        }

        list.Clear();
        ListPool.Push(list);
    }

    private static HashSet<Vector2Int> GetHashSet()
    {
        if (HashSetPool.Count == 0)
        {
            return new HashSet<Vector2Int>();
        }

        HashSet<Vector2Int> set = HashSetPool.Pop();
        set.Clear();
        return set;
    }

    private static void ReturnHashSet(HashSet<Vector2Int> set)
    {
        if (set == null)
        {
            return;
        }

        set.Clear();
        HashSetPool.Push(set);
    }

    private static Dictionary<Vector2Int, Vector2Int> GetParentMap()
    {
        if (ParentMapPool.Count == 0)
        {
            return new Dictionary<Vector2Int, Vector2Int>();
        }

        Dictionary<Vector2Int, Vector2Int> map = ParentMapPool.Pop();
        map.Clear();
        return map;
    }

    private static void ReturnParentMap(Dictionary<Vector2Int, Vector2Int> map)
    {
        if (map == null)
        {
            return;
        }

        map.Clear();
        ParentMapPool.Push(map);
    }

    private static Dictionary<Vector2Int, int> GetScoreMap()
    {
        if (ScoreMapPool.Count == 0)
        {
            return new Dictionary<Vector2Int, int>();
        }

        Dictionary<Vector2Int, int> map = ScoreMapPool.Pop();
        map.Clear();
        return map;
    }

    private static void ReturnScoreMap(Dictionary<Vector2Int, int> map)
    {
        if (map == null)
        {
            return;
        }

        map.Clear();
        ScoreMapPool.Push(map);
    }
}
