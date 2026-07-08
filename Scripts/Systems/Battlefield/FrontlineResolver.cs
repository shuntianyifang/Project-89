using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ColdWarWargame.Systems.Battlefield
{
    public readonly struct FrontlineSegment
    {
        public FrontlineSegment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        public Vector2 Start { get; }
        public Vector2 End { get; }
    }

    public readonly struct FrontlineChain
    {
        public FrontlineChain(IReadOnlyList<Vector2> points, bool blueOnPositiveNormal)
        {
            Points = points;
            BlueOnPositiveNormal = blueOnPositiveNormal;
        }

        public IReadOnlyList<Vector2> Points { get; }
        public bool BlueOnPositiveNormal { get; }
    }

    /// <summary>
    /// Frontline 规则提取器：只从地块控制权归属图提取边界线段。
    /// </summary>
    public sealed class FrontlineResolver
    {
        private readonly struct PointKey : IEquatable<PointKey>
        {
            public PointKey(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }

            public bool Equals(PointKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is PointKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(X, Y);
        }

        public int[,] BuildOwnershipMap(int[,] controlMap)
        {
            int width = controlMap.GetLength(0);
            int height = controlMap.GetLength(1);
            var ownership = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    ownership[x, y] = controlMap[x, y];
            }

            return ownership;
        }

        public int[,] BuildOwnershipMap(
            GridMap map,
            IEnumerable<Vector2I> blueUnitPositions,
            IEnumerable<Vector2I> redUnitPositions,
            ZOCManager zocMgr)
        {
            int width = map.Width;
            int height = map.Height;
            var ownership = new int[width, height];

            var blueZoc = zocMgr.GetFactionZOC(blueUnitPositions);
            var redZoc = zocMgr.GetFactionZOC(redUnitPositions);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var pos = new Vector2I(x, y);
                    bool inBlue = blueZoc.Contains(pos);
                    bool inRed = redZoc.Contains(pos);

                    if (inBlue && !inRed) ownership[x, y] = 1;
                    else if (inRed && !inBlue) ownership[x, y] = 2;
                    else ownership[x, y] = 0;
                }
            }

            return ownership;
        }

        public List<FrontlineSegment> ResolveFrontline(int[,] controlMap)
        {
            var ownership = BuildOwnershipMap(controlMap);
            return ExtractBoundarySegments(ownership);
        }

        public List<FrontlineChain> ResolveFrontlineChains(int[,] controlMap)
        {
            var ownership = BuildOwnershipMap(controlMap);
            return BuildOrientedChains(ownership);
        }

        public List<FrontlineSegment> ExtractBoundarySegments(int[,] ownership)
        {
            int width = ownership.GetLength(0);
            int height = ownership.GetLength(1);
            var segments = new List<FrontlineSegment>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int current = ownership[x, y];
                    if (current == 0) continue;

                    if (x + 1 < width)
                    {
                        int right = ownership[x + 1, y];
                        if (right != current)
                            segments.Add(new FrontlineSegment(new Vector2(x + 1, y), new Vector2(x + 1, y + 1)));
                    }

                    if (y + 1 < height)
                    {
                        int down = ownership[x, y + 1];
                        if (down != current)
                            segments.Add(new FrontlineSegment(new Vector2(x, y + 1), new Vector2(x + 1, y + 1)));
                    }
                }
            }

            return MergeCollinearSegments(segments);
        }

        private List<FrontlineChain> BuildOrientedChains(int[,] ownership)
        {
            var segments = ExtractBoundarySegments(ownership);
            var chains = TraceChainsFromSegments(segments);
            var oriented = new List<FrontlineChain>();

            foreach (var chain in chains)
            {
                if (chain.Count < 2)
                    continue;

                var smooth = SmoothChain(chain);
                bool blueOnPositiveNormal = DetermineBlueOnPositiveNormal(ownership, smooth);
                oriented.Add(new FrontlineChain(smooth, blueOnPositiveNormal));
            }

            return oriented;
        }

        private static List<List<Vector2>> TraceChainsFromSegments(List<FrontlineSegment> segments)
        {
            var segmentPoints = new List<(PointKey start, PointKey end, Vector2 startValue, Vector2 endValue, bool used)>();
            var adjacency = new Dictionary<PointKey, List<int>>();

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var startKey = new PointKey((int)Math.Round(segment.Start.X), (int)Math.Round(segment.Start.Y));
                var endKey = new PointKey((int)Math.Round(segment.End.X), (int)Math.Round(segment.End.Y));
                segmentPoints.Add((startKey, endKey, segment.Start, segment.End, false));

                if (!adjacency.TryGetValue(startKey, out var startList))
                {
                    startList = new List<int>();
                    adjacency[startKey] = startList;
                }
                startList.Add(i);

                if (!adjacency.TryGetValue(endKey, out var endList))
                {
                    endList = new List<int>();
                    adjacency[endKey] = endList;
                }
                endList.Add(i);
            }

            var chains = new List<List<Vector2>>();
            for (int i = 0; i < segmentPoints.Count; i++)
            {
                if (segmentPoints[i].used)
                    continue;

                var chain = new List<Vector2>();
                var currentIndex = i;
                var current = segmentPoints[currentIndex];
                MarkUsed(segmentPoints, currentIndex);
                chain.Add(current.startValue);
                chain.Add(current.endValue);

                ExtendChain(chain, current.end, adjacency, segmentPoints, current.endValue - current.startValue);
                ExtendChainBackward(chain, current.start, adjacency, segmentPoints, current.startValue - current.endValue);

                chains.Add(chain);
            }

            return chains;
        }

        private static void ExtendChain(List<Vector2> chain, PointKey anchor, Dictionary<PointKey, List<int>> adjacency, List<(PointKey start, PointKey end, Vector2 startValue, Vector2 endValue, bool used)> segmentPoints, Vector2 currentDirection)
        {
            var current = anchor;
            while (adjacency.TryGetValue(current, out var candidates))
            {
                int nextIndex = SelectNextSegment(chain, current, currentDirection, candidates, segmentPoints, forward: true);
                if (nextIndex < 0)
                    return;

                var seg = segmentPoints[nextIndex];
                MarkUsed(segmentPoints, nextIndex);
                var nextPoint = seg.start.Equals(current) ? seg.endValue : seg.startValue;
                var nextKey = seg.start.Equals(current) ? seg.end : seg.start;

                if (chain.Count == 0 || !PointsEqual(chain[^1], nextPoint))
                    chain.Add(nextPoint);

                current = nextKey;
                currentDirection = nextPoint - chain[^2];
                if (PointsEqual(chain[0], chain[^1]))
                    return;
            }
        }

        private static void ExtendChainBackward(List<Vector2> chain, PointKey anchor, Dictionary<PointKey, List<int>> adjacency, List<(PointKey start, PointKey end, Vector2 startValue, Vector2 endValue, bool used)> segmentPoints, Vector2 currentDirection)
        {
            var current = anchor;
            while (adjacency.TryGetValue(current, out var candidates))
            {
                int nextIndex = SelectNextSegment(chain, current, currentDirection, candidates, segmentPoints, forward: false);
                if (nextIndex < 0)
                    return;

                var seg = segmentPoints[nextIndex];
                MarkUsed(segmentPoints, nextIndex);
                var nextPoint = seg.start.Equals(current) ? seg.endValue : seg.startValue;
                var nextKey = seg.start.Equals(current) ? seg.end : seg.start;

                if (chain.Count == 0 || !PointsEqual(chain[0], nextPoint))
                    chain.Insert(0, nextPoint);

                current = nextKey;
                currentDirection = chain[1] - nextPoint;
                if (PointsEqual(chain[0], chain[^1]))
                    return;
            }
        }

        private static int SelectNextSegment(
            List<Vector2> chain,
            PointKey current,
            Vector2 currentDirection,
            List<int> candidates,
            List<(PointKey start, PointKey end, Vector2 startValue, Vector2 endValue, bool used)> segmentPoints,
            bool forward)
        {
            float bestScore = float.MaxValue;
            int bestIndex = -1;

            foreach (int candidateIndex in candidates)
            {
                if (segmentPoints[candidateIndex].used)
                    continue;

                var seg = segmentPoints[candidateIndex];
                Vector2 nextPoint = seg.start.Equals(current)
                    ? (forward ? seg.endValue : seg.startValue)
                    : (forward ? seg.startValue : seg.endValue);

                Vector2 dir = (nextPoint - (forward ? chain[^1] : chain[0]));
                if (dir.LengthSquared() < 0.0001f)
                    dir = currentDirection;

                float score = AngleDifference(currentDirection, dir);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = candidateIndex;
                }
            }

            return bestIndex;
        }

        private static float AngleDifference(Vector2 a, Vector2 b)
        {
            if (a.LengthSquared() < 0.0001f || b.LengthSquared() < 0.0001f)
                return 0f;

            float dot = Mathf.Clamp(a.Normalized().Dot(b.Normalized()), -1f, 1f);
            return Mathf.Acos(dot);
        }

        private static void MarkUsed(List<(PointKey start, PointKey end, Vector2 startValue, Vector2 endValue, bool used)> segmentPoints, int index)
        {
            var item = segmentPoints[index];
            item.used = true;
            segmentPoints[index] = item;
        }

        private static bool PointsEqual(Vector2 a, Vector2 b) => Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Y - b.Y) < 0.001f;

        private static List<Vector2> SmoothChain(IReadOnlyList<Vector2> chain, int subdivisions = 4)
        {
            if (chain.Count <= 2)
                return chain.ToList();

            var points = new List<Vector2>();
            points.Add(chain[0]);

            for (int i = 0; i < chain.Count - 1; i++)
            {
                var p0 = i > 0 ? chain[i - 1] : chain[i];
                var p1 = chain[i];
                var p2 = chain[i + 1];
                var p3 = i + 2 < chain.Count ? chain[i + 2] : chain[i + 1];

                for (int step = 1; step <= subdivisions; step++)
                {
                    float t = step / (float)subdivisions;
                    points.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return points;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static bool DetermineBlueOnPositiveNormal(int[,] ownership, IReadOnlyList<Vector2> chain)
        {
            if (chain.Count < 2)
                return true;

            Vector2 a = chain[0];
            Vector2 b = chain[1];
            Vector2 dir = b - a;
            if (dir.LengthSquared() < 0.0001f)
                return true;

            Vector2 normal = new Vector2(-dir.Y, dir.X).Normalized();
            Vector2 mid = (a + b) * 0.5f;
            int positive = SampleOwnership(ownership, mid + normal * 0.35f);
            int negative = SampleOwnership(ownership, mid - normal * 0.35f);

            if (positive == 1 && negative == 2)
                return true;
            if (positive == 2 && negative == 1)
                return false;

            return positive == 1;
        }

        private static int SampleOwnership(int[,] ownership, Vector2 point)
        {
            int x = (int)Math.Floor(point.X);
            int y = (int)Math.Floor(point.Y);
            int width = ownership.GetLength(0);
            int height = ownership.GetLength(1);

            x = Math.Clamp(x, 0, width - 1);
            y = Math.Clamp(y, 0, height - 1);
            return ownership[x, y];
        }

        static List<FrontlineSegment> MergeCollinearSegments(List<FrontlineSegment> segments)
        {
            if (segments.Count <= 1) return segments;

            var horizontal = new Dictionary<float, List<(float start, float end)>>();
            var vertical = new Dictionary<float, List<(float start, float end)>>();

            foreach (var seg in segments)
            {
                if (Math.Abs(seg.Start.Y - seg.End.Y) < 0.0001f)
                {
                    float y = seg.Start.Y;
                    float start = Math.Min(seg.Start.X, seg.End.X);
                    float end = Math.Max(seg.Start.X, seg.End.X);
                    if (!horizontal.TryGetValue(y, out var list))
                    {
                        list = new List<(float start, float end)>();
                        horizontal[y] = list;
                    }
                    list.Add((start, end));
                }
                else if (Math.Abs(seg.Start.X - seg.End.X) < 0.0001f)
                {
                    float x = seg.Start.X;
                    float start = Math.Min(seg.Start.Y, seg.End.Y);
                    float end = Math.Max(seg.Start.Y, seg.End.Y);
                    if (!vertical.TryGetValue(x, out var list))
                    {
                        list = new List<(float start, float end)>();
                        vertical[x] = list;
                    }
                    list.Add((start, end));
                }
            }

            var merged = new List<FrontlineSegment>();
            foreach (var kv in horizontal)
            {
                foreach (var (start, end) in MergeRanges(kv.Value))
                    merged.Add(new FrontlineSegment(new Vector2(start, kv.Key), new Vector2(end, kv.Key)));
            }
            foreach (var kv in vertical)
            {
                foreach (var (start, end) in MergeRanges(kv.Value))
                    merged.Add(new FrontlineSegment(new Vector2(kv.Key, start), new Vector2(kv.Key, end)));
            }

            return merged;
        }

        static List<(float start, float end)> MergeRanges(List<(float start, float end)> ranges)
        {
            var ordered = ranges.OrderBy(r => r.start).ThenBy(r => r.end).ToList();
            var result = new List<(float start, float end)>();
            foreach (var range in ordered)
            {
                if (result.Count == 0)
                {
                    result.Add(range);
                    continue;
                }

                var last = result[^1];
                if (range.start <= last.end + 0.0001f)
                    result[^1] = (last.start, Math.Max(last.end, range.end));
                else
                    result.Add(range);
            }
            return result;
        }
    }
}