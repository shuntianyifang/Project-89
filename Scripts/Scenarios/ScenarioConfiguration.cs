using System;
using System.Text.Json;
using Godot;

namespace ColdWarWargame.Scenarios
{
    public sealed class ScenarioConfiguration
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string BlueOob { get; set; }
        public string RedOob { get; set; }
        public string Occupation { get; set; }
        public static ScenarioConfiguration Load()
        {
            using var file = FileAccess.Open("res://Scripts/Data/Scenarios/Fulda_Gap/scenario.json", FileAccess.ModeFlags.Read);
            if (file == null) throw new InvalidOperationException("无法读取场景配置");
            var config = JsonSerializer.Deserialize<ScenarioConfiguration>(file.GetAsText());
            if (config == null || config.Id != "fulda_gap_1989" || string.IsNullOrWhiteSpace(config.Title) ||
                !FileAccess.FileExists(config.BlueOob) || !FileAccess.FileExists(config.RedOob) || !FileAccess.FileExists(config.Occupation))
                throw new InvalidOperationException("Fulda Gap 场景配置或数据引用无效");
            return config;
        }
    }
}
