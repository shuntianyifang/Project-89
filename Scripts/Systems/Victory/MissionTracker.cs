using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using ColdWarWargame.Models;
using ColdWarWargame.Scenarios;
using GridMap = ColdWarWargame.Systems.Battlefield.GridMap;

namespace ColdWarWargame.Systems.Victory
{
    public sealed class MissionObjective
    {
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Points { get; set; }
    }
    public sealed class MissionConfiguration
    {
        public string Id { get; set; }
        public int WindowRounds { get; set; }
        public float MinimumCE { get; set; }
        public float BreakthroughTarget { get; set; }
        public int BreakthroughPoints { get; set; }
        public int DelayPoints { get; set; }
        public int PreservationPoints { get; set; }
        public string[] PreservationUnits { get; set; }
        public int[][] RedExits { get; set; }
        public int[][] BlueExits { get; set; }
        public MissionObjective[] Objectives { get; set; }
        public static MissionConfiguration Load()
        {
            using var file=FileAccess.Open("res://Scripts/Data/Scenarios/Fulda_Gap/missions.json",FileAccess.ModeFlags.Read);
            var c=file==null?null:JsonSerializer.Deserialize<MissionConfiguration>(file.GetAsText());
            if(c==null || c.Id!="fulda_missions_v1" || c.WindowRounds<=0 || !float.IsFinite(c.MinimumCE) ||
                c.MinimumCE<=0 || c.MinimumCE>1 || !float.IsFinite(c.BreakthroughTarget) || c.BreakthroughTarget<=0 ||
                c.PreservationUnits==null || c.PreservationUnits.Length==0 || c.RedExits==null || c.BlueExits==null || c.Objectives==null ||
                c.BreakthroughPoints<0 || c.DelayPoints<0 || c.PreservationPoints<0 ||
                c.RedExits.Concat(c.BlueExits).Any(p=>p==null || p.Length!=2 || p[0]<0 || p[0]>=50 || p[1]<0 || p[1]>=30) ||
                c.Objectives.Any(o=>o==null || o.X<0 || o.X>=50 || o.Y<0 || o.Y>=30 || o.Points<0))
                throw new InvalidOperationException("任务配置无效");
            return c;
        }
        public bool IsExit(int faction,Vector2I p)=>(faction==1?BlueExits:RedExits).Any(v=>v[0]==p.X&&v[1]==p.Y);
    }
    public sealed class MissionExit
    {
        public SavedBattalion Unit { get; set; }
        public float CE { get; set; }
        public int Round { get; set; }
    }
    public sealed class MissionState
    {
        public string ConfigurationId { get; set; }="fulda_missions_v1";
        public int CompletedRounds { get; set; }
        public int DelayRounds { get; set; }
        public int? FirstBreakthroughRound { get; set; }
        public List<MissionExit> Exits { get; set; }=new();
        public List<MissionRoundRecord> RoundHistory { get; set; }=new();
    }
    public sealed class MissionRoundRecord
    {
        public int Round { get; set; }
        public bool RedCorridorOpen { get; set; }
        public bool DelayAwarded { get; set; }
        public int[] ObjectiveOwners { get; set; }
    }
    public sealed class MissionTracker
    {
        public MissionConfiguration Configuration { get; }
        public MissionState State { get; private set; }=new();
        public MissionTracker(MissionConfiguration config) { Configuration=config; }
        public float BreakthroughEquivalent=>State.Exits.Where(e=>e.Unit.Faction==2).Sum(e=>e.CE);
        public float DelayScore=>Configuration.DelayPoints*(float)State.DelayRounds/Configuration.WindowRounds;
        public float PreservationScore=>Configuration.PreservationPoints*State.Exits.Where(e=>e.Unit.Faction==1&&Configuration.PreservationUnits.Contains(e.Unit.Id)).Sum(e=>e.CE)/Configuration.PreservationUnits.Length;
        public float BreakthroughScore=>Configuration.BreakthroughPoints*Math.Min(1,BreakthroughEquivalent/Configuration.BreakthroughTarget);
        public float ObjectiveScore(int[,] control)=>Configuration.Objectives.Where(o=>control[o.X,o.Y]==2).Sum(o=>o.Points);
        public float BlueScore=>DelayScore+PreservationScore;
        public float RedScore(int[,] control)=>BreakthroughScore+ObjectiveScore(control);
        public void CompleteRound(int round,GridMap map=null,int[,] control=null)
        {
            if(round!=State.CompletedRounds+1) return;
            State.CompletedRounds=round;
            bool awarded=round<=Configuration.WindowRounds && !State.FirstBreakthroughRound.HasValue;
            if(awarded) State.DelayRounds++;
            if(map!=null && control!=null)
                State.RoundHistory.Add(new MissionRoundRecord {
                    Round=round, DelayAwarded=awarded,
                    RedCorridorOpen=Configuration.RedExits.Any(p=>HasControlledRoad(map,new(p[0],p[1]),control,2)),
                    ObjectiveOwners=Configuration.Objectives.Select(o=>control[o.X,o.Y]).ToArray()
                });
        }
        public string TryExit(Battalion unit,Vector2I pos,int round,GridMap map,int[,] control,float supply)
        {
            if(State.Exits.Any(e=>e.Unit.Id==unit.InstanceId)) return "该单位已经退出，不能重复计分";
            if(!Configuration.IsExit(unit.Faction,pos)) return "请先到达本方任务出口";
            if(!unit.HasSurvivingSubUnits || unit.IsEliminatedByThreshold()) return "单位已失去作战能力";
            if(unit.CalculateCE()+0.00001f<Configuration.MinimumCE || unit.Fatigue>8) return "CE不足50%或已溃散，无法有组织退出";
            if(!float.IsFinite(supply) || supply<=0 || control[pos.X,pos.Y]!=unit.Faction) return "出口须由本方控制且单位有补给";
            if(unit.Faction==2)
            {
                if(!unit.CanFillMain()) return "只有主战营可以计入有效突破";
                if(!HasControlledRoad(map,pos,control,2)) return "没有通往东侧入口的本方控制道路通道";
                State.FirstBreakthroughRound ??= round;
            }
            State.Exits.Add(new MissionExit { Unit=SavedBattalion.Capture(unit,pos),CE=unit.CalculateCE(),Round=round });
            return null;
        }
        public static bool HasControlledRoad(GridMap map,Vector2I start,int[,] control,int faction)
        {
            if(!map.IsInBounds(start) || !map.IsPassable(start) || control[start.X,start.Y]!=faction ||
                map.GetTile(start).InfraType==0 || !map.PrimarySupplySources.TryGetValue(faction,out var sources)) return false;
            var seen=new HashSet<Vector2I>{start}; var queue=new Queue<Vector2I>(); queue.Enqueue(start);
            while(queue.Count>0)
            {
                var p=queue.Dequeue(); if(sources.Contains(p)) return true;
                foreach(var n in map.GetAllNeighbors(p))
                    if(control[n.X,n.Y]==faction && map.IsPassable(n) && map.GetTile(n).InfraType>0 && map.CanCross(p,n) && seen.Add(n)) queue.Enqueue(n);
            }
            return false;
        }
        public MissionState Capture()=>JsonSerializer.Deserialize<MissionState>(JsonSerializer.Serialize(State));
        public void Validate(MissionState state,IEnumerable<Battalion> active,int turn)
        {
            if(state==null || state.ConfigurationId!=Configuration.Id || state.CompletedRounds!=turn-1 || state.DelayRounds<0 ||
                state.DelayRounds>Math.Min(state.CompletedRounds,Configuration.WindowRounds) || state.Exits==null ||
                state.FirstBreakthroughRound is <1 || state.FirstBreakthroughRound>turn)
                throw new InvalidOperationException("存档任务回合状态无效");
            var ids=active.Select(b=>b.InstanceId).ToHashSet();
            foreach(var e in state.Exits)
            {
                if(e?.Unit==null || !ids.Add(e.Unit.Id) || e.Round<1 || e.Round>turn || !float.IsFinite(e.CE) || e.CE<Configuration.MinimumCE || e.CE>1 ||
                    !Configuration.IsExit(e.Unit.Faction,new(e.Unit.X,e.Unit.Y))) throw new InvalidOperationException("存档退出记录无效");
                var unit=e.Unit.Restore();
                if(unit.IsEliminatedByThreshold() || Math.Abs(unit.CalculateCE()-e.CE)>0.0001f || (unit.Faction==2&&!unit.CanFillMain()))
                    throw new InvalidOperationException("存档退出单位效能无效");
            }
            var red=state.Exits.Where(e=>e.Unit.Faction==2).ToList();
            if((red.Count==0)!=(!state.FirstBreakthroughRound.HasValue) ||
                (red.Count>0&&red.Min(e=>e.Round)!=state.FirstBreakthroughRound)) throw new InvalidOperationException("存档首次突破记录无效");
            int expectedDelay=Math.Min(Configuration.WindowRounds,Math.Min(state.CompletedRounds,(state.FirstBreakthroughRound??int.MaxValue)-1));
            if(state.DelayRounds!=expectedDelay) throw new InvalidOperationException("存档迟滞积分记录不一致");
            if(state.RoundHistory==null) throw new InvalidOperationException("存档任务过程记录无效");
            int previous=0;
            foreach(var record in state.RoundHistory)
            {
                if(record==null || record.Round<=previous || record.Round>state.CompletedRounds ||
                    record.ObjectiveOwners==null || record.ObjectiveOwners.Length!=Configuration.Objectives.Length ||
                    record.ObjectiveOwners.Any(owner=>owner is <0 or >2) ||
                    record.DelayAwarded!=(record.Round<=Configuration.WindowRounds && record.Round<(state.FirstBreakthroughRound??int.MaxValue)))
                    throw new InvalidOperationException("存档任务过程记录不一致");
                previous=record.Round;
            }
        }
        public void Restore(MissionState state)=>State=JsonSerializer.Deserialize<MissionState>(JsonSerializer.Serialize(state));
        public string Journal()
        {
            var entries=State.Exits.Select(e=>(Round:e.Round,Order:0,Text:
                $"第 {e.Round} 回合：{(e.Unit.Faction==1?"有组织撤离":"有效突破")} · {e.Unit.Name} · CE {e.CE:P0}")).ToList();
            entries.AddRange(State.RoundHistory.Select(r=>(Round:r.Round,Order:1,Text:
                $"第 {r.Round} 回合结束：西向道路通道{(r.RedCorridorOpen?"连通":"阻断")}；"+
                $"迟滞{(r.DelayAwarded?"计分":"不再计分")}；"+
                string.Join("、",Configuration.Objectives.Select((o,i)=>$"{o.Name}={OwnerName(r.ObjectiveOwners[i])}"))+
                (r.Round==Configuration.WindowRounds?"；24小时考核窗口结束":""))));
            string note=State.RoundHistory.Count<State.CompletedRounds ? "旧存档未记录的回合态势不予推断。\n" : "";
            return note+(entries.Count==0?"尚无任务事件":string.Join("\n",entries.OrderBy(e=>e.Round).ThenBy(e=>e.Order).Select(e=>e.Text)));
        }
        private static string OwnerName(int faction)=>faction==1?"北约":faction==2?"华约":"未控制";
        public string Summary(int[,] control)=>
            $"任务分：北约 {BlueScore:0.0} / 华约 {RedScore(control):0.0}\n"+
            $"北约：迟滞 {DelayScore:0.0}/{Configuration.DelayPoints} · 保存 {PreservationScore:0.0}/{Configuration.PreservationPoints}\n"+
            $"华约：突破 {BreakthroughScore:0.0}/{Configuration.BreakthroughPoints} · 节点 {ObjectiveScore(control):0.0}/{Configuration.Objectives.Sum(o=>o.Points)}\n"+
            $"迟滞：{State.DelayRounds}/{Configuration.WindowRounds} 回合（首次有效突破后停止）\n"+
            $"有效突破：{BreakthroughEquivalent:0.00}/{Configuration.BreakthroughTarget:0.00} 营当量 · 首次突破：{(State.FirstBreakthroughRound.HasValue?"第"+State.FirstBreakthroughRound+"回合":"尚未发生")}\n"+
            string.Join("\n",Configuration.Objectives.Select(o=>$"{o.Name} ({o.X},{o.Y})：{(control[o.X,o.Y]==2?"华约控制":"未由华约控制")}，{o.Points} 分"))+"\n"+
            "华约出口："+string.Join(" / ",Configuration.RedExits.Select(p=>$"({p[0]},{p[1]})"))+"\n"+
            "北约撤离区："+string.Join(" / ",Configuration.BlueExits.Select(p=>$"({p[0]},{p[1]})"))+"\n"+
            $"已退出：{State.Exits.Count} 个单位 · CE≥{Configuration.MinimumCE:P0}、有补给、未溃散；华约还须主战营及连通道路\n"+
            string.Join("\n",State.Exits.Select(e=>$"{(e.Unit.Faction==1?"撤离":"突破")}：{e.Unit.Name}，CE {e.CE:P0}，第 {e.Round} 回合"))+"\n"+
            "提前手动结束只结算已获得的任务分；战损不计入胜负。\n\n任务过程\n"+Journal();
    }
}
