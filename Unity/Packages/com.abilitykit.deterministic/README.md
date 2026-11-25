<a id="abilitykit-确定性数值"></a>

# AbilityKit Deterministic

> 文档类型：包入口与使用导航
> 核校状态：2026-11-24 核对数值 API、编译路径与示例；本轮执行范围为本地 .NET。

`com.abilitykit.deterministic` 提供 Q32.32 标量与向量、定点数学、可恢复随机流和 raw 值哈希。它不接管世界时钟、同步模式或回滚协议，也不单独保证完整模拟跨平台逐位一致。

## 环境与安装

从项目约定来源安装，包版本与 Unity 最低版本见 [package.json](package.json)。本包没有 AbilityKit 或第三方包依赖。Unity 自定义 asmdef 引用 `AbilityKit.Deterministic`，C# 使用同名命名空间。

## 首次使用

以下完整类用比例构造四分之一步长，推进四次，并验证随机流的状态恢复：

```csharp
using AbilityKit.Deterministic;

public static class DeterministicQuickStart
{
    public static string Run()
    {
        var step = Fixed64.FromRatio(1, 4);
        var position = Fixed64.Zero;
        for (int i = 0; i < 4; i++)
            position += step;

        var random = new DeterministicRandom(42);
        random.CaptureState(out ulong s0, out ulong s1, out ulong sequence);
        ulong first = random.NextUInt64();
        random.RestoreState(s0, s1, sequence);
        bool repeated = first == random.NextUInt64();
        return $"{position.RawValue}:{repeated}";
    }
}
```

<a id="约定"></a>

预期返回 `4294967296:True`。四分之一可以精确表示；其他步长可能量化，不应把示例外推为任意时间步长都精确。保存状态时使用 raw 值及随机流状态，不用浮点视图无损回存。

<a id="当前范围"></a>

## 能力与文档

- `Fixed64`、`FixedVec2`、`FixedVec3`：受检数值与向量。
- `DeterministicMath`：取整、开方、三角函数、`Exp` / `Ln` / `Pow`。
- `DeterministicRandom`：种子、流推进与状态捕获/恢复。
- `DeterministicHash`：固定混合顺序的 FNV-1a 64 位哈希，不是密码学或无碰撞标识。

<a id="使用方"></a>

详细使用方式见[包内文档索引](Documentation~/README.md)与[使用契约](Documentation~/Usage.md)。

<a id="确定性保证"></a><a id="后端策略"></a>

## 确定性边界

Fixed64 当前在 `NET7_0_OR_GREATER` 使用 Int128 乘除路径，其他目标使用 decimal 路径。负小数乘法的取整方向和中间范围存在差异，因此不能承诺整个包在 .NET、Mono、IL2CPP 之间逐位一致。

`Round` 当前使用 `Floor(value + Half)`，半值向正无穷取整，不是“五舍六入”。浮点构造是边界操作；算法、原始输入、随机调用顺序、集合顺序和目标编译路径均需保持一致并实际验证。具体反例、异常和验证限制见[跨目标限制](Documentation~/Usage.md#跨目标限制)。
