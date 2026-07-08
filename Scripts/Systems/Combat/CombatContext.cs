using System.Collections.Generic;
using ColdWarWargame.Models;

namespace ColdWarWargame.Systems.Combat
{
    /// <summary>
    /// 战斗上下文：记录战斗发生时的环境条件与双方状态。
    /// 地形加成由调用方根据防御方所在网格的 TerrainType 计算后填入。
    /// </summary>
    public class CombatContext
    {
        /// <summary>防御方的地形加成值（Terrain_Combat_Effects.md）</summary>
        public float DefenderTerrainBonus { get; set; } = 0f;

        public int AttackerFaction { get; set; } = 1;
        public int DefenderFaction { get; set; } = 2;

        public int AttackerOOSTurns { get; set; } = 0;
        public int DefenderOOSTurns { get; set; } = 0;

        public int RoundId { get; set; } = 0;
        public object Extra { get; set; }
    }

    public class ModifierEntry
    {
        public string Source { get; set; }
        public float Value { get; set; }
        public string Reason { get; set; }
        public string Target { get; set; }
    }

    public class CasualtyRecord
    {
        public SubUnitInstance Unit { get; set; }
        public int HpLost { get; set; }
        public bool IsDestroyed { get; set; }
        public int RemainingHp { get; set; }
    }

    public class AdvantageResult
    {
        public float Value { get; set; }
        public List<ModifierEntry> Modifiers { get; set; } = new();
    }

    public class CombatResolutionResult
    {
        public AdvantageResult Advantage { get; set; }
        public float AttackerDamagePool { get; set; }
        public float DefenderDamagePool { get; set; }
        public int AttackerHpLost { get; set; }
        public int DefenderHpLost { get; set; }
        public List<CasualtyRecord> AttackerCasualties { get; set; } = new();
        public List<CasualtyRecord> DefenderCasualties { get; set; } = new();
    }
}
