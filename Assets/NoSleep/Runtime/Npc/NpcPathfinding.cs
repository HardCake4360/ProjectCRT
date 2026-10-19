using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public enum NpcMoveResult { Succeeded, Blocked, Invalid, Cancelled, Busy }

    public static class NpcPathfinding
    {
        // This order makes equally short routes reproducible across runs and saves.
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static bool TryFindPath(MapDefinition map, Vector2Int start, Vector2Int destination,
            Func<Vector2Int, bool> occupied, int nodeBudget, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (map == null || nodeBudget < 1 || !map.IsWalkable(start) || !map.IsWalkable(destination)) return false;
            if (start == destination) return true;
            if (occupied != null && occupied(destination)) return false;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            queue.Enqueue(start);
            int budget = Mathf.Min(65536, nodeBudget);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    Vector2Int next = cell + direction;
                    if (previous.ContainsKey(next) || !map.IsWalkable(next) || (occupied != null && occupied(next))) continue;
                    if (previous.Count >= budget) return false;
                    previous.Add(next, cell);
                    if (next == destination)
                    {
                        for (var cursor = destination; cursor != start; cursor = previous[cursor]) path.Add(cursor);
                        path.Reverse();
                        return true;
                    }
                    queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
