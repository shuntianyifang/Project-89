# PRD-代码对齐清单（2026-07-09）

## 范围
本轮仅核对以下4类阈值与边界：
- AP 阈值
- 疲劳阈值
- 战斗门槛
- 胜利区间边界

## 结论概览
- 已对齐：8 项
- 存在偏差：5 项
- 高优先级修正：3 项

## 对齐明细

| 编号 | 核对项 | PRD要求 | 代码证据 | 结论 | 建议动作 | 优先级 |
|---|---|---|---|---|---|---|
| A1 | AP 容差 EPSILON | 使用 EPSILON=0.05，判定 current_AP + EPSILON >= cost | [MovementResolver.cs](Scripts/Systems/Battlefield/MovementResolver.cs#L15), [MovementResolver.cs](Scripts/Systems/Battlefield/MovementResolver.cs#L27) | 对齐 | 保持不变 | 低 |
| A2 | 可达性预算判定 | 超预算不可达 | [MovementResolver.cs](Scripts/Systems/Battlefield/MovementResolver.cs#L132) | 对齐 | 保持不变 | 低 |
| A3 | AP 消耗后处理 | 扣 AP 后保留1位小数，负微小值归零 | [GameSessionController.cs](Scripts/Systems/Gameplay/GameSessionController.cs#L115) | 偏差 | 将移动后 AP 改为四舍五入到1位小数，再做非负夹紧 | 高 |
| A4 | 高亮阈值 | AP>=4 可执行关键操作 | [Grid3DRenderer.cs](Scripts/Rendering/Grid3DRenderer.cs#L277) | 对齐 | 保持不变 | 低 |
| F1 | 疲劳分段（战斗系数） | 0-4:1.0, 5-6:0.9, 7-8:0.5 | [Battalion.cs](Scripts/Models/Battalion.cs#L81) | 对齐 | 保持不变 | 低 |
| F2 | 疲劳分段（最大AP） | 5-6 -20%，7-8 -50%，>8 崩溃 | [Battalion.cs](Scripts/Models/Battalion.cs#L82), [Battalion.cs](Scripts/Models/Battalion.cs#L85) | 基本对齐 | 保持不变 | 低 |
| F3 | 疲劳取值范围 | 文档写 0-8；>8 组织涣散 | [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L33), [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L41), [Grid3DRenderer.cs](Scripts/Rendering/Grid3DRenderer.cs#L153) | 部分偏差 | 代码当前允许到10；建议文档改为 0-10（9-10=崩溃）或代码改上限8（二选一） | 中 |
| F4 | OOS流转阈值 | Turn1 +1，Turn2+ +2 | [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L27), [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L28) | 对齐 | 保持不变 | 低 |
| F5 | 补给内疲劳恢复 | AP>=8 恢复2；4<=AP<8 恢复1 | [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L36), [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L38) | 对齐 | 保持不变 | 低 |
| C1 | 战斗发起 AP 门槛 | 最低消耗阈值 4 AP | [Cold_War_Wargame_PRD_TDD.md](Cold_War_Wargame_PRD_TDD.md#L61), [GameSessionController.cs](Scripts/Systems/Gameplay/GameSessionController.cs#L78), [CombatFlowController.cs](Scripts/Systems/Gameplay/CombatFlowController.cs#L54) | 对齐 | 保持不变 | 低 |
| C2 | 交战范围门槛 | 切比雪夫距离 <=2（5x5） | [EngagementResolver.cs](Scripts/Systems/Combat/EngagementResolver.cs#L14), [CombatFlowController.cs](Scripts/Systems/Gameplay/CombatFlowController.cs#L53) | 对齐 | 保持不变 | 低 |
| C3 | 战斗 CRT 区间 | +1.5/+1.0/+0.5/0/-0.5/-1.0 分段 | [CombatResolver.cs](Scripts/Systems/Combat/CombatResolver.cs#L122) | 对齐 | 保持不变 | 低 |
| C4 | 战斗结果文案区间 | 结果面板文案与CRT阈值一致 | [CombatDeploymentPanel.cs](Scripts/Systems/Combat/CombatDeploymentPanel.cs#L550) | 对齐 | 保持不变 | 低 |
| V1 | 胜利比值钳制 | R 钳制 [0.1,10.0]；0分边界特殊处理 | [VictoryTracker.cs](Scripts/Systems/Victory/VictoryTracker.cs#L151), [VictoryTracker.cs](Scripts/Systems/Victory/VictoryTracker.cs#L161) | 对齐 | 保持不变 | 低 |
| V2 | Victory 区间边界 | 4.0/2.0/1.25/0.8/0.5/0.25 分段 | [VictoryTracker.cs](Scripts/Systems/Victory/VictoryTracker.cs#L165) | 对齐 | 保持不变 | 低 |
| X1 | AP 重置与疲劳联动 | 回合切换应体现疲劳后的最大AP | [TurnManager.cs](Scripts/Systems/Turns/TurnManager.cs#L115), [TurnManager.cs](Scripts/Systems/Turns/TurnManager.cs#L118), [Battalion.cs](Scripts/Models/Battalion.cs#L82) | 偏差 | 将回合重置从固定12改为 b.GetMaxAP() | 高 |
| X2 | OOS 对战斗惩罚来源 | OOS 惩罚应基于当前作战单位的 turns_oos | [CombatDeploymentPresenter.cs](Scripts/Systems/Gameplay/CombatDeploymentPresenter.cs#L90), [CombatResolver.cs](Scripts/Systems/Combat/CombatResolver.cs#L15) | 已修复（待平衡复测） | 已改为逐营 OOS 先作用到对应营攻防基础值，再按多营聚合进入优势计算 | 中 |

## 重点风险说明

### 1) 回合重置 AP 未使用疲劳上限（高风险）
- 现状：回合切换直接重置到 12。
- 影响：疲劳阈值在机动端失效，导致 PRD 的疲劳机制被部分绕过。
- 位置：[TurnManager.cs](Scripts/Systems/Turns/TurnManager.cs#L118)

### 2) AP 扣减后未进行统一四舍五入（高风险）
- 现状：移动后 AP 仅做非负夹紧，未保留1位小数。
- 影响：长局中可能出现阈值边界抖动与显示/判定不一致。
- 位置：[GameSessionController.cs](Scripts/Systems/Gameplay/GameSessionController.cs#L115)

### 3) 疲劳上限文档与代码口径不一致（中风险）
- 现状：文档写 0-8，代码允许到10并以 >8 表示崩溃。
- 影响：设计沟通和调参时会产生误解。
- 位置：[Cold_War_Wargame_PRD_TDD.md](Cold_War_Wargame_PRD_TDD.md#L194), [SupplyManager.cs](Scripts/Systems/Supply/SupplyManager.cs#L41)

## 建议的最小修正顺序
1. 修正回合 AP 重置：12f -> GetMaxAP()。
2. 修正移动后 AP 后处理：统一 Round(1) + Clamp>=0。
3. 统一疲劳上限口径：文档改 0-10 或代码改 0-8（二选一）。
4. 再做一次 PRD-代码复核（只看阈值项），确认无回归。
