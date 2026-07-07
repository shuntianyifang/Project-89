using System.Collections.Generic;

namespace ColdWarWargame.Core.Combat
{
    /// <summary>
    /// 战斗上下文：记录战斗发生时的环境条件与双方状态。
    /// 地形加成由调用方根据防御方所在网格的 TerrainType 计算后填入。
    /// </summary>
    public class CombatContext
    {
        /// <summary>
        /// 防御方的地形加成值。
        /// 根据防御方营所在网格的地形类型决定：
        ///   平原 0.0，森林 +0.1，半城镇 +0.3，城镇 +0.4
        /// 调用方从 GridMap 读取防御方坐标的 TileData.TerrainType 后计算填入。
        /// </summary>
        public float DefenderTerrainBonus { get; set; } = 0f;

        public bool IsAttacker { get; set; }

        public bool AttackerInZOC { get; set; }
        public bool DefenderInZOC { get; set; }

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
