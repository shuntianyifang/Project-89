using System.Collections.Generic;
using System.Text.Json;
using Godot; // 如果使用 Godot 的 FileAccess

namespace ColdWarWargame.Data
{
    public static class UnitDatabase
    {
        // 核心只读字典
        private static Dictionary<string, UnitTemplate> _unitTemplates;

        public static void Initialize(string resFilePath)
        {
            // 使用 Godot 原生 API 读取文件内容
            using var file = FileAccess.Open(resFilePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"无法加载单位数据文件: {resFilePath}, 错误码: {FileAccess.GetOpenError()}");
                return;
            }

            string jsonString = file.GetAsText();
            
            // 反序列化
            _unitTemplates = JsonSerializer.Deserialize<Dictionary<string, UnitTemplate>>(jsonString);
            
            GD.Print($"成功加载了 {_unitTemplates.Count} 种单位的基础数据！");
        }

        // 供营级生成器调用的查询接口
        public static UnitTemplate GetTemplate(string unitId)
        {
            if (_unitTemplates.TryGetValue(unitId, out var template))
            {
                return template;
            }
            throw new KeyNotFoundException($"未找到单位 ID: {unitId}");
        }
    }
}