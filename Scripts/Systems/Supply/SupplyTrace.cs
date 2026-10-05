using System.Collections.Generic;
using Godot;

namespace ColdWarWargame.Systems.Supply
{
    // Immutable links preserve the exact winning path when later runs activate hubs.
    public sealed record SupplyRoute(Vector2I Position, string SourceKind, float Budget,
        float Cost, SupplyRoute Previous)
    {
        public List<Vector2I> Path()
        {
            var path = new List<Vector2I>();
            for (var node = this; node != null; node = node.Previous) path.Add(node.Position);
            path.Reverse();
            return path;
        }
    }

    public sealed class SupplyTrace
    {
        public SupplyRoute[,] Routes { get; private set; }
        public HashSet<Vector2I> Blocked { get; private set; }
        public void Reset(int width, int height, HashSet<Vector2I> blocked)
        {
            Routes = new SupplyRoute[width, height];
            Blocked = new HashSet<Vector2I>(blocked);
        }
    }
}
