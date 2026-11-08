using System;
using System.Collections.Generic;
using System.Drawing;

namespace Revora.Game.Features
{
    // Per-actor route cache. Search and failed-route retries have fixed budgets.
    internal sealed class CompanionNavigation
    {
        internal const int SearchBudget = 256;
        private readonly List<Point> route = new List<Point>();
        private Point goal;
        private Point expected;
        private object world;
        private DateTime retryAt;
        internal int LastExpanded { get; private set; }

        private sealed class Node
        {
            internal Point Position;
            internal Node Parent;
            internal int Cost;
        }

        internal void Reset()
        {
            route.Clear();
            world = null;
            retryAt = DateTime.MinValue;
        }

        internal static int Distance(Point a, Point b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        internal static bool CanStep(Point a, Point b, Func<int, int, bool> open)
        {
            if (Distance(a, b) > 1 || b.X < 1 || b.Y < 1 || !open(b.X, b.Y)) return false;
            return a.X == b.X || a.Y == b.Y || (open(a.X, b.Y) && open(b.X, a.Y));
        }

        internal static bool HasSight(Point from, Point to, Func<int, int, bool> open)
        {
            int count = Distance(from, to);
            if (count > 64) return false;
            Point previous = from;
            for (int i = 1; i <= count; i++)
            {
                Point next = new Point(from.X + (to.X - from.X) * i / count,
                    from.Y + (to.Y - from.Y) * i / count);
                if (!CanStep(previous, next, open)) return false;
                previous = next;
            }
            return true;
        }

        internal bool Next(object map, Point start, Point destination, DateTime now,
            Func<int, int, bool> open, out Point next)
        {
            next = start;
            if (start == destination) return false;
            if (world != map || expected != start || goal != destination)
            {
                route.Clear();
                // Moving goals cannot bypass the failed-search backoff.
                if (world != map) retryAt = DateTime.MinValue;
            }
            world = map;
            goal = destination;
            expected = start;
            if (route.Count > 0 && !CanStep(start, route[0], open)) route.Clear();
            if (route.Count == 0)
            {
                if (now < retryAt) return false;
                FindRoute(start, destination, open);
                retryAt = now.AddSeconds(1);
            }
            if (route.Count == 0) return false;
            next = route[0];
            route.RemoveAt(0);
            expected = next;
            return true;
        }

        private void FindRoute(Point start, Point destination, Func<int, int, bool> open)
        {
            LastExpanded = 0;
            var frontier = new List<Node>();
            var costs = new Dictionary<Point, int>();
            var first = new Node { Position = start };
            frontier.Add(first);
            costs[start] = 0;
            Node best = first;
            while (frontier.Count > 0 && LastExpanded < SearchBudget)
            {
                int index = 0;
                for (int i = 1; i < frontier.Count; i++)
                    if (frontier[i].Cost + Distance(frontier[i].Position, destination) * 10 <
                        frontier[index].Cost + Distance(frontier[index].Position, destination) * 10)
                        index = i;
                Node node = frontier[index];
                frontier.RemoveAt(index);
                if (costs[node.Position] != node.Cost) continue;
                LastExpanded++;
                if (Distance(node.Position, destination) < Distance(best.Position, destination)) best = node;
                if (node.Position == destination) { best = node; break; }
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Point p = new Point(node.Position.X + dx, node.Position.Y + dy);
                        if (Distance(start, p) > 24 || !CanStep(node.Position, p, open)) continue;
                        int cost = node.Cost + 10;
                        int previous;
                        if (costs.TryGetValue(p, out previous) && previous <= cost) continue;
                        costs[p] = cost;
                        frontier.Add(new Node { Position = p, Parent = node, Cost = cost });
                    }
            }
            while (best.Parent != null)
            {
                route.Add(best.Position);
                best = best.Parent;
            }
            route.Reverse();
        }
    }

    internal enum CompanionStance { Assist, Protect, Passive }

    internal static class CompanionTactics
    {
        internal static int Score(CompanionStance stance, bool ownerTarget, bool ownerThreat,
            bool selfThreat, bool claimedByOther, bool current, int distance)
        {
            if (stance == CompanionStance.Passive || claimedByOther) return 0;
            int score = 0;
            if (ownerTarget && stance == CompanionStance.Assist) score = 10000;
            if (ownerThreat) score = Math.Max(score, stance == CompanionStance.Protect ? 20000 : 8000);
            if (selfThreat) score = Math.Max(score, 6000);
            return score == 0 ? 0 : score + (current ? 400 : 0) - distance * 20;
        }

        internal static bool Retreat(bool retreating, uint hp, uint maximum)
        {
            if (maximum == 0) return false;
            return (ulong)hp * 100 < (ulong)maximum * (retreating ? 60u : 25u);
        }
    }
}
