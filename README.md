# Project 89

冷战背景的战役级兵棋推演技术原型，当前场景为 **Fulda Gap 1989**。

> 项目仍处于开发中。核心系统、数据驱动流程和测试框架正在迭代；不应将当前版本视为规则平衡、完整交互体验或稳定发布版本。

## 技术栈

- **引擎：** Godot 4.7 .NET（Forward Plus）
- **语言与运行时：** C#、.NET 9
- **物理后端：** Jolt Physics
- **入口：** `project.godot`，主场景为 `game_manager.tscn`

## 开发环境与运行

需要安装与项目匹配的 Godot 4.7 .NET 编辑器，以及 .NET 9 SDK。打开 `project.godot` 后运行主场景（默认快捷键 `F5`）即可启动原型。

构建 C# 项目：

```powershell
dotnet build "Project 89.csproj"
```

自动化测试通过主场景启动。设置 `CW_RUN_TESTS=1` 后，`GameManager` 会调用 `AllTestsRunner` 并根据测试结果退出：

```powershell
$env:CW_RUN_TESTS = "1"
godot4 --headless --path .
```

若 `godot4` 未加入 `PATH`，请改为本机 Godot .NET 可执行文件的完整路径。构建与无界面测试还依赖本机已配置 Godot .NET SDK、NuGet 访问权限和兼容的渲染环境。

## 热座对局操作与验证

- 点击己方营，再点击可达格移动；选择主力营后点击两格内敌方营进入双方部署与战斗结算。
- 空格结束当前阵营回合；F6 切换补给覆盖，F7 切换控制区域。
- “结束战役并结算”按累计 VP 比值显示双方结果；一方无存活营时自动结束。战斗全灭先显示战术战果，关闭后显示战役结果。
- “重新开局”恢复初始部署、回合和 VP。“保存对局”与“读取对局”恢复完整编制与战役状态；移动和战斗期间不可存读档。
- 完整存档使用 Godot 用户目录内的 `Fulda_Gap_campaign.json`，场景数据引用配置位于 `Scripts/Data/Scenarios/Fulda_Gap/scenario.json`。
- 执行 `powershell -ExecutionPolicy Bypass -File tools/validate.ps1` 可依次构建并运行全部回归测试；必要时通过 `-GodotPath` 指定 Godot .NET 程序。

## 启动与数据流

主场景的启动链路为：

```text
GameManager → GameApplication → GameSessionHost
```

`GameSessionHost` 负责加载单位与编制模板 JSON、Fulda Gap 的双方 OOB 和占领状态，随后装配回合管理、会话控制、HUD、网格渲染和相机。场景地图数据及补给特殊节点目前由 `FuldaGapScenario` 构建。

## 目录结构

| 路径 | 职责 |
| --- | --- |
| `Scripts/Models/` | 领域实体与运行时状态，如营、子单位、格子。 |
| `Scripts/Data/` | 单位、编制模板、场景 JSON 的模型与读取逻辑。 |
| `Scripts/Factories/` | 根据模板和 OOB 数据构造营级实例及覆盖项。 |
| `Scripts/Systems/` | 按 Battlefield、Combat、Supply、Turns、Victory、Gameplay 划分的规则计算和流程控制。 |
| `Scripts/Rendering/` | 网格、兵牌、相机与 HUD 等显示层。 |
| `Scripts/Scenarios/` | 场景构建与占领状态编解码。 |
| `Scripts/Tests/` | 与系统领域对应的回归测试；统一由 `AllTestsRunner` 调度。 |
| `tools/` | 仍需反复使用的辅助脚本与工具说明。 |
| `archive/` | 历史文档和一次性迁移/修补资料，不作为当前实现依据。 |

## 当前规则文档

默认场景现为基于历史材料的富尔达—巴德赫斯费尔德假想战：2 公里/格、2 小时/完整回合；蓝军两支骑兵中队，红军两个团的先头 / 后续模型。地图与装备数量经过首版抽象，不宣称当日实有实力或精确地形复原。参见 [历史基线](docs/current/Fulda_Gap_Historical_Baseline.md)、[编制与校准](docs/current/Fulda_Gap_Calibration.md)。地图 / OOB 可用 `tools/build_fulda_historical.py` 重新生成（需要 Pillow），结构审计使用 `tools/audit_fulda_scenario.py`；更改后需重跑 `tools/validate.ps1`。

`docs/current/` 是当前生效的规则与技术设计依据。修改机制、数值或数据前，请阅读相关文档：

- [PRD 与技术设计](docs/current/Cold_War_Wargame_PRD_TDD.md)
- [兵棋特征字典](docs/current/兵棋特征字典.md)
- [营级单位类型参考](docs/current/Battalion_Types_Reference.md)
- [战斗修正参考](docs/current/Combat_Modifiers_Reference.md)
- [地形战斗效果](docs/current/Terrain_Combat_Effects.md)

历史对齐清单等资料位于 `archive/legacy_document/`，仅用于追溯，不能替代当前规则文档。

## 贡献与验证约定

- 修改规则或数值时，同步更新对应的 `docs/current/` 文档和回归测试。
- 修改单位、模板、OOB 或占领状态 JSON 后，检查 ID 唯一性、引用完整性和场景坐标/占领状态一致性。
- 新增规则测试放入对应的 `Scripts/Tests/<领域>/`，并接入 `AllTestsRunner`。
- 提交前检查 `git status`，避免纳入 `.godot/` 缓存和与本次修改无关的文件。

更详细的代理协作规则请见 [AGENTS.md](AGENTS.md)。
