using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace ColdWarWargame.Data
{
    public static class UnitDatabase
    {
        // 核心只读字典，统管全军
        private static Dictionary<string, UnitTemplate> _unitTemplates;

        // 现在的参数变成了文件夹路径，比如 "res://Data/Units"
        public static void Initialize(string directoryPath)
        {
            // 1. 初始化空字典
            _unitTemplates = new Dictionary<string, UnitTemplate>();

            // 2. 打开目标文件夹
            using var dir = DirAccess.Open(directoryPath);
            if (dir == null)
            {
                GD.PrintErr($"无法打开单位数据文件夹: {directoryPath}, 错误码: {DirAccess.GetOpenError()}");
                return;
            }

            // 3. 遍历文件夹内的所有文件
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            
            while (fileName != "")
            {
                // 只处理 .json 后缀的文件，忽略子文件夹或 .import 等缓存文件
                if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                {
                    string filePath = directoryPath + "/" + fileName;
                    LoadSingleFile(filePath);
                }
                fileName = dir.GetNext();
            }
            
            GD.Print($"数据初始化完成：共扫描并合并了 {_unitTemplates.Count} 种单位的基础数据！");
        }

        // 抽取出的子方法：专门读取单个文件并合并到总字典中
        private static void LoadSingleFile(string filePath)
        {
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"读取文件失败: {filePath}");
                return;
            }

            string jsonString = file.GetAsText();
            var dict = JsonSerializer.Deserialize<Dictionary<string, UnitTemplate>>(jsonString);

            // 将这个文件里的单位，逐个合并到全局大字典里
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    // 检测是否有重复的单位 ID
                    if (_unitTemplates.ContainsKey(kvp.Key))
                    {
                        GD.PrintErr($"发生数据冲突！单位 ID [{kvp.Key}] 在多个文件中重复定义，已跳过。");
                        continue;
                    }
                    
                    _unitTemplates.Add(kvp.Key, kvp.Value);
                }
                GD.Print($"已加载: {filePath} ({dict.Count} 个单位)");
            }
        }

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