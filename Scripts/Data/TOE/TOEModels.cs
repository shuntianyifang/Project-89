using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ColdWarWargame.Data.TOE
{
    public record BattalionTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }
        [JsonPropertyName("role")]
        public string Role { get; init; }
        [JsonPropertyName("battalion_tags")]
        public List<string> BattalionTags { get; init; }
        
        [JsonPropertyName("companies")]
        public Dictionary<string, CompanyTemplate> Companies { get; init; }
    }

    public record CompanyTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("platoons")]
        public Dictionary<string, PlatoonTemplate> Platoons { get; init; }
    }

    public record PlatoonTemplate
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } // "standard" 或 "composite"

        // 对于 standard 类型
        [JsonPropertyName("units")]
        public Dictionary<string, SubUnitDef> Units { get; init; }

        // 对于 composite 类型
        [JsonPropertyName("infantry")]
        public Dictionary<string, SubUnitDef> Infantry { get; init; }

        [JsonPropertyName("vehicle")]
        public Dictionary<string, SubUnitDef> Vehicle { get; init; }
    }

    public record SubUnitDef
    {
        [JsonPropertyName("unit_id")]
        public string UnitId { get; init; }
        
        [JsonPropertyName("max_hp")]
        public int MaxHp { get; init; }
    }
}
