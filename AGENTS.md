# Project 89 协作指南

## 项目基线

- 本项目使用 **Godot 4.7 .NET**、**C#** 与 **.NET 9**。
- `GameManager.cs` 是场景启动入口；当环境变量 `CW_RUN_TESTS=1` 时，它会调用 `Scripts/Tests/AllTestsRunner.cs` 并在测试结束后退出。
- 使用 Godot .NET 编辑器打开 `project.godot`，日常手动验证可直接运行主场景。

## 规则文档与变更原则

`docs/current/` 下的 Markdown 文件是当前生效的规则来源，修改机制、数值、单位特性或场景数据前必须阅读对应文档：

- `Cold_War_Wargame_PRD_TDD.md`：核心规则、技术架构与数据格式。
- `兵棋特征字典.md`：兵棋特征与属性语义。
- `Battalion_Types_Reference.md`：营级单位类型、编制与战术特性。
- `Combat_Modifiers_Reference.md`：战斗优势修正与强制检定。
- `Terrain_Combat_Effects.md`：地形攻防效果与单位-地形交互。

`archive/legacy_document/` 仅保存历史快照，不能作为当前实现依据。若当前文档相互矛盾，或文档与代码的预期行为不一致，记录冲突并请求确认；不要自行选择或臆造数值。任何有意的规则改动都必须在同一变更中同步更新对应的 `docs/current/` 文档和回归测试。

## 代码与数据边界

- `Scripts/Models/`：领域实体和状态，例如营、子单位与格子。
- `Scripts/Data/` 与 `Scripts/Factories/`：JSON 模型、模板/单位数据读取和对象初始化。
- `Scripts/Systems/`：按 `Battlefield`、`Combat`、`Supply`、`Turns`、`Victory`、`Gameplay` 划分的规则计算与流程控制。
- `Scripts/Rendering/`：画面、相机与 UI 表现；不要将规则结算逻辑放入渲染层。
- `Scripts/Scenarios/`：场景装配和占领状态编解码。
- `Scripts/Tests/`：按被测系统领域组织；新增规则测试需接入 `AllTestsRunner.RunAll()`。

单位、模板、OOB 与占领状态 JSON 分别位于 `Scripts/Data/Units/`、`Scripts/Data/Templates/` 和 `Scripts/Data/Scenarios/`。修改后检查单位 ID 唯一性、模板/单位/OOB 引用完整性，以及场景坐标和占领状态的一致性。

## 验证

每次代码或数据行为变更至少执行与影响范围相符的验证：

1. 在已配置 Godot .NET SDK 和 NuGet 访问权限的环境中运行：`dotnet build "Project 89.csproj"`。
2. 运行自动化测试：在启动 Godot 前设置 `CW_RUN_TESTS=1`，使主场景执行 `AllTestsRunner`。例如 PowerShell：`$env:CW_RUN_TESTS='1'; godot4 --headless --path .`。
3. 新增或修改规则时，在对应的 `Scripts/Tests/<领域>/` 中添加或更新回归测试，并确认总测试运行器已调用该测试集。

如果环境缺少 Godot .NET SDK、NuGet 访问权限或无界面渲染支持，应明确报告验证未执行的原因；不要将环境错误归因于游戏逻辑。

## 工作区卫生

- 修改前后检查 `git status`，保留并避开与本任务无关的未提交改动。
- 不提交 `.godot/` 缓存或其他生成物。
- 可反复使用的辅助脚本放入 `tools/`；一次性迁移或修补脚本归档到 `archive/legacy_scripts/`。
- 不要为了清理工作区执行破坏性 Git 操作（如 `reset --hard`、覆盖式 checkout），除非用户明确要求。
