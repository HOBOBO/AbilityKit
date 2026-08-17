# AbilityKit 确定性数值

`com.abilitykit.deterministic` 为帧同步模拟代码提供确定性数值门面。

本包有意保持独立，不依赖 `core`、`pipeline`、`triggering`、`modifiers` 或示例包。现有浮点数包仍可用于表现层、工具、配置和非帧同步运行时路径。

## 当前范围

- `Fixed64`：带符号 Q32.32 定点标量，原始存储类型为 `long`（使用受检算术；.NET 7+ 使用 `Int128` 乘除法，其他环境使用精确的 `decimal` 回退，包括 Unity 编译器）。
- `FixedVec2` 和 `FixedVec3`：确定性向量，提供 `Magnitude` / `Normalized` / `Distance` / `Dot` / `Cross` / `Angle` / `Lerp`。
- `DeterministicMath`：提供 `Abs` / `Min` / `Max` / `Clamp` / `Lerp`、`Floor` / `Ceiling` / `Round`（五舍六入）、`Sqrt` 及完整三角函数 `Sin` / `Cos` / `Tan` / `Asin` / `Acos` / `Atan` / `Atan2`，并提供 `Pi` / `TwoPi` / `HalfPi` / `E` 常量。
- `DeterministicRandom`：基于整数的可复现随机流（xoroshiro128+，通过 SplitMix64 设种），输出定点数。
- `DeterministicHash`：对 `Fixed64` / `FixedVec2` / `FixedVec3` 执行稳定的 64 位 FNV-1a 哈希，用于模拟状态哈希（回滚校正、回放验证）。与 `object.GetHashCode` 不同，其值按设计在进程和平台之间保持一致。

浮点转换只能用于边界 API。模拟逻辑应交换原始定点值、比例、整数或确定性向量。

## 确定性保证

本包所有算法均使用普通 64 位整数操作（加、减、移位、比较）和 `Fixed64` 算术实现。任何计算路径都不涉及 `double` 或 `float`，因此结果在 .NET、Mono 和 IL2CPP 间逐位一致：

- 三角函数使用 CORDIC（正弦/余弦采用旋转模式，atan2 采用向量模式），包含 32 项 `atan(2^-i)` 表和增益预补偿。
- `Sqrt` 对 96 位操作数 `raw << 32` 使用逐位恢复整数平方根，并舍入到最近值。
- `src/AbilityKit.Deterministic.Tests/DeterministicGoldenTests.cs` 固定采样输入的精确原始输出；任何导致单个位变化的修改都会使门禁失败（`tools/test-gates.json` 中的 `core-stability`）。

相对 `System.Math` 的精度为数个 Q32.32 末位单位（约 1e-8），并已通过跨周期、象限的 `System.Math` 容差测试验证。

## 约定

- 源码可在 Unity 2022.3（C# 9）下编译：使用块级命名空间，不使用 C# 10+ 语法，也不依赖运行时新特性。
- 公共接口面遵循仓库的静态属性惯例（如 `Fixed64.Zero`、`Vec3` 风格），不使用公共字段或常量，并由 PublicAPI 分析器（`PublicAPI.Unshipped.txt`）跟踪。

## 后端策略

AbilityKit 公共类型有意保持窄职责，使内部后端未来可以替换为第三方定点实现或与其桥接。当前决策（2026-08）是保留上述自包含实现：带 `Int128` 快速路径的 Q32.32 已满足需求，缺失数学函数已用纯整数算法补齐，也无需审查外部依赖的许可或确定性。若需求变化，ET 的 `cn.etetet.truesync`（`Fix64` / `TSMath` / `TSVector`）仍是已知回退方案，但当前没有任何代码依赖它。

## 使用方

本包已作为帧同步栈的确定性数值核心接入 Unity 包图（2026-08，路线图 P0 到 P3 已完成）：

- `com.abilitykit.core`：`MathUtil.Sqrt` 经由本包实现，因此仓库内所有 `Vec2/Vec3.Magnitude` / `.Normalized` / `.Distance` 和 `Quat.LookRotation` 都具有确定性；`DeterministicMathBridge`（浮点边界门面：Normalize / Magnitude / Sqrt / ToFixed / ToVec3 / Quat.Normalize）位于 `core`，作为唯一共享实现。
- `com.abilitykit.combat.collision.abstractions`：射线检测/扫掠查询中的平方根和归一化点。
- `com.abilitykit.combat.motion`：轨迹长度、移动归一化、贴墙滑动和牵引范围求解器。
- `com.abilitykit.combat.projectile`：投射物运动学（位置/速度/距离预算采用 Q32.32；回滚快照 v7 存储原始 long 值）。
- `com.abilitykit.world.framesync`：`FrameTime` 以 Q32.32 累加时间（回滚载荷 v2 存储原始 long 值）。
- `com.abilitykit.demo.moba.runtime`：伤害/治疗/护盾/资源管线采用 Q32.32，并使用单次转换浮点边界（`MobaResourceFixedConvert`）。

添加新数值字段时的边界规则和回滚约定见帧同步包的《定点帧同步接入指南》（`com.abilitykit.world.framesync/Document/定点帧同步接入指南.md`）。
