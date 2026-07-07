using System;
using System.Collections.Generic;

namespace ColdWarWargame.Core.Combat
{
    public class CombatContext
    {
        public bool IsAttacker { get; set; }
        public Dictionary<string, float> TerrainModifiers { get; set; } = new();
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
        public string Target { get; set; } // "attacker" or "defender"
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
