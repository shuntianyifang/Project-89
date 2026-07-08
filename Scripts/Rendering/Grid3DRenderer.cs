using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private HashSet<Vector2I> _artilleryRangeTiles = new();
        private Vector2I? _selectedPos = null;
        private List<MeshInstance3D> _highlightMeshes = new();
        private Node3D _unitRoot;
        private Node3D _highlightRoot;
        private Node3D _linesRoot;
        private float _flashTimer = 0f;
        private bool _flashOn = true;
        private float _selectionAlpha = 0.6f;
        private float _selectedUnitAP = 0f;

      private class UnitVis { public MeshInstance3D Body; public Node3D Root; public Label InPanelName; public Vector2I GridPos; }
      private List<UnitVis> _unitVisuals = new();
       private Vector2? _rightClickStart = null;
       private Vector2I? _hoveredPos = null;
        private Node3D _pathRoot;
        private bool _isAnimating;
        private System.Collections.Generic.List<Vector2I> _animPath;
        private int _animIndex;
        private float _animTimer;
        private UnitVis _animUnit;

        static readonly Color[] TerrainColors = new[] {
            new Color(0.63f, 0.82f, 0.50f),
            new Color(0.18f, 0.42f, 0.13f),
            new Color(0.5f, 0.5f, 0.5f),
            new Color(0.1f, 0.1f, 0.1f),
        };

        public Action<int, Battalion, Vector2I> OnUnitClicked;
        public Action<Vector2I> OnTileClicked;
        public Action OnRightClick;
       public Action<Vector2I?> OnHoverChanged;
        public System.Action OnMoveFinished;
        private Camera3D _camRef;

        public void SetGrid(GridMap map) { _map = map; BuildTerrain(); }
        public void SetBlueUnits(List<(Battalion bat, Vector2I pos)> u) { _blueUnits = u; BuildUnits(); }
        public void SetRedUnits(List<(Battalion bat, Vector2I pos)> u) { _redUnits = u; BuildUnits(); }
        public void SetReachable(Dictionary<Vector2I, float> r, float uap = 0f) { _reachableTiles = r; _selectedUnitAP = uap; UpdateHighlights(); }
        public void ClearSel() { _selectedPos = null; _reachableTiles.Clear(); _artilleryRangeTiles.Clear(); foreach (var uv in _unitVisuals) if (uv.InPanelName != null) uv.InPanelName.Visible = false; UpdateHighlights(); }
        public void SetArtilleryRange(HashSet<Vector2I> tiles) { _artilleryRangeTiles = tiles ?? new(); UpdateHighlights(); }
        public void ClearArtilleryRange() { _artilleryRangeTiles.Clear(); UpdateHighlights(); }
        public void SetSel(Vector2I p) { _selectedPos = p; foreach (var uv in _unitVisuals) if (uv.InPanelName != null) uv.InPanelName.Visible = (uv.GridPos == p); UpdateHighlights(); }

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
            mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            float gw = w * CellSize, gh = h * CellSize;
            var mi = new MeshInstance3D();
            mi.Mesh = new QuadMesh { Size = new Vector2(gw, gh) };
            mi.MaterialOverride = mat;
            mi.RotateX(-Mathf.Pi / 2);
            mi.Position = new Vector3(gw / 2, 0, gh / 2);
            AddChild(mi);

            if (_linesRoot != null) { RemoveChild(_linesRoot); _linesRoot.QueueFree(); }
            _linesRoot = new Node3D(); AddChild(_linesRoot);
            var lm = new StandardMaterial3D { AlbedoColor = Colors.Black, ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
            float lw = 0.03f;
            for (int x = 0; x <= w; x++) { var l = new MeshInstance3D(); l.Mesh = new BoxMesh { Size = new Vector3(lw, 0.02f, gh + 0.1f) }; l.MaterialOverride = lm; l.Position = new Vector3(x, 0.005f, gh / 2); _linesRoot.AddChild(l); }
            for (int y = 0; y <= h; y++) { var l = new MeshInstance3D(); l.Mesh = new BoxMesh { Size = new Vector3(gw + 0.1f, 0.02f, lw) }; l.MaterialOverride = lm; l.Position = new Vector3(gw / 2, 0.005f, y); _linesRoot.AddChild(l); }
        }

        void BuildUnits()
        {
            if (_unitRoot != null) { RemoveChild(_unitRoot); _unitRoot.QueueFree(); }
            _unitRoot = new Node3D(); AddChild(_unitRoot); _unitVisuals.Clear();
            foreach (var u in _blueUnits) CreateUnitVis(u.pos, new Color(0.27f, 0.53f, 1.0f), new Color(0.5f, 0.7f, 1.0f), u.bat);
            foreach (var u in _redUnits) CreateUnitVis(u.pos, new Color(1.0f, 0.27f, 0.27f), new Color(1.0f, 0.6f, 0.6f), u.bat);
        }

        void CreateUnitVis(Vector2I pos, Color bodyCol, Color topCol, Battalion bat)
        {
            float cx = pos.X * CellSize + CellSize / 2, cz = pos.Y * CellSize + CellSize / 2;
            var root = new Node3D(); root.Position = new Vector3(cx, 0, cz);
            _unitRoot.AddChild(root);

            var bm = new StandardMaterial3D { AlbedoColor = bodyCol, ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
            var body = new MeshInstance3D();
            body.Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.2f, 0.7f) };
            body.MaterialOverride = bm; body.Position = new Vector3(0, 0.1f, 0);
            root.AddChild(body);

            var labelRoot = BuildLabel(bat, topCol);
            labelRoot.Position = new Vector3(0, 1.5f, 0);
            root.AddChild(labelRoot);


        var vp = labelRoot.GetChild<SubViewport>(0); var nameLbl = vp.GetChild<Label>(vp.GetChildCount() - 1); _unitVisuals.Add(new UnitVis { Body = body, Root = root, GridPos = pos, InPanelName = nameLbl }); }

        Node3D BuildLabel(Battalion bat, Color topColor)
        {
            var root = new Node3D();
            int wL = 600, hL = 690;
            var vp = new SubViewport();
            vp.Size = new Vector2I(wL, hL);
            vp.TransparentBg = true; vp.Disable3D = true;
            vp.Set("update_mode", 2); vp.Set("render_target_update_mode", 2);
            root.AddChild(vp);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.85f);
            style.CornerRadiusTopLeft = 6; style.CornerRadiusTopRight = 6;
            style.CornerRadiusBottomLeft = 6; style.CornerRadiusBottomRight = 6;
            var panel = new Panel();
            panel.Size = new Vector2I(wL, 600);
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.AddThemeStyleboxOverride("panel", style);
            vp.AddChild(panel);

            // Top 1/8 bar
            var topBar = new ColorRect();
            topBar.Color = topColor;
            topBar.Size = new Vector2I(wL, 75);
            topBar.Position = new Vector2I(0, 0);
            panel.AddChild(topBar);

            // Fatigue blocks
            bool disorganized = bat.Fatigue > 8;
            if (!disorganized)
            {
                for (int f = 0; f < Math.Min(bat.Fatigue, 8); f++)
                {
                    Color fc = f < 2 ? new Color(1f, 1f, 0.3f) : f < 4 ? new Color(1f, 1f, 0f) : f < 6 ? new Color(1f, 0.65f, 0f) : new Color(1f, 0f, 0f);
                    var blk = new ColorRect();
                    blk.Color = fc;
                    blk.Size = new Vector2I(30, 40);
                    blk.Position = new Vector2I(12 + f * 36, 17);
                    panel.AddChild(blk);
                    var dot = new ColorRect();
                    dot.Color = new Color(0.5f, 0.3f, 0.1f);
                    dot.Size = new Vector2I(8, 14);
                    dot.Position = new Vector2I(12 + f * 36 + 11, 30);
                    panel.AddChild(dot);
                }
            }

            // AP and disorganized text on top bar
            if (disorganized)
            {
                var od = new Label();
                od.Text = "ORGANIZATION COLLAPSE";
                od.Size = new Vector2I(400, 75);
                od.Position = new Vector2I(10, 0);
                od.VerticalAlignment = VerticalAlignment.Center;
                od.AddThemeFontSizeOverride("font_size", 28);
                od.AddThemeColorOverride("font_color", Colors.Red);
                panel.AddChild(od);
            }

            var ap = new Label();
            ap.Text = bat.CurrentAP.ToString("0.0");
            ap.Size = new Vector2I(100, 75);
            ap.Position = new Vector2I(wL - 120, 0);
            ap.HorizontalAlignment = HorizontalAlignment.Right;
            ap.VerticalAlignment = VerticalAlignment.Center;
            ap.AddThemeFontSizeOverride("font_size", 36);
            ap.AddThemeColorOverride("font_color", Colors.White);
            panel.AddChild(ap);

            // Icon placeholder
            var ib = new ColorRect();
            ib.Color = topColor.Lerp(new Color(1, 1, 1), 0.3f);
            ib.Size = new Vector2I(220, 220);
            ib.Position = new Vector2I(wL / 2 - 110, 155);
            panel.AddChild(ib);
            var it = new Label();
            it.Text = "TYPE";
            it.Size = new Vector2I(220, 220);
            it.Position = new Vector2I(wL / 2 - 110, 155);
            it.HorizontalAlignment = HorizontalAlignment.Center;
            it.VerticalAlignment = VerticalAlignment.Center;
            it.AddThemeFontSizeOverride("font_size", 24);
            it.AddThemeColorOverride("font_color", Colors.White);
            panel.AddChild(it);

            // ATK/DEF boxes at bottom, numbers only
            float atk = bat.GetActualAttack(), def = bat.GetActualDefense();

            var ab = new ColorRect();
            ab.Color = new Color(0, 0, 0);
            ab.Size = new Vector2I(180, 70);
            ab.Position = new Vector2I(20, 520);
            panel.AddChild(ab);
            var at = new Label();
            at.Text = atk.ToString("0.0");
            at.Size = new Vector2I(180, 70);
            at.Position = new Vector2I(20, 520);
            at.HorizontalAlignment = HorizontalAlignment.Center;
            at.VerticalAlignment = VerticalAlignment.Center;
            at.AddThemeFontSizeOverride("font_size", 32);
            at.AddThemeColorOverride("font_color", Colors.White);
            panel.AddChild(at);

            var db = new ColorRect();
            db.Color = new Color(1, 1, 1);
            db.Size = new Vector2I(180, 70);
            db.Position = new Vector2I(wL - 200, 520);
            panel.AddChild(db);
            var dt = new Label();
            dt.Text = def.ToString("0.0");
            dt.Size = new Vector2I(180, 70);
            dt.Position = new Vector2I(wL - 200, 520);
            dt.HorizontalAlignment = HorizontalAlignment.Center;
            dt.VerticalAlignment = VerticalAlignment.Center;
            dt.AddThemeFontSizeOverride("font_size", 32);
            dt.AddThemeColorOverride("font_color", Colors.Black);
            panel.AddChild(dt);

            // Unit name below panel (hidden, shown on selection)
            var nameInPanel = new Label();
            nameInPanel.Text = bat.Name.Length > 20 ? bat.Name[..20] : bat.Name;
            nameInPanel.Size = new Vector2I(wL, 90);
            nameInPanel.Position = new Vector2I(0, 600);
            nameInPanel.HorizontalAlignment = HorizontalAlignment.Center;
            nameInPanel.VerticalAlignment = VerticalAlignment.Center;
            nameInPanel.AddThemeFontSizeOverride("font_size", 60);
            nameInPanel.AddThemeColorOverride("font_color", Colors.White);
            nameInPanel.Visible = false;
            vp.AddChild(nameInPanel);

            // Sprite3D to display
            var sprite = new Sprite3D();
            sprite.Texture = vp.GetTexture();
            sprite.PixelSize = 0.002f;
            sprite.Centered = false;
            sprite.Position = new Vector3(-0.6f, -0.6f, 0f);
            sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            root.AddChild(sprite);
            return root;
        }

        void UpdateHighlights()
        {
            if (_highlightRoot != null) { RemoveChild(_highlightRoot); _highlightRoot.QueueFree(); }
            _highlightRoot = new Node3D(); AddChild(_highlightRoot);

            var brightMat = new StandardMaterial3D(); brightMat.AlbedoColor = new Color(0, 0.7f, 1.0f, 0.12f); brightMat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded; brightMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            var darkMat = new StandardMaterial3D(); darkMat.AlbedoColor = new Color(0, 0.3f, 0.5f, 0.12f); darkMat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded; darkMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            foreach (var kv in _reachableTiles)
            {
                float rem = _selectedUnitAP - kv.Value;
                var m = MakeHighlight(kv.Key, rem >= 4f ? brightMat : darkMat, 0.02f);
                _highlightRoot.AddChild(m);
            }
            var artyMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.5f, 0f, 0.15f), ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
            foreach (var p in _artilleryRangeTiles)
            {
                _highlightRoot.AddChild(MakeHighlight(p, artyMat, 0.04f));
            }

            if (_selectedPos != null)
            {
                var selMat = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1, _selectionAlpha), ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded };
                var m = MakeHighlight(_selectedPos.Value, selMat, 0.06f);
                _highlightRoot.AddChild(m);
            }
        }

        MeshInstance3D MakeHighlight(Vector2I pos, Material mat, float height)
        {
            float cx = pos.X * CellSize + CellSize / 2, cz = pos.Y * CellSize + CellSize / 2;
            return new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.95f, height, CellSize * 0.95f), Material = mat }, Position = new Vector3(cx, height / 2 + 0.001f, cz) };
        }

        public void SetCameraRef(Camera3D cam) { _camRef = cam; }

       public override void _Process(double delta)
       {
           _flashTimer += (float)delta;
           if (_flashTimer > 0.35f) { _flashTimer = 0f; _flashOn = !_flashOn; _selectionAlpha = _flashOn ? 0.6f : 0.15f; if (_selectedPos != null) UpdateHighlights(); }

            if (_isAnimating)
            {
                float stepTime = 0.12f;
                _animTimer += (float)delta;
                if (_animTimer >= stepTime)
                {
                    _animTimer -= stepTime;
                    _animIndex++;
                    if (_animIndex >= _animPath.Count)
                    {
                        _isAnimating = false;
                        _animUnit = null;
                        OnMoveFinished?.Invoke();
                        return;
                    }
                    var pos = _animPath[_animIndex];
                    float cx = pos.X * CellSize + CellSize / 2;
                    float cz = pos.Y * CellSize + CellSize / 2;
                    _animUnit.Root.Position = new Vector3(cx, 0, cz);
                    _animUnit.GridPos = pos;
                }
            }
       }

       Vector2I? ScreenToGrid(Vector2 sp)
        {
            var cam = _camRef ?? GetViewport()?.GetCamera3D();
            if (cam == null) return null;
            var origin = cam.ProjectRayOrigin(sp);
            var dir = cam.ProjectRayNormal(sp);
            if (dir.Y >= 0) return null;
            float t = -origin.Y / dir.Y;
            if (t < 0) return null;
            var point = origin + t * dir;
            int gx = (int)(point.X / CellSize), gy = (int)(point.Z / CellSize);
            if (_map == null || !_map.IsInBounds(new Vector2I(gx, gy))) return null;
            return new Vector2I(gx, gy);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    var gp = ScreenToGrid(mb.Position);
                    if (gp == null) return;
                    var p = gp.Value;
                    foreach (var (bat, pos) in _blueUnits) { if (pos == p) { OnUnitClicked?.Invoke(1, bat, pos); return; } }
                    foreach (var (bat, pos) in _redUnits) { if (pos == p) { OnUnitClicked?.Invoke(2, bat, pos); return; } }
                    OnTileClicked?.Invoke(p);
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (mb.Pressed)
                    {
                        _rightClickStart = mb.Position;
                    }
                    else if (_rightClickStart.HasValue)
                    {
                        float dist = _rightClickStart.Value.DistanceTo(mb.Position);
                        _rightClickStart = null;
                        if (dist < 8f)
                        {
                            OnRightClick?.Invoke();
                        }
                    }
                }
            }

            if (@event is InputEventMouseMotion mm)
            {
                var gp = ScreenToGrid(mm.Position);
                if (gp != _hoveredPos)
                {
                    _hoveredPos = gp;
                    OnHoverChanged?.Invoke(gp);
                }
            }
        }
    

        // 路径显示
        public void ShowPath(System.Collections.Generic.List<Vector2I> path)
        {
            ClearPath();
            _pathRoot = new Node3D();
            AddChild(_pathRoot);
            var pm = new StandardMaterial3D();
            pm.AlbedoColor = new Color(1f, 1f, 0f, 0.25f);
            pm.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            pm.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            foreach (var p in path)
            {
                float cx = p.X * CellSize + CellSize / 2;
                float cz = p.Y * CellSize + CellSize / 2;
                var m = new MeshInstance3D();
                m.Mesh = new BoxMesh { Size = new Vector3(CellSize * 0.9f, 0.03f, CellSize * 0.9f), Material = pm };
                m.Position = new Vector3(cx, 0.015f, cz);
                _pathRoot.AddChild(m);
            }
        }

        public void ClearPath()
        {
            if (_pathRoot != null) { RemoveChild(_pathRoot); _pathRoot.QueueFree(); _pathRoot = null; }
        }

        internal void StartMoveAnimation(System.Collections.Generic.List<Vector2I> path, Battalion bat)
        {
            _animUnit = null;
            foreach (var uv in _unitVisuals)
                if (uv.GridPos == path[0]) { _animUnit = uv; break; }
            if (_animUnit == null) { OnMoveFinished?.Invoke(); return; }
            _isAnimating = true;
            _animPath = path;
            _animIndex = 0;
            _animTimer = 0f;
        }}
}
