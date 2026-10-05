using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using ColdWarWargame.Rendering;
using ColdWarWargame.Systems.Supply;
using ColdWarWargame.Systems.Turns;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class GameDebugInspector
    {
        private readonly FuldaGapScenario _scenario;
        private readonly TurnManager _turn;
        private readonly Grid3DRenderer _renderer;
        private readonly PanelContainer _panel;
        private readonly Label _text;
        private readonly Queue<object> _settlements = new();
        private readonly SupplyTrace[] _trace = { null, new(), new() };
        private readonly float[][,] _sp = new float[3][,];
        private readonly float[][,] _displayed = new float[3][,];
        private Vector2I? _hover;
        private string _exportStatus = "";
        public bool Enabled { get; private set; }
        public string LastExportPath { get; private set; }
        private int _faction = 1;

        public GameDebugInspector(FuldaGapScenario scenario, TurnManager turn,
            Grid3DRenderer renderer, CanvasLayer canvas)
        {
            _scenario = scenario; _turn = turn; _renderer = renderer;
            _panel = new PanelContainer { Position = new Vector2(10, 145), Size = new Vector2(440, 320), Visible = false };
            var style = new StyleBoxFlat { BgColor = new Color(0.02f, 0.03f, 0.05f, 0.94f),
                ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
            _panel.AddThemeStyleboxOverride("panel", style);
            var column = new VBoxContainer();
            _panel.AddChild(column);
            var buttons = new HBoxContainer(); column.AddChild(buttons);
            var export = new Button { Text = "导出快照 [F9]", FocusMode = Control.FocusModeEnum.None };
            export.Pressed += Export; buttons.AddChild(export);
            var side = new Button { Text = "切换阵营 [F10]", FocusMode = Control.FocusModeEnum.None };
            side.Pressed += SwitchFaction; buttons.AddChild(side);
            _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(415, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            _text.AddThemeFontSizeOverride("font_size", 14); column.AddChild(_text);
            canvas.AddChild(_panel);
            var toggle = new Button { Text = "补给调试 [F3]", Position = new Vector2(180, 92),
                FocusMode = Control.FocusModeEnum.None };
            toggle.Pressed += () => HandleKey(Key.F3);
            canvas.AddChild(toggle);
        }

        public bool HandleKey(Key key)
        {
            if (key == Key.F3)
            {
                Enabled = !Enabled; _panel.Visible = Enabled;
                if (Enabled) Refresh(); else _renderer.ClearPath();
                return true;
            }
            if (!Enabled) return false;
            if (key == Key.F9) { Export(); return true; }
            if (key == Key.F10) { SwitchFaction(); return true; }
            return false;
        }

        private List<(Battalion bat, Vector2I pos)> Units() =>
            _scenario.BlueBattalions.Concat(_scenario.RedBattalions).ToList();

        public void SetDisplayed(float[,] blue, float[,] red)
        {
            _displayed[1] = blue; _displayed[2] = red;
            if (Enabled) Refresh();
        }

        public void Hover(Vector2I? position)
        {
            _hover = position;
            if (Enabled) Present();
        }

        private void SwitchFaction() { _faction = 3 - _faction; Present(); }

        private void Refresh()
        {
            var units = Units(); var (hubs, airports) = _scenario.GetSupplySpecialNodes();
            for (int faction = 1; faction <= 2; faction++)
            {
                var occupied = units.Where(u => u.bat.Faction != faction).Select(u => u.pos).ToHashSet();
                _sp[faction] = new SupplyManager().ComputeFactionSupplySP(faction, _scenario.Map, units,
                    occupied, _scenario.ZOC.GetFactionZOC(occupied), hubs, airports,
                    _scenario.GetOccupationMap(), _trace[faction]);
            }
            Present();
        }

        private void Present()
        {
            if (!Enabled || _sp[1] == null) return;
            var text = new StringBuilder($"调试 [F3]  回合 {_turn.TurnNumber} / 当前行动方 {_turn.CurrentFaction}\n检查阵营：{_faction}（1蓝 / 2红）\n");
            text.AppendLine($"覆盖：蓝 {_sp[1].Cast<float>().Count(v => v > 0)} / 红 {_sp[2].Cast<float>().Count(v => v > 0)}");
            if (_hover is Vector2I p && _scenario.Map.IsInBounds(p))
            {
                var tile = _scenario.Map.GetTile(p); var owner = _scenario.GetOccupationMap()[p.X, p.Y];
                text.AppendLine($"格子 {p} 归属 {owner} 地形 {tile.TerrainType} 道路 {tile.InfraType}");
                text.AppendLine($"SP 蓝 {_sp[1][p.X,p.Y]:F2} / 红 {_sp[2][p.X,p.Y]:F2}");
                text.AppendLine($"显示SP {_displayed[_faction]?[p.X,p.Y]:F2} / 诊断SP {_sp[_faction][p.X,p.Y]:F2}");
                var blockers = Units().Where(u => u.bat.Faction != _faction &&
                    (u.pos == p || (u.bat.CurrentAP >= 4 && Math.Abs(u.pos.X-p.X) <= 1 && Math.Abs(u.pos.Y-p.Y) <= 1)));
                text.AppendLine(_trace[_faction].Blocked.Contains(p) ? "阻断：敌军实体/剩余AP≥4范围" : tile.IsPassable ? "可通行；SP=0表示断连或预算耗尽" : "阻断：地形不可通行");
                foreach (var u in blockers) text.AppendLine($"阻断者 {u.bat.InstanceId} {u.pos} AP={u.bat.CurrentAP:F2}");
                var route = _trace[_faction].Routes[p.X,p.Y];
                if (route != null)
                {
                    var path = route.Path();
                    text.AppendLine($"来源 {route.SourceKind} {path[0]} 预算 {route.Budget} 累计消耗 {route.Cost:F2}");
                    text.AppendLine($"来源归属 {_scenario.GetOccupationMap()[path[0].X,path[0].Y]} 路径 {path.Count} 格（地图亮线）");
                    _renderer.ShowPath(path);
                }
                else _renderer.ClearPath();
                foreach (var u in Units().Where(u => u.pos == p))
                    text.AppendLine($"{u.bat.InstanceId} 方{u.bat.Faction} AP={u.bat.CurrentAP:F2} 疲劳={u.bat.Fatigue} OOS={u.bat.TurnsOOS} 上次断补={u.bat.WasOOSLastTurn}");
                if (_scenario.SupplyHubs.Contains(p)) text.AppendLine("设施：枢纽（己方且连通时重新激活）");
                if (_scenario.SupplyAirports.Contains(p)) text.AppendLine("设施：机场");
            }
            else { text.AppendLine("悬停地图格子查看阻断、SP和实际传播路径。"); _renderer.ClearPath(); }
            text.AppendLine("诊断展示双方完整数据，不受战争迷雾限制。");
            text.Append(_exportStatus);
            _text.Text = text.ToString();
        }

        public void RecordSettlement(int faction, string stage)
        {
            if (!Enabled) return;
            Refresh();
            _settlements.Enqueue(Capture($"end_turn_{faction}_{stage}"));
            while (_settlements.Count > 8) _settlements.Dequeue();
        }

        private static object Point(Vector2I p) => new { x = p.X, y = p.Y };

        public object Capture(string reason)
        {
            var cells = new List<object>();
            var control = _scenario.GetOccupationMap();
            for (int y = 0; y < _scenario.Map.Height; y++)
                for (int x = 0; x < _scenario.Map.Width; x++)
                {
                    var p = new Vector2I(x,y); var tile = _scenario.Map.GetTile(p);
                    cells.Add(new { x, y, terrain = tile.TerrainType, infra = tile.InfraType,
                        passable = tile.IsPassable, owner = control[x,y], hub = _scenario.SupplyHubs.Contains(p), airport = _scenario.SupplyAirports.Contains(p),
                        blue = CellDiagnostic(1, p), red = CellDiagnostic(2, p) });
                }
            return new { reason, turn = _turn.TurnNumber, active_faction = _turn.CurrentFaction,
                phase = _turn.PhaseName(), width = _scenario.Map.Width, height = _scenario.Map.Height,
                hovered = _hover.HasValue ? Point(_hover.Value) : null, inspecting_faction = _faction,
                primary_sources = _scenario.Map.PrimarySupplySources.Select(kv => new { faction = kv.Key, points = kv.Value.Select(Point).ToArray() }).ToArray(),
                blocked_crossings = _scenario.Map.BlockedCrossings.Select(e => new { a = Point(e.a), b = Point(e.b) }).ToArray(),
                cells, units = Units().Select(u => new { id = u.bat.InstanceId, name = u.bat.Name,
                    template = u.bat.TemplateId, faction = u.bat.Faction, x = u.pos.X, y = u.pos.Y,
                    ap = u.bat.CurrentAP, fatigue = u.bat.Fatigue, turns_oos = u.bat.TurnsOOS,
                    was_oos = u.bat.WasOOSLastTurn, tags = u.bat.BattalionTags.ToArray() }).ToArray() };
        }

        private object CellDiagnostic(int faction, Vector2I p)
        {
            var route = _trace[faction].Routes[p.X,p.Y];
            return new { sp = _sp[faction][p.X,p.Y], displayed_sp = _displayed[faction]?[p.X,p.Y],
                blocked = _trace[faction].Blocked.Contains(p), source_kind = route?.SourceKind,
                budget = route?.Budget, cost = route?.Cost,
                path = route?.Path().Select(Point).ToArray() };
        }

        private void Export()
        {
            try
            {
                Refresh();
                string dir = ProjectSettings.GlobalizePath("user://debug_snapshots");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"supply-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
                var data = new { schema_version = 1, captured_utc = DateTime.UtcNow,
                    engine = Engine.GetVersionInfo()["string"].AsString(),
                    assembly_build = typeof(GameDebugInspector).Assembly.ManifestModule.ModuleVersionId.ToString(),
                    rules = new { primary_sp = 36, airport_sp = 12, block_ap = 4,
                        blue_edge = "configured_west", red_edge = "configured_east", ownership_required_for_edge = true },
                    current = Capture("manual_export"), settlements = _settlements.ToArray() };
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                LastExportPath = path;
                bool clipboard = DisplayServer.GetName() != "headless";
                if (clipboard) DisplayServer.ClipboardSet(path);
                _exportStatus = (clipboard ? "已导出，路径已复制：\n" : "已导出：\n") + path;
                GD.Print("[DEBUG SNAPSHOT] " + path); Present();
            }
            catch (Exception ex) { _exportStatus = "导出失败：" + ex.Message; GD.PrintErr(ex); Present(); }
        }
    }
}
