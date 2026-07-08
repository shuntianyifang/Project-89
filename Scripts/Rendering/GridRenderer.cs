using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;
using ColdWarWargame.Models;

namespace ColdWarWargame.Rendering
{
    public partial class GridRenderer : Node2D
    {
        [Export] public float CellSize { get; set; } = 32f;

        private GridMap _map;
        private List<(Battalion bat, Vector2I pos)> _blueUnits = new();
        private List<(Battalion bat, Vector2I pos)> _redUnits = new();
        private Dictionary<Vector2I, float> _reachableTiles = new();
        private Vector2I? _selectedPos = null;
        private string _statusText = "";

        static readonly Color Plain = new Color(0.63f, 0.82f, 0.50f);
        static readonly Color Forest = new Color(0.18f, 0.42f, 0.13f);
        static readonly Color SemiUrban = new Color(0.75f, 0.66f, 0.44f);
        static readonly Color Urban = new Color(0.55f, 0.45f, 0.33f);
        static readonly Color Highway = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        static readonly Color BlueFaction = new Color(0.27f, 0.53f, 1.0f);
        static readonly Color RedFaction = new Color(1.0f, 0.27f, 0.27f);
        static readonly Color SelBorder = new Color(1.0f, 1.0f, 0.0f);
        static readonly Color ReachableCol = new Color(0.0f, 0.8f, 0.0f, 0.25f);
        static readonly Color GridLine = new Color(0.0f, 0.0f, 0.0f, 0.05f);

        public Action<int, Battalion, Vector2I> OnUnitClicked;
        public Action<Vector2I> OnTileClicked;

        public void SetGrid(GridMap map) { _map = map; QueueRedraw(); }
        public void SetBlueUnits(List<(Battalion bat, Vector2I pos)> u) { _blueUnits = u; QueueRedraw(); }
        public void SetRedUnits(List<(Battalion bat, Vector2I pos)> u) { _redUnits = u; QueueRedraw(); }
        public void SetReachable(Dictionary<Vector2I, float> r) { _reachableTiles = r; QueueRedraw(); }
        public void ClearSel() { _selectedPos = null; _reachableTiles.Clear(); QueueRedraw(); }
        public void SetSel(Vector2I p) { _selectedPos = p; QueueRedraw(); }
        public void SetStatus(string s) { _statusText = s; QueueRedraw(); }

        public override void _Draw()
        {
            if (_map == null) return;
            DrawBackground();
            DrawTerrain();
            DrawHighway();
            DrawReachableTiles();
            DrawGridLines();
            DrawUnits();
            DrawSelection();

        }

        void DrawBackground() =>
            DrawRect(new Rect2(0, 0, _map.Width * CellSize, _map.Height * CellSize), Plain);

        void DrawTerrain()
        {
            for (int x = 0; x < _map.Width; x++)
                for (int y = 0; y < _map.Height; y++)
                {
                    int t = _map.GetTile(new Vector2I(x, y)).TerrainType;
                    if (t == 0) continue;
                    DrawRect(new Rect2(x * CellSize, y * CellSize, CellSize, CellSize),
                        t == 1 ? Forest : t == 2 ? SemiUrban : Urban);
                }
        }

        void DrawHighway()
        {
            for (int x = 0; x < _map.Width; x++)
                for (int y = 0; y < _map.Height; y++)
                {
                    var tile = _map.GetTile(new Vector2I(x, y));
                    if (tile.InfraType >= 1)
                    {
                        float cx = x * CellSize + CellSize / 2, cy = y * CellSize + CellSize / 2;
                        DrawRect(new Rect2(cx - 1, cy - 1, 2, 2), Highway);
                        if (x > 0 && _map.GetTile(new Vector2I(x - 1, y)).InfraType >= 1)
                            DrawLine(new Vector2(cx - CellSize, cy), new Vector2(cx, cy), Highway, 1);
                        if (y > 0 && _map.GetTile(new Vector2I(x, y - 1)).InfraType >= 1)
                            DrawLine(new Vector2(cx, cy - CellSize), new Vector2(cx, cy), Highway, 1);
                    }
                }
        }

        void DrawGridLines()
        {
            for (int x = 0; x <= _map.Width; x++)
                DrawLine(new Vector2(x * CellSize, 0), new Vector2(x * CellSize, _map.Height * CellSize), GridLine, 1);
            for (int y = 0; y <= _map.Height; y++)
                DrawLine(new Vector2(0, y * CellSize), new Vector2(_map.Width * CellSize, y * CellSize), GridLine, 1);
        }

        void DrawReachableTiles()
        {
            foreach (var pos in _reachableTiles.Keys)
                DrawRect(new Rect2(pos.X * CellSize, pos.Y * CellSize, CellSize, CellSize), ReachableCol);
        }

        void DrawUnits()
        {
            foreach (var (bat, pos) in _blueUnits) DrawUnit(pos, BlueFaction, "B");
            foreach (var (bat, pos) in _redUnits) DrawUnit(pos, RedFaction, "R");
        }

        void DrawUnit(Vector2I pos, Color color, string label)
        {
            float pad = 2f;
            var r = new Rect2(pos.X * CellSize + pad, pos.Y * CellSize + pad, CellSize - 2 * pad, CellSize - 2 * pad);
            DrawRect(r, color);
            DrawRect(r, Colors.Black, false, 1);
        }

        void DrawSelection()
        {
            if (_selectedPos == null) return;
            var p = _selectedPos.Value;
            DrawRect(new Rect2(p.X * CellSize, p.Y * CellSize, CellSize, CellSize), SelBorder, false, 3);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                var wp = GetGlobalMousePosition();
                var gp = new Vector2I((int)(wp.X / CellSize), (int)(wp.Y / CellSize));
                if (_map == null || !_map.IsInBounds(gp)) return;

                foreach (var (bat, pos) in _blueUnits) { if (pos == gp) { OnUnitClicked?.Invoke(1, bat, pos); return; } }
                foreach (var (bat, pos) in _redUnits) { if (pos == gp) { OnUnitClicked?.Invoke(2, bat, pos); return; } }
                OnTileClicked?.Invoke(gp);
            }
        }
    }
}
