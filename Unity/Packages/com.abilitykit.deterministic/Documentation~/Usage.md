# 定点数、随机流与哈希使用契约

> 文档类型：使用指南 / API 契约
> 适用范围：当前 0.1.0 源码中的公开数值入口；依赖和 Unity 最低版本以 package.json 为准。
> 核校状态：已核对表示、转换、取整、算术分支、随机状态和哈希源码；本轮验证限本地 .NET，未执行 Unity、Mono 或 IL2CPP。
> 事实基线：2026-11-24。

从[包入口](../README.md)复制最短示例；本包提供数值原语，不接管帧时钟、快照协议、输入顺序或世界恢复。

## 表示、转换与取整

`Fixed64` 用 `long RawValue` 表示带符号 Q32.32：一对应 `4294967296`，分辨率为 `2^-32`，范围为 `[-2147483648, 2147483648 - 2^-32]`。

| API | 行为与约束 |
| --- | --- |
| `FromRaw(long)` | 原样保留原始计数；恢复已有状态时使用，不做单位换算 |
| `FromInt32` / `FromInt64` | 乘以 `2^32`；超出 raw 范围抛 `OverflowException` |
| `FromRatio(n, d)` | 表示比例；分母零抛 `DivideByZeroException`，结果仍受编译路径和范围限制 |
| `FromSingle` / `FromDouble` | 拒绝 NaN 和无穷；乘以 `2^32` 后受检转 long，向零截断；超范围失败 |
| `FromDecimal` | 乘以 `2^32` 后受检转 long，向零截断；中间值或结果可溢出 |
| `ToInt32` / `ToInt64` | 向零截断，例如 `-1.5` 得到 `-1` |
| `Floor` / `Ceiling` | 向负无穷取整 / 向正无穷取整；边缘结果可能溢出 |
| `Round` | 当前是 `Floor(value + Half)`；半值向正无穷，`1.5 → 2`、`-1.5 → -1`；不是银行家舍入，靠近最大值时加法可先溢出 |
| `ToSingle` / `ToDouble` | 浮点边界视图，不能无损保存所有 raw 计数 |

本包的浮点构造拒绝非有限值。其他包可能在桥接层先守卫或归零，不能把其策略当作 `Fixed64` 的默认行为。保存与恢复优先使用 raw 计数；坐标单位、序列化字节布局和格式版本由所属协议决定。

## 运算与失败行为

加、减、取负采用受检整数运算；乘、除和比例构造有不同目标分支，见[跨目标限制](#跨目标限制)。失败传播异常，不自动饱和、不返回 NaN，也不自动回退成浮点运算。

| 能力 | 使用边界 |
| --- | --- |
| `Sqrt` | 量化后为负数时抛 `ArgumentOutOfRangeException`；使用整数开方与就近舍入 |
| `Sin` / `Cos` / `Tan`、反三角函数 | 角度单位是弧度；CORDIC 产生近似值，特殊角不保证精确数学值。Tan 使用定点除法，不能把旧源码注释中的饱和描述作为契约 |
| `Asin` / `Acos` | 输入需在 `[-1, 1]`；中间算术仍受当前数值路径限制 |
| `Exp` / `Ln` / `Pow` | 已有实现；Ln 要求正数，负底数幂要求整数指数，零的负次幂失败；中间量溢出与低值下溢需由调用方处理 |
| `FixedVec2` / `FixedVec3` | 长度依赖分量平方及求和；分量本身可表示不保证平方和可表示。零长度归一化返回零向量 |
| `Lerp` / `LerpClamped` | 前者不夹紧插值参数，后者夹紧到 `[0, 1]`；两者仍可能在中间差值或乘法溢出 |

例如 `Fixed64.MinValue` 取负失败；向量分量为 50000 时，其平方已超出 Q32.32 数值范围。数学定义域成立不能替代实际中间范围检查。

## 随机流与哈希

`DeterministicRandom` 使用 SplitMix64 初始化 xoroshiro128+ 状态。相同算法、种子、状态和调用顺序才会重现同一序列；宿主负责实例生命周期与同步访问。

- `SetSeed` 重置 Seed 和 Sequence。`NextFixed01()` 返回 `[0, 1)` 的定点值；`NextInt32(min, max)` 要求 `min < max`，使用拒绝采样，可能消耗多次原始抽样。
- 回滚用 `CaptureState(out s0, out s1, out sequence)` 与 `RestoreState` 恢复推进状态；仅保存种子不能直接恢复中途位置。`RestoreState` 不修改 `Seed` 属性，若还需要保留原始种子元信息，应由宿主另行保存。
- `NextFixed(min, max)` 经定点减、乘、加计算，范围跨度也可能溢出；不能将所有有序端点组合视为安全输入。
- 多个业务域可通过 `DeriveSeed(rootSeed, scope)` 派生流种子；域标识与调用顺序由项目稳定定义。该算法不用于密码学或安全随机。

`DeterministicHash` 是按 raw long 的低字节到高字节混合的 FNV-1a 64 位值；向量按 X、Y、Z 分量顺序混合。相同输入 raw 值和组合顺序可得到相同结果，哈希本身不能消除上游数值差异，也不能保证无碰撞。

持久化或同步比较使用 `DeterministicHash` 的明确算法，不使用向量的 `GetHashCode()` 代替协议哈希。后者使用运行时的 `HashCode.Combine`，不是跨进程稳定格式。

## 跨目标限制

当前 [Fixed64 源码](../Runtime/Deterministic/Fixed64.cs)仍有两条编译路径：

| 操作 | `NET7_0_OR_GREATER` | 其他目标，包括当前 Unity 编译路径 |
| --- | --- | --- |
| 乘法 | Int128 乘积算术右移 32 位，再受检转 long；负数低位被移除时趋向负无穷 | decimal 乘积除以 `2^32`，再转 long 向零截断 |
| 除法 / 比例构造 | Int128 左移后整数除法 | decimal 中间算术后转 long；decimal 精度及范围也构成限制 |

直接反例：`Fixed64.FromRaw(-1) * Fixed64.FromRaw(1)` 的 raw 结果，在 Int128 右移路径为 `-1`，在 decimal 路径为 `0`。decimal 的中间乘积还可能在最终定点结果可表示时先溢出。

因此不能宣称整个包在 .NET、Mono 与 IL2CPP 间逐位一致，也不能因一组 CORDIC、开方或哈希黄金向量通过而提升为全模拟链保证。更换统一舍入、溢出或算法属于数值兼容变更，须另行评审存量快照、黄金向量和消费者影响；本轮只修正文档。

## 源码与验证范围

- [Fixed64](../Runtime/Deterministic/Fixed64.cs)：表示、边界转换与受检算术。
- [DeterministicMath](../Runtime/Deterministic/DeterministicMath.cs)：取整、CORDIC、开方与指数函数。
- [FixedVec3](../Runtime/Deterministic/FixedVec3.cs)、[FixedVec2](../Runtime/Deterministic/FixedVec2.cs)：向量运算与中间值。
- [随机流](../Runtime/Deterministic/DeterministicRandom.cs)、[状态哈希](../Runtime/Deterministic/DeterministicHash.cs)：状态推进与混合顺序。

本轮用仓库已有数值测试入口复核本地 .NET 分支，并编译执行包入口示例；这属于局部 E3。未执行其他目标的数值交叉验证、Player 构建或完整战斗重演，不将本次验证写成跨平台保证。
