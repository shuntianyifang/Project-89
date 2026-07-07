# 《冷战战役级兵棋推演系统》开发与技术设计文档 (PRD & TDD)

## 1. 项目概述 (Project Overview)

本项目为一个聚焦于冷战背景的战役级（营级编制）兵棋推演演示Demo。核心设计理念为系统驱动（System-Driven）与数据/逻辑解耦。在极短的开发周期（3天）限制下，项目摒弃了复杂的AI行为树与3D模型动画，采用纯逻辑运算、图层叠加工艺与热座模式（Hotseat），以最严谨的结构主义代码实现高度简化的战线调度与兵种协同推演。

## 2. 核心游戏规则 (Core Gameplay Mechanics)

### 2.1 棋盘与空间规则

*   **空间抽象：** 采用 2D 正方形网格（Square Grid）承载 3D 视觉元素。
*   **行动力（AP）标度：** 以 12 AP 为基准，保证用户体验的清晰。正交移动基准消耗为 1，斜向（对角线）移动基准消耗为 1.4。在移动判定代码中，永远不要直接比较两个浮点数，而是允许一个极其微小的误差范围。

```python
# 定义一个极小的误差值
EPSILON = 0.05

def can_move_to_tile(unit, cost):
    # 只要剩余 AP 加上微小误差后大于等于消耗，就放行
    if unit.current_AP + EPSILON >= cost:
        return True
    return False

def consume_ap(unit, cost):
    unit.current_AP -= cost
    # UI 显示前，强制保留一位小数，防止长串数字污染界面
    unit.current_AP = round(unit.current_AP, 1) 
    
    # 兜底：如果因为误差扣成了极小的负数（如 -0.01），直接归零
    if unit.current_AP < 0:
        unit.current_AP = 0.0
```

*   **防穿模机制（Corner Clipping）：** 进行斜向移动时，若两侧翼网格均为不可逾越障碍或被敌军占据，则移动判定被阻断。如果有一侧可移动则仍可进行斜向移动。
*   **控制区（ZOC, Zone of Control）：** 每个存活单位对其所在的网格及周围 8 格（切比雪夫距离为 1 的 3x3 区域）投射 ZOC。
*   **移动阻断：** 任何单位都无法进入敌方 ZOC。

### 2.2 地形与机动规则

采用“自然地形”与“基础设施”双图层独立结算逻辑。最终移动消耗代价（Cost）取两者极小值：

$$ Cost = \min(Cost_{terrain}, Cost_{infra}) $$

如果城镇方格上铺设了高速公路（消耗0.5），`min(1, 0.5)` 会自动选择高速公路的数值；如果没有高速公路，`min(2, \infty)` 会稳定输出 2，实现逻辑自洽。

**参数定义表：**

| 标识符 | 分类 | 类型 | 基础AP消耗 | 逻辑说明 |
| :--- | :--- | :--- | :--- | :--- |
| `infra_type = 2` | 基础设施 | 高速公路 | 0.5 | 战略机动极快 |
| `infra_type = 1` | 基础设施 | 支线公路 | 1 | 最常见的作战路径 |
| `terrain_type = 3` | 自然地形 | 城镇 | 1 | 默认对齐支线公路消耗 |
| `terrain_type = 2` | 自然地形 | 半城镇 | 1 | 默认对齐支线公路消耗 |
| `terrain_type = 0` | 自然地形 | 平原 | 2 | 标准越野消耗 |
| `terrain_type = 1` | 自然地形 | 森林 | 4 | 严重阻碍机动 |

### 2.3 模块化战斗结算规则

战斗摒弃微观战术操作，采用插槽化（Slot-based）面板与黑盒概率演算。

*   **交战区域（向炮声前进）：** 以防守方为中心，切比雪夫距离 ≤2（5x5网格范围）内，且 AP 满足最低消耗阈值（40 AP）的存活单位或在攻击范围内的炮兵类单位，皆可被编入战斗池。
*   **参战插槽：** 每次战斗攻击方与防守方均有 4 个固定席位：主力营 x2，辅助营 x1，炮兵营 x1。
*   **数值演算公式：** 设攻击方主力总基础火力为 $A_{base}$，防守方主力总基础防御为 $D_{base}$。地形对攻防的加成系数分别为 $T_{atk}$ 和 $T_{def}$。各项兵种特性与辅助营带来的绝对值修正总和为 $M_{attr}$，随机浮动因子为 $E$。

战斗结果判定值 $V$ 公式如下：

$$ V = rac{A_{base} \cdot (1 + T_{atk})}{D_{base} \cdot (1 + T_{def})} + M_{attr} + E $$

*   **战术战斗战果映射（CRT）：** 依据计算出的 $V$ 值区间（如 $V \ge 2.5$ 为碾压，$V < 0.8$ 为受挫），查表得出双方伤亡比例。
*   **伤亡分摊：** 采用权重随机抽取算法，将按比例计算出的总伤害点数分配至营内的各个子单位（Sub-units）。

**战斗发起的程序流程**

1.  **发起攻击：** 攻击方发起方点击敌方A营。
2.  **生成候选名单（池子）：** 系统遍历 5x5 区域，分别筛选出符合条件的进攻方单位列表和防守方单位列表。
3.  **UI 呈现：** 弹出战斗结算面板。如果是玩家回合，玩家可以从己方候选名单中，将单位拖拽进“主力2”、“辅助”、“炮兵”这三个空缺的插槽中。
4.  **自动判定敌方可能攻防力量：** 根据一套简单的贪心算法（比如优先把火力最强的营填入防守主力，缺少步兵/指挥单位等属性时优先寻找有对应单位的营拉入战斗），自动补齐它的对方的战斗插槽，显示给战斗双方的界面。
5.  **数值结算：** 点击“交战”，执行之前确定的演算抛骰子逻辑，按比例分配伤亡。

**战术战斗战果映射表：**

| 最终优势分 (V) | 战斗结果描述 (进攻方视角) | 战斗结果描述 (防守方视角) | 进攻方伤亡率 | 防御方伤亡率 | 进攻方疲劳增量 | 防御方疲劳增量 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| +1.5 至及以上 | 大获全胜 | 全军覆没 | 3% | 60% | 2 | 5 |
| +1.0 至 +1.49 | 酣畅大捷 | 重大失败 | 10% | 35% | 2 | 4 |
| +0.5 至 +0.99 | 略占上风 | 血战惨败 | 15% | 25% | 2 | 3 |
| 0 至 +0.49 | 平局 / 僵持 | 平局 / 僵持 | 20% | 20% | 2 | 1 |
| -0.5 至 -0.01 | 血战惨败 | 略占上风 | 25% | 15% | 3 | 1 |
| -1.0 至 -0.49 | 重大失败 | 酣畅大捷 | 35% | 10% | 4 | 1 |
| -1.5及以下 | 全军覆没 | 大获全胜 | 60% | 3% | 5 | 1 |

**伤亡分摊算法：**

1.  **建立基础数据结构（权重设定）**
    在子单位（Sub-unit）类中，除了 HP（血量）之外，增加一个核心属性：遇敌权重（Exposure Weight）。这个权重代表该单位在战场上暴露并吸引火力的概率。
    *   **高权重（如 10）：** 坦克连、机步连（顶在最前线，吸收绝大部分伤害）。
    *   **中权重（如 4）：** 营属迫击炮连、反坦克导弹排（二线支援，偶尔被反击）。
    *   **低权重（如 1）：** 营部指挥车、后勤补给卡车（躲在最后方，只有在极度倒霉或前线崩溃时才会被打中）。
2.  **计算总伤害池（Damage Pool）**
    根据 CRT 战果映射表，假设防守方（总血量 100）这次战斗需要承受 20% 的伤亡率。
    $$ DamagePool = 100 	imes 20\% = 20 	ext{ 点伤害} $$
    这 20 点伤害将被打散成 20 个“伤害骰子”，一颗一颗地扔进营里。
3.  **循环结算算法（核心代码逻辑）**
    接下来，写一个 `while (DamagePool > 0)` 的循环。每一次循环代表分配 1 点伤害：
    *   **Step A: 动态计算总权重。** 遍历营里所有活着的子单位，把它们的权重加起来，得到 $W_{alive}$。如果一个单位 HP 归零，它的权重立刻从池子里剔除。
    *   **Step B: 抛骰子。** 生成一个在 0 到 $W_{alive}$ 之间的随机浮点数 $R$。
    *   **Step C: 确定受击者。** 遍历存活的单位，将它们的权重依次累加，当累加值大于等于 $R$ 时，该单位即为被击中的倒霉蛋。
        （数学表达：子单位 $i$ 被抽中的概率为 $P_i = rac{W_i}{W_{alive}}$）
    *   **Step D: 扣血结算。** 被抽中的单位 HP 减 1。$DamagePool$ 减 1。
    *   **Step E: 死亡检定。** 检查该单位 HP 是否为 0，如果是，将其标记为死亡。

### 2.4 战斗营机制与规则

**营——连——排编制**
一个营里下辖数个连，而连又有各个排级单位，其情况如下所示：

| 连A | 连B | 连C | 连D |
| :--- | :--- | :--- | :--- |
| **排A1** | **排B1** | **排C1** | **排D1** |
| 单位（x2） | 单位（x3） | 单位（全部阵亡） | 单位（x1） |
| **排A2** | **排B2** | **排C2** | **排D2** |
| 单位（全部阵亡） | 单位（x4） | 单位（x2） | 单位（x1） |

这会影响一个营所对应的数据存储和UI显示。一个营里面的步兵排与所乘载具在营级（Battalion）的列表里，不单独放“装甲车”和“步兵”，而是设计一个名为 `MechPlatoon`（机步排）或 `MotPlatoon`（摩步排）的复合数据字典。战斗中步兵和所乘载具作为两个独立单位进行演算，但是在 UI 上，一个步兵排会这么显示其情况：

| 连A | 连B | 连C | 连D |
| :--- | :--- | :--- | :--- |
| **排A1** | **排B1** | **排C1** | **排D1** |
| 步兵（x5）<br>载具（x5） | 步兵（全部阵亡）<br>载具（1x） | 步兵（全部阵亡）<br>载具（全部阵亡） | 步兵（x3）<br>载具（x7） |
| **排A2** | **排B2** | **排C2** | **排D2** |
| 步兵（全部阵亡）<br>载具（x2） | 步兵（x1）<br>载具（x2） | 步兵（x2）<br>载具（全部阵亡） | 步兵（x3）<br>载具（x1） |

**战斗营攻防力量计算**

**一、 攻防数值的基准锚定 (The Baseline Anchor)**

1.  **步兵锚定物：** 标准摩托化步兵班 = 1 攻 / 1 防
2.  **载具锚定物逻辑：**
    *   **豹1A5:** 底层攻击设为 1.5，底层防御设为 2.5（机动防御）。
    *   **M1A1 艾布拉姆斯:** 底层攻击设为 3.0，底层防御设为 2.5（贫铀装甲）。

| 单位类型 | 遇敌权重 | 基础攻击力 | 基础防御力 | 设计意图 |
| :--- | :--- | :--- | :--- | :--- |
| 标准摩步班 | 10 | 1.0 | 1.0 | 锚定基准，攻防均衡。 |
| 反装甲小组 | 4 | 0.2 | 1.7 | 在隐蔽处作战，极度擅于防守 |
| 重型主战坦克 | 10 | 3.0 | 2.5 | 矛头单位，作战主力 |
| 步兵战车(IFV) | 10 | 1.5 | 2.0 | 拥有一定装甲，可用于进攻，但更适合利用其导弹进行防守 |
| 炮兵类单位 | 4 (对方无炮时为0) | 2.0 | 2.0 | 其曲射火力在进攻和防御中都相当有效 |
| 后勤补给卡车 | 1 | 0.0 | 0.1 | 绝对的脆皮，无法进攻，防守时可以协助步兵机动 |

**二、 战斗营数值的聚合与换算公式**

聚合缩放常量 (Aggregation Constant)，设为 $K=10$。
营级面板实际显示的攻防数值计算公式如下：

$$ Attack_{bat} = \sum_{i=1}^{n} rac{Attack_i 	imes S_i}{10} $$
*(注：$S_i$ 为存活状态，下取整或四舍五入保留一位小数均可)*

**三、 利用战斗效能比 (Combat Effectiveness, CE)计算单位伤亡其他影响**

$$ CE = rac{\sum_{i=1}^{n} Cost_i 	imes S_i}{\sum_{i=1}^{n} Cost_i} $$

**四、 攻防力量的“底层聚合”与“顶层惩罚”**

**第一层：底层数据的真实聚合**
$$ Attack_{base} = \sum_{i=1}^{n} UnitAttack_i 	imes S_i $$

**第二层：顶层组织度的惩罚 (Organizational Debuff)**
通过分段函数将 CE 转化为惩罚系数 $M_{org}$：
*   当 $CE \ge 80\%$ 时，$M_{org} = 1.0$ （建制完整，无惩罚）
*   当 $50\% \le CE < 80\%$ 时，$M_{org} = 0.8$ （建制受损，协同混乱）
*   当 $CE < 50\%$ 时，$M_{org} = 0.5$ （建制残破，各自为战）

最终投入战斗公式的实际攻击力：
$$ Attack_{actual} = Attack_{base} 	imes M_{org} $$
*(防御力计算同理)*

**三、 单位HP的机制**

HP 恢复与疲劳度恢复绑定，每当一个营回复了1点疲劳度，所有存活单位的HP都会+2直到满血；如果回复2点疲劳度，则HP+4直到满血。在观赏性伤亡比 UI中，载具类阵亡即计数+1，步兵类HP-1即计数+1。

| 单位类型 | HP | 设计意图 |
| :--- | :--- | :--- |
| 标准摩步班 | 8 | 锚定基准，高血量高吸收火力。 |
| 反装甲小组 | 3 | 人数少但火力强，容易被针对。 |
| 重型主战坦克 | 12 | 矛头单位，承伤主力，HP厚实。 |
| 步兵战车(IFV) | 8 | 伴随步兵，一定程度的装甲保护。 |
| 炮兵类单位 | 2 | 平均防御极低，一旦被打到直接报销。 |
| 后勤补给卡车 | 1 | 绝对的脆皮，被攻击就会轻易暴毙。 |

### 2.5 部队疲劳度与后勤机制

#### 2.5.1 阶跃式疲劳度系统
疲劳度（Fatigue）取值范围为 0 到 8：
*   **常态区 (0 - 4)：** 100% 最大 AP，无惩罚。
*   **疲惫区 (5 - 6)：** 最大 AP 扣除 20%，攻防力量 x0.9。
*   **力竭区 (7 - 8)：** 最大 AP 扣除 50%，攻防力量 x0.5。
*   **超过 8：** 组织涣散，ZOC失效，AP归0，极易被消灭。

**疲劳恢复（战役轮换机制）：** 回合结束阶段，处于补给网络内根据剩余 AP 回复：
*   剩余 $AP \ge 8.0$：疲劳度 -2。
*   $8.0 > AP \ge 4.0$：疲劳度 -1。

#### 2.5.2 基于泛洪算法的动态补给网
基于 Dijkstra 带权图搜索算法，模拟物资沿交通网流动的物理特性。
*   **绝对补给源 (Absolute Source)：** 地图边缘出口，SP = 36.0。
*   **次级补给源 (Secondary Source)：** 机场。

算法流程：
1.  **检测战略连通性：** 从绝对补给源开始正向蔓延（扣除地形AP阻力）。如果接触到枢纽（Hub），重新激活并将该枢纽产生的势能重置为 36.0。
2.  **检测次级连通性：** 若枢纽与老家断连但有机场，变更为 Secondary_Supported，释放局部 SP（如 18.0）。
3.  **彻底瘫痪：** 若皆无，释放 SP 归零。

**阵地检定与防“跑马”机制：**
*   **实体阻断：** 敌方单位所在的网格阻力为 $\infty$。
*   **ZOC 阻断：** 额外增加 15 SP 阻力。
*   **阵地门槛：** 剩余 $AP \ge 4$ 的单位 ZOC 才能产生阻断效果。

#### 2.5.3 断联状态流转
*   **Turn 0 (失去后勤)：** $turns\_oos = 0$。失去 AP 兑换疲劳度权限。
*   **Turn 1 (弹尽粮绝)：** $turns\_oos = 1$。疲劳度 +1；下回合最大 AP -20%；防守作战时额外 -0.5 优势分。
*   **Turn 2+ (溃散边缘)：** $turns\_oos = 2$。疲劳度 +2；下回合最大 AP 减半；作战时额外 -1.0 优势分。

### 2.6 胜利机制与胜利点数及其地理控制

**1. 战损交换比：**
$$ VP_{gain} = \sum_{i=1}^{n} Cost_{destroyed\_unit\_i} $$

**2. 地理控制得分与占领机制：**
进入网格及未被敌方ZOC影响的自身ZOC网格覆写控制权。冲突区为中立区。

**3. 战役胜负判定标准：**
```csharp
float MAX_RATIO = 10.0f;
float R = 1.0f; // 默认为 1:1 僵局
if (V_red == 0 && V_blue == 0) {
    R = 1.0f; // 双方都没得分，静坐战争
} 
else if (V_red == 0 && V_blue > 0) {
    R = MAX_RATIO; // 蓝方 n:0，直接赋予最大极限值
} 
else if (V_blue == 0 && V_red > 0) {
    R = 1.0f / MAX_RATIO; // 红方 n:0 (即 0.1)
} 
else {
    // 正常计算，并限制在 [0.1, 10.0] 区间内
    R = Mathf.Clamp((float)V_blue / V_red, 1.0f / MAX_RATIO, MAX_RATIO);
}
```

**战役结果表 (Victory CRT)：**

| 比例区间 (R) | 战果定性 (7挡制) | 军事学语义与底层逻辑表现 |
| :--- | :--- | :--- |
| $R \ge 4.0$ | 决定性胜利 (Decisive Victory) | 歼灭与突破：建制被抹除。 |
| $2.0 \le R < 4.0$ | 重大胜利 (Major Victory) | 重创与夺取：压倒性战损比，敌方战役级后撤。 |
| $1.25 \le R < 2.0$ | 边缘/战术胜利 (Marginal Victory) | 惨胜与击退：皮洛士式胜利。 |
| $0.8 \le R < 1.25$ | 血腥僵局 (Stalemate) | 静态消耗：堑壕/城镇拉锯战。 |
| $0.5 \le R < 0.8$ | 边缘/战术失利 (Marginal Defeat) | 迟滞与挫退：部分阵地被夺，成功给进攻方造成消耗。 |
| $0.25 \le R < 0.5$ | 重大失利 (Major Defeat) | 溃退与割裂：核心阵地失守，面临包围风险。 |
| $R < 0.25$ | 一败涂地 (Crushing Defeat) | 合围与覆灭：失去战役作战能力。 |

### 2.7 视野机制
*   **瞎子基线 (Base Vision = 6)：** 无 "Recon" 标签的营，仅能看 6 格。
*   **建制内侦察激活 (Standard Vision = 8)：** 营内至少有一个 "Recon" 实体存活，提升至 8 格。
*   **专业侦察营 (Advanced Vision = 12 及以上)：** 营种类判定为侦察营直接判定为 12。

### 2.8 战线表现与渲染机制
1.  **边界提取：** 遍历全图提取异色交界点（Edges）。
2.  **几何重构：** 连通图排序与卡特姆-罗姆样条（Catmull-Rom Spline）曲线平滑拟合。
3.  **视觉渲染：** Godot 中使用 Line2D 节点渲染，配合材质（Alpha）和着色器渐变实现专业军事地图前沿箭头感。

## 3. 软件工程与技术路线 (Technical Architecture)

### 3.1 实体架构 (Entity-Component Logic)
**容器化单位（Battalion Class）：** 营本身无硬编码属性，状态（如 `has_command_network()`）均为通过内部存活子单位动态计算（Dynamic Evaluation）。

### 3.2 渲染与视觉管线 (Rendering & Visuals)
纯色 3D 底座 + 2D 纯白 Billboard Sprite 剪影。公路网使用 Hub and Spoke 涌现渲染机制。

### 3.3 数据驱动与持久化 (Data-Driven Initialization)
全面分离逻辑代码与关卡数据：纯文本矩阵定义地图，JSON 定义战斗序列与部署。

### 3.4 回合控制流 (Turn State Machine)
采用“双手互搏（Hotseat）”全局状态机，切换阶段重置 AP 刷新全局 ZOC。

## 4. 关键代码标准

**1. 全局状态机与枚举定义**
```csharp
public enum FactionState { Neutral, Faction_Blue, Faction_Red }
public enum GamePhase { 
    StrategicMovement,  // 大地图机动阶段
    CombatDeployment_Attacker, // 攻击方部署阶段
    CombatDeployment_Defender, // 防守方部署阶段
    CombatResolution    // 数值结算阶段
}

public class TurnManager : Node
{
    public FactionState CurrentActiveFaction { get; private set; } = FactionState.Faction_Blue;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.StrategicMovement;
    
    // 记录当前交战的上下文，用于打完切回原回合
    private CombatContext _currentCombat; 
}
```

**2. 视野更新管线**
```csharp
public void UpdateGlobalVision()
{
    // 1. 物理遮蔽：无条件隐藏所有非当前活动阵营的单位 
    foreach (var unit in AllUnitsInScene)
    {
        if (unit.Faction != CurrentActiveFaction)
        {
            unit.Visible = false; 
            unit.UIElement.Visible = false; 
        }
    }

    // 2. 视野聚合与网格投射
    HashSet<Vector2I> visibleGrids = new HashSet<Vector2I>();
    foreach (var unit in GetUnitsByFaction(CurrentActiveFaction))
    {
        int visionRange = unit.CalculateVisionRange(); 
        var grids = GetGridsInChebyshevDistance(unit.GridPosition, visionRange);
        visibleGrids.UnionWith(grids);
    }

    // 3. 精准点亮
    foreach (var unit in AllUnitsInScene)
    {
        if (unit.Faction != CurrentActiveFaction && visibleGrids.Contains(unit.GridPosition))
        {
            unit.Visible = true;
            unit.UIElement.Visible = true;
        }
    }
    
    UpdateFrontlineRendering(); 
}
```

**3. 控制权与输入拦截**
```csharp
public void OnUnitClicked(BattalionUnit clickedUnit)
{
    // 绝对拦截
    if (clickedUnit.Faction != CurrentActiveFaction) return; 
    
    // 阶段拦截
    if (CurrentPhase != GamePhase.StrategicMovement)
    {
        if (!_currentCombat.IsUnitInEngagementZone(clickedUnit)) return;
    }
    
    SelectUnit(clickedUnit);
}
```

**4. 回合与战斗状态切换流**
```csharp
public void EndStrategicTurn()
{
    CurrentActiveFaction = (CurrentActiveFaction == FactionState.Faction_Blue) ? 
                           FactionState.Faction_Red : FactionState.Faction_Blue;
    CurrentPhase = GamePhase.StrategicMovement;
    ResetAP(CurrentActiveFaction);
    UpdateGlobalZOC();
    UpdateGlobalVision();
}

public void FinishAttackerDeployment()
{
    CurrentPhase = GamePhase.CombatDeployment_Defender;
    CurrentActiveFaction = _currentCombat.DefenderFaction;
    UpdateGlobalVision();
    ShowCombatDeploymentPanel(CurrentActiveFaction);
}

public void FinishDefenderDeployment()
{
    CurrentPhase = GamePhase.CombatResolution;
    ExecuteCombatResolution(_currentCombat);
    
    CurrentActiveFaction = _currentCombat.AttackerFaction;
    CurrentPhase = GamePhase.StrategicMovement;
    
    _currentCombat = null;
    UpdateGlobalZOC();
    UpdateGlobalVision();
}
```

### 4.1 编制模板库 JSON (unit_templates.json)
```json
{
  "us_mech_battalion_standard": {
    "name": "美军标准机械化步兵营",
    "visuals": {
      "icon": "icon_ifv_blue",
      "model_scale": 1.0
    },
    "companies": {
      "Company_A": {
        "name": "A连 (装甲步兵)",
        "platoons": {
          "Platoon_A1": {
            "type": "composite",
            "infantry": {
              "squad_1": { "unit_id": "us_mech_squad", "current_hp": 10, "max_hp": 10 }
            },
            "vehicle": {
              "veh_1": { "unit_id": "m2_bradley", "current_hp": 5, "max_hp": 5 }
            }
          }
        }
      },
      "Company_C": {
        "name": "C连 (坦克连)",
        "platoons": {
          "Platoon_C1": {
            "type": "standard",
            "units": {
              "tank_1": { "unit_id": "m1a1_abrams", "current_hp": 8, "max_hp": 8 }
            }
          }
        }
      }
    }
  }
}
```

### 4.2 战场初始部署 JSON
```json
{
  "faction_blue": [
    {
      "instance_id": "1st_battalion_7th_cav",
      "template_id": "us_mech_battalion_standard",
      "x": 12,
      "y": 15,
      "structure_overrides": {
        "remove_nodes": [
          "companies/Company_A/platoons/Platoon_A3"  
        ],
        "add_nodes": {
          "companies/Company_HQ/platoons/Sniper_Team": {
            "type": "standard",
            "units": {
              "sniper_1": { "unit_id": "recon_sniper_squad", "current_hp": 4, "max_hp": 4 }
            }
          }
        }
      },
      "state_overrides": {
        "Company_A": {
          "Platoon_A1": {
            "vehicle": {
              "veh_1": { "current_hp": 0 } 
            }
          }
        }
      }
    }
  ]
}
```
