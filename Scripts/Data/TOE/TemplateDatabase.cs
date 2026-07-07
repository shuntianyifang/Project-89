using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace ColdWarWargame.Data.TOE
{
    public static class TemplateDatabase
    {
        // 核心字典：存储全军所有的营级编制表 (TO&E)
        private static Dictionary<string, BattalionTemplate> _templates;

        /// <summary>
        /// 扫描指定文件夹（如 "res://Data/Templates"），读取并合并所有编制 JSON 文件
        /// </summary>
        public static void Initialize(string directoryPath)
        {
            _templates = new Dictionary<string, BattalionTemplate>();

            using var dir = DirAccess.Open(directoryPath);
            if (dir == null)
            {
                GD.PrintErr($"无法打开编制模板文件夹: {directoryPath}, 错误码: {DirAccess.GetOpenError()}");
                return;
            }

            dir.ListDirBegin();
            string fileName = dir.GetNext();

            while (fileName != "")
            {
                // 确保是 JSON 文件，排除 .import 缓存
                if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                {
                    string filePath = directoryPath + "/" + fileName;
                    LoadSingleTemplateFile(filePath);
                }
                fileName = dir.GetNext();
            }

            GD.Print($"编制模板初始化完成：共加载了 {_templates.Count} 个营级编制模板。");
        }

        private static void LoadSingleTemplateFile(string filePath)
        {
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"读取编制文件失败: {filePath}");
                return;
            }

            string jsonString = file.GetAsText();
            var dict = JsonSerializer.Deserialize<Dictionary<string, BattalionTemplate>>(jsonString);

            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    if (_templates.ContainsKey(kvp.Key))
                    {
                        GD.PrintErr($"数据冲突！编制模板 ID [{kvp.Key}] 重复定义，已跳过。");
                        continue;
                    }
                    _templates.Add(kvp.Key, kvp.Value);
                }
                GD.Print($"已加载编制文件: {filePath} ({dict.Count} 个模板)");
            }
        }

        /// <summary>
        /// 提供给工厂调用的检索接口
        /// </summary>
        public static BattalionTemplate GetTemplate(string templateId)
        {
            if (_templates.TryGetValue(templateId, out var template))
            {
                return template;
            }
            throw new KeyNotFoundException($"未找到编制模板 ID: {templateId}");
        }
    }
}