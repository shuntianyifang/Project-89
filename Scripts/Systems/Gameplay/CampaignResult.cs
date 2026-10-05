using System.Linq;
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
        public CampaignResult(VictoryTracker tracker, int turn, string reason)
        {
            Assessment = tracker.Evaluate(turn);
            Reason = reason;
            Casualties = tracker.BuildCampaignCasualtySummary();
        }

        public static bool HasLivingBattalions(IEnumerable<Battalion> units) =>
            units.Any(b => b.HasSurvivingSubUnits && !b.IsEliminatedByThreshold());

        public GameplayEventType EndEvent => Assessment.BlueLevel switch
        {
            VictoryLevel.Stalemate => GameplayEventType.MatchDrawn,
            >= VictoryLevel.MarginalVictory => GameplayEventType.MatchWon,
            _ => GameplayEventType.MatchLost
        };

        public string Summary => $"战役结束 · 第 {Assessment.TurnNumber} 回合\n{Reason}\n\n" +
            $"北约：{Assessment.BlueVP} VP · {Assessment.BlueLevel.DisplayName()}\n" +
            $"华约：{Assessment.RedVP} VP · {Assessment.RedLevel.DisplayName()}\n" +
            $"得分比：{Assessment.Ratio:0.00}\n" +
            $"控制格：北约 {Assessment.BlueControlled} / 华约 {Assessment.RedControlled}\n\n{Casualties}";
    }
}
