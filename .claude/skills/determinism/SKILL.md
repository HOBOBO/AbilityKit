---
name: determinism
description: AbilityKit 定点数与确定性栈——com.abilitykit.deterministic 定点数学(Fixed64/Q32.32/FixedVec/CORDIC 三角/DeterministicHash)、DeterministicMathBridge 收敛层、raw long 快照约定、帧时钟确定≠全战斗确定的边界、定点改造配方(基类 raw 存储+float 视图)、已知漂移源(遍历序/System.Math/float 计时)。触发场景：定点化改造、确定性 bug、漂移排查、Fixed64、raw long 快照、回滚 payload、golden 测试、帧同步一致性、位一致断言、MathUtil.Sqrt、DeterministicMathBridge。
---

# determinism skill

基于源码核校（2026-08-16）。定点化 P0-P4 已全部完成：全模拟域定点（伤害/资源/护盾/FrameTime/Effect/buff 链）+ 审计修复（三角/遍历序/整数对齐）+ bridge 收敛 Core + 旧 Numerics 删除。剩余为 float 计时残留收尾与外部步骤（OpenUPM/Unity 验证）。

## 核心包与边界

- **`com.abilitykit.deterministic`**：Fixed64/FixedVec2/FixedVec3、DeterministicMath（Sqrt/Floor/Ceiling/Round/全套三角函数 CORDIC）、DeterministicRandom、DeterministicHash(FNV-1a 64)。74 测试含 golden 位锁定。块命名空间（Unity 2022.3/C# 9 兼容），公共字段/const 全部是**静态属性**（PublicApiAnalyzers 3.3.4 不吃公共字段）。
- **`DeterministicMathBridge`**（`AbilityKit.Core.Mathematics`，公开唯一实现）：ToFixed/ToSingle（NaN/Inf→0 守卫）+ Cos/Sin/Tan/Atan2 float 边界成员。三份 internal bridge（collision/motion/demo.moba）已删除收敛到此。
- **`MathUtil.Sqrt` 已路由到定点内核**：Vec2/Vec3.Magnitude 与 Quat 的 MathF.Sqrt 全走它，**全仓库一切 Normalized/Magnitude/Distance 自动确定**（含未逐点改造的调用）。[0,2e9) 确定域位一致；负数→NaN、超大→回退硬件 sqrt。

## 确定性分层（铁律）

1. **帧时钟确定 ≠ 全战斗确定**：定点数学只约束时间/数值表达，不自动保证逐位一致——遍历序、System.Math 超越函数、float 计时残留都会破坏。
2. **定点栈与同步模式正交**：MOBA 最终形态是"定点模拟内核 + 帧同步/状态同步双模式"；shooter 保持纯 float 对照组不动。
3. **raw long 快照约定**：定点字段在回滚/序列化里存 Q32.32 raw long（如 `TimeRaw/FixedDeltaRaw/CurrentRaw`），float 属性是边界视图。

## 定点改造配方（后续残留改造复用）

**基类集中 raw 存储 + float 视图属性 + 接口不动 + 回滚 payload raw 化 + 测试替身删遮蔽属性**。

- raw 累加用整数（`>>32` 即 floor，`(raw*1000)>>32` 即 ms），float 属性只在初始化/回填/边界读取时单次换算。
- 无限期/哨兵用 `long.MinValue`（BuffTimer payload v3）。
- 接线三件套：asmdef + package.json + csproj（+InternalsVisibleTo）加 `AbilityKit.Deterministic` 引用；Unity manifest 不动（包走 Packages/ 内嵌解析）。

## 已知漂移源（要确定性必须排查）

| 类别 | 例子 |
|------|------|
| System.Math 超越函数 | targeting `CommonRules.cs` SectorShapeRule 的 cos、弹丸碰撞挤出方向、连击衰减 `Math.Pow`、ability `DefaultNumericRpnFunctions`（exp/log 无确定性实现且零配置使用，已从注册表删除） |
| 遍历序 | `BehaviorManager.cs` foreach Dictionary、`MobaEntityManager` HashSet、快照输出序 5 处——应排序视图/List 化 |
| float 时间累加 | 已清零的主体见下；`MobaBattleDriverHost.LogicTimeSeconds` double 是会话/表现域豁免 |

## 剩余 float 计时（已清零/保留裁定）

- 已清零：HFSM Delay/Timeout、ability 旧引擎 RunningActions、SkillPipelineContext/SkillTimelinePhase/MobaTimelinePlayer、motion FixedStep/FixedDelta/Trajectory、buff 链（ElapsedRaw/IntervalRemainingRaw/durationRaw）。
- **保留 float（视图边界，位一致）**：`MobaBuffStateRecoveryProvider`（重连重建视图）、`MotionState.Time`（公共状态字段）、`MobaBattleDriverHost.LogicTimeSeconds`（会话/表现域）。
- 未排期大项：属性系统（AttributeGroup）整块定点化（当前 float 存储 + 读取处单次换算，位一致）。

## 坑

- **float 视图丢低位，不能反向断言 raw long**——断言确定性用 raw/golden，不用 float 边界。
- **非 dyadic 步长定点 floor 不等于直觉**：0.5s@30fps 的定点 floor 是 14 帧不是 15（raw(1/30f) 略大于精确 1/30）。
- **PublicAPI 清单**：新增公共 API 要同步 `PublicAPI.Unshipped.txt`，静态成员带 `static` 前缀，隐式构造写 `Type.Type() -> void`。
- **命名空间撞车**：motion 包内 `Core.Mathematics.xxx` 会撞 `AbilityKit.Combat.MotionSystem.Core` 兄弟命名空间，直接用 `DeterministicMathBridge`（顶部已有 using）。
- 溢出语义：从 float 静默归零变为 checked 抛异常（fail-loud，对锁步正确；病态配置如伤害 >~21M 会崩）。

## 相关文档与 skill

- 详细迁移记录见记忆 `deterministic-migration-roadmap`；接入指南 `com.abilitykit.world.framesync/Document/定点帧同步接入指南`。
- 帧同步/回滚 → [framesync-prediction-rollback](../framesync-prediction-rollback/SKILL.md)
- 发布（deterministic 在 batch-1）→ [publishing](../publishing/SKILL.md)
