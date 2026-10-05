using System.Linq;
using System;
using System.Collections.Generic;
using ColdWarWargame.Models;
using ColdWarWargame.Systems.Victory;

namespace ColdWarWargame.Systems.Gameplay
{
    public sealed class CampaignResult
    {
        public VictoryAssessment Assessment { get; }
        public string Reason { get; }
        public string Casualties { get; }
        public string MissionSummary { get; }
        public CampaignResult(VictoryTracker tracker, int turn, string reason, MissionTracker missions=null, int[,] control=null)
        {
            Assessment = tracker.Evaluate(turn);
            Reason = reason;
            Casualties = tracker.BuildCampaignCasualtySummary();
            if(missions!=null)
            {
                float blue=missions.BlueScore,red=missions.RedScore(control);
                Assessment.Ratio=blue==0&&red==0?1:red==0?10:blue==0?.1f:Math.Clamp(blue/red,.1f,10);
                Assessment.BlueLevel=VictoryTracker.LevelForRatio(Assessment.Ratio);
                Assessment.BlueVP=(int)Math.Round(blue); Assessment.RedVP=(int)Math.Round(red);
                MissionSummary=missions.Summary(control);
            }
        }

        public static bool HasLivingBattalions(IEnumerable<Battalion> units) =>
            units.Any(b => b.HasSurvivingSubUnits && !b.IsEliminatedByThreshold());

        public GameplayEventType EndEvent => Assessment.BlueLevel switch
        {
            VictoryLevel.Stalemate => GameplayEventType.MatchDrawn,
            >= VictoryLevel.MarginalVictory => GameplayEventType.MatchWon,
            _ => GameplayEventType.MatchLost
        };

        public string Summary => MissionSummary!=null ?
            $"战役结束 · 第 {Assessment.TurnNumber} 回合\n{Reason}\n北约：{Assessment.BlueLevel.DisplayName()} · 华约：{Assessment.RedLevel.DisplayName()}\n任务分比：{Assessment.Ratio:0.00}\n\n{MissionSummary}\n\n{Casualties}" :
            $"战役结束 · 第 {Assessment.TurnNumber} 回合\n{Reason}\n\n" +
            $"北约：{Assessment.BlueVP} VP · {Assessment.BlueLevel.DisplayName()}\n" +
            $"华约：{Assessment.RedVP} VP · {Assessment.RedLevel.DisplayName()}\n" +
            $"得分比：{Assessment.Ratio:0.00}\n" +
            $"控制格：北约 {Assessment.BlueControlled} / 华约 {Assessment.RedControlled}\n\n{Casualties}";
    }
}
