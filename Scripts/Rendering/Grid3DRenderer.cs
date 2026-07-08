using Godot;
using System;
using System.Collections.Generic;
using ColdWarWargame.Systems.Battlefield;
using ColdWarWargame.Models;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;

namespace ColdWarWargame.Rendering
{
    public partial class Grid3DRenderer : Node3D
    {
        [Export] public float CellSize { get; set; } = 1.0f;

        private GridMap _map;
        private List<(Battalion bat, Vector2I pos)> _blueUnits = new();
        private List<(Battalion bat, Vector2I pos)> _redUnits = new();
        private Dictionary<Vector2I, float> _reachableTiles = new();
        private Vector2I? _selectedPos = null;
        private List<MeshInstance3D> _highlightMeshes = new();
        private Node3D _unitRoot;
        private Node3D _highlightRoot;

        static readonly Color[] TerrainColors = new[] {
            new Color(0.63f, 0.82f, 0.50f), // 0 Plain
            new Color(0.18f, 0.42f, 0.13f), // 1 Forest
            new Color(0.75f, 0.66f, 0.44f), // 2 SemiUrban
            new Color(0.55f, 0.45f, 0.33f), // 3 Urban
        };

        public Action<int, Battalion, Vector2I> OnUnitClicked;
        public Action<Vector2I> OnTileClicked;
        private Camera3D _camRef;

        public void SetGrid(GridMap map) { _map = map; BuildTerrain(); }
        public void SetBlueUnits(List<(Battalion bat, Vector2I pos)> u) { _blueUnits = u; BuildUnits(); }
        public void SetRedUnits(List<(Battalion bat, Vector2I pos)> u) { _redUnits = u; BuildUnits(); }
        public void SetReachable(Dictionary<Vector2I, float> r) { _reachableTiles = r; UpdateHighlights(); }
        public void ClearSel() { _selectedPos = null; _reachableTiles.Clear(); UpdateHighlights(); }
        public void SetSel(Vector2I p) { _selectedPos = p; UpdateHighlights(); }

        void BuildTerrain()
        {
            if (_map == null) return;
            int w = _map.Width, h = _map.Height;

            var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var tile = _map.GetTile(new Vector2I(x, y));
                    var col = TerrainColors[tile.TerrainType];
                    if (tile.InfraType == 2) col = col.Lerp(new Color(0.6f, 0.6f, 0.6f), 0.4f);
                    else if (tile.InfraType == 1) col = col.Lerp(new Color(0.8f, 0.7f, 0.5f), 0.3f);
                    img.SetPixel(x, y, col);
                }

            var tex = ImageTexture.CreateFromImage(img);
            var mat = new StandardMaterial3D();
            mat.AlbedoTexture = tex;
            

            float gw = w * CellSize, gh = h * CellSize;
            var quad = new QuadMesh { Size = new Vector2(gw, gh) };

            var mi = new MeshInstance3D(); mi.Mesh = quad; mi.MaterialOverride = mat; mi.RotateX(-Mathf.Pi / 2);
            mi.Position = new Vector3(gw / 2, 0, gh / 2);
            AddChild(mi);

            // Grid border
            var borderMat = new StandardMaterial3D();
            borderMat.AlbedoColor = new Color(0.2f, 0.2f, 0.2f);
            borderMat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
            var border = new MeshInstance3D(); border.Mesh = new QuadMesh { Size = new Vector2(gw + 0.1f, gh + 0.1f) }; border.MaterialOverride = borderMat;
            border.RotateX(-Mathf.Pi / 2);
            border.Position = new Vector3(gw / 2, -0.01f, gh / 2);
            AddChild(border);
        }

        void BuildUnits()
        {
            if (_unitRoot != null) { RemoveChild(_unitRoot); _unitRoot.QueueFree(); }
            _unitRoot = new Node3D(); AddChild(_unitRoot);

            foreach (var (bat, pos) in _blueUnits) CreateUnitMesh(pos, Colors.DodgerBlue);
            foreach (var (bat, pos) in _redUnits) CreateUnitMesh(pos, Colors.IndianRed);
        }

        void CreateUnitMesh(Vector2I pos, Color color)
        {
            float cx = pos.X * CellSize + CellSize / 2;
            float cz = pos.Y * CellSize + CellSize / 2;
            var mat = new StandardMaterial3D { AlbedoColor = color, ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
            var mesh = new MeshInstance3D();
            mesh.Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.35f, Height = 0.25f, Material = mat };
            mesh.Position = new Vector3(cx, 0.15f, cz);
            mesh.RotateX(Mathf.Pi / 2);
            _unitRoot.AddChild(mesh);
        }

        void UpdateHighlights()
        {
            foreach (var m in _highlightMeshes) { m.QueueFree(); }
            _highlightMeshes.Clear();
            if (_highlightRoot == null) { _highlightRoot = new Node3D(); AddChild(_highlightRoot); }

            // Reachable tiles
            var reachableMat = new StandardMaterial3D { AlbedoColor = new Color(0, 0.8f, 0, 0.3f), ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
            foreach (var kv in _reachableTiles)
            {
                var m = MakeHighlight(kv.Key, reachableMat, 0.02f);
                _highlightMeshes.Add(m); _highlightRoot.AddChild(m);
            }

            // Selection
            if (_selectedPos != null)
            {
                var selMat = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 0, 0.5f), ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
                var m = MakeHighlight(_selectedPos.Value, selMat, 0.04f);
                _highlightMeshes.Add(m); _highlightRoot.AddChild(m);
            }
        }

        MeshInstance3D MakeHighlight(Vector2I pos, Material mat, float height)
        {
            float cx = pos.X * CellSize + CellSize / 2;
            float cz = pos.Y * CellSize + CellSize / 2;
            return new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.95f, height, CellSize * 0.95f), Material = mat },
                Position = new Vector3(cx, height / 2 + 0.001f, cz)
            };
        }

        // ===== Mouse interaction =====
        public void SetCameraRef(Camera3D cam) { _camRef = cam; }

        Vector2I? ScreenToGrid(Vector2 screenPos)
        {
            var cam = _camRef ?? GetViewport()?.GetCamera3D();
            if (cam == null) return null;

            var origin = cam.ProjectRayOrigin(screenPos);
            var dir = cam.ProjectRayNormal(screenPos);
            if (dir.Y >= 0) return null;

            float t = -origin.Y / dir.Y;
            if (t < 0) return null;
            var point = origin + t * dir;

            int gx = (int)(point.X / CellSize);
            int gy = (int)(point.Z / CellSize);
            if (_map == null || !_map.IsInBounds(new Vector2I(gx, gy))) return null;
            return new Vector2I(gx, gy);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                var gridPos = ScreenToGrid(mb.Position);
                if (gridPos == null) return;
                var gp = gridPos.Value;

                foreach (var (bat, pos) in _blueUnits) { if (pos == gp) { OnUnitClicked?.Invoke(1, bat, pos); return; } }
                foreach (var (bat, pos) in _redUnits) { if (pos == gp) { OnUnitClicked?.Invoke(2, bat, pos); return; } }
                OnTileClicked?.Invoke(gp);
            }
        }
    }
}
