using Godot;
using System.Text.Json.Serialization;

namespace ColdWarWargame.Battlefield
{
    /// <summary>
    /// 单个网格的数据：地形类型 + 基础设施类型 + 可通过性
    /// PRD §2.2: 自然地形(terrain)与基础设施(infra)双图层独立结算
    /// </summary>
    public struct TileData
    {
        /// <summary>地形类型: 0=平原(2), 1=森林(4), 2=半城镇(1), 3=城镇(1)</summary>
        [JsonPropertyName("terrain")]
        public int TerrainType { get; set; }

        /// <summary>基础设施类型: 0=无(inf), 1=支线公路(1), 2=高速公路(0.5)</summary>
        [JsonPropertyName("infra")]
        public int InfraType { get; set; }

        /// <summary>网格是否可通行</summary>
        [JsonPropertyName("passable")]
        public bool IsPassable { get; set; }

        public TileData(int terrainType, int infraType = 0, bool isPassable = true)
        {
            TerrainType = terrainType;
            InfraType = infraType;
            IsPassable = isPassable;
        }

        /// <summary>自然地形基础AP消耗（PRD §2.2 参数定义表）</summary>
        public static float GetTerrainCost(int terrainType) => terrainType switch
        {
            0 => 2f,  // 平原
            1 => 4f,  // 森林
            2 => 1f,  // 半城镇
            3 => 1f,  // 城镇
            _ => float.PositiveInfinity
        };

        /// <summary>基础设施AP消耗</summary>
        public static float GetInfraCost(int infraType) => infraType switch
        {
            0 => float.PositiveInfinity, // 无基础设施
            1 => 1f,   // 支线公路
            2 => 0.5f, // 高速公路
            _ => float.PositiveInfinity
        };

        /// <summary>最终移动成本 = min(terrain_cost, infra_cost)</summary>
        public float GetMovementCost()
        {
            if (!IsPassable) return float.PositiveInfinity;
            float terrainCost = GetTerrainCost(TerrainType);
            float infraCost = GetInfraCost(InfraType);
            return Mathf.Min(terrainCost, infraCost);
        }
    }
}
