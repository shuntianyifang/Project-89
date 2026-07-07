using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ColdWarWargame.Data
{
    // 根节点模板
    public record UnitTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("class_type")]
        public string ClassType { get; init; }

        [JsonPropertyName("visuals")]
        public VisualsData Visuals { get; init; }

        [JsonPropertyName("combat_stats")]
        public CombatStatsData CombatStats { get; init; }

        // 部分防空单位特有属性，如 Gepard1A1 和 爱国者导弹，使用可空类型
        [JsonPropertyName("aa_area")]
        public int? AaArea { get; init; }

        [JsonPropertyName("system_vars")]
        public SystemVarsData SystemVars { get; init; }

        [JsonPropertyName("tactical_tags")]
        public TacticalTagsData TacticalTags { get; init; }
    }

    public record VisualsData
    {
        [JsonPropertyName("icon")]
        public string Icon { get; init; }

        [JsonPropertyName("model_scale")]
        public float ModelScale { get; init; }
    }

    public record CombatStatsData
    {
        [JsonPropertyName("max_hp")]
        public int MaxHp { get; init; }

        [JsonPropertyName("attack")]
        public float Attack { get; init; }

        [JsonPropertyName("defense")]
        public float Defense { get; init; }
    }

    public record SystemVarsData
    {
        [JsonPropertyName("cost")]
        public int Cost { get; init; }

        [JsonPropertyName("base_weight")]
        public int BaseWeight { get; init; }
    }

    public record TacticalTagsData
    {
        [JsonPropertyName("domain")]
        public string Domain { get; init; }

        // 使用 HashSet 确保后续在 CRT 结算时，HasCapability("HeavyArmor") 查询时间复杂度为 O(1)
        [JsonPropertyName("capabilities")]
        public HashSet<string> Capabilities { get; init; } 
    }
}