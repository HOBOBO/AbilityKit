# 数学原语的单位、退化输入与变换契约

`AbilityKit.Core.Mathematics` 的六个原语位于 `Runtime/Math`：`MathUtil`、`Vec2`、`Vec3`、`Quat`、`Transform3`、`Ray3`。它们提供无引擎依赖的数学计算；坐标单位、碰撞形状、领域精度、协议量化及项目坐标转换由使用方决定。`Runtime/Mathematics` 的已有适配单独见 [定点数学桥接契约](./DeterministicMathBridge.md)。

## 浮点与开方范围

这些类型保存普通单精度浮点分量，构造不校验有限性。`Epsilon` 为绝对容差 `1e-6`；具体入口可能比较缩放分量或长度平方，不能把它解释为全包统一的相对误差。

`MathUtil.Sqrt` 对 `[0, 2e9)` 内输入先转换为 Q32.32，调用 Deterministic 的整数开方，再转回 `float`。低于 `2^-32` 的正输入会被截断为零；极小非零向量因此可能具有零长度及零归一化结果。达到上限、负数、NaN 及无穷值时回退 `System.Math.Sqrt`。负数返回 NaN，正无穷返回正无穷；负零通过定点路径变为正零。

整数开方子路径不能证明整个数学链跨平台位一致。平方和、加减乘除、转换和轴角中的 `System.MathF.Sin/Cos` 仍采用各自的浮点计算。需要锁步的领域应使用经过评审的完整数值链，不能仅替换一个开方函数。本轮 .NET 结果不替代 Mono、IL2CPP 或其他平台的实测证据。

## 标量、向量及相等比较

| 入口 | 当前契约 |
| --- | --- |
| `IsZero`、`Approximately` | 按绝对值或绝对差与容差比较，包含边界；负容差及 NaN 比较不命中。相同无穷值的差为 NaN，因此不近似相等。 |
| `Clamp` | 先比较下限再比较上限，不验证上下限顺序；浮点 NaN 不被修复。 |
| `Sign` | 正数为 1，负数为 -1，正负零及 NaN 为 0。 |
| `MathUtil.Min/Max` | 严格比较成立才选左值，否则选右值；相等、NaN 与有符号零的结果受参数位置影响。 |
| `Vec2/Vec3.Min/Max` | 分量调用 `System.MathF.Min/Max`，不应套用 MathUtil 的选择表达式。 |
| `Lerp` | 将因子限制到零至一后计算 `a + (b - a) * t`，没有端点快捷返回；差值溢出或非有限输入即使在端点也可能得到 NaN。 |
| 向量归一化 | 长度大于零才除以长度，否则返回零向量；不提供有限性验证。极小量化、平方和溢出和非有限分量均影响结果。 |
| 标量除以零 | 按浮点规则产生无穷或 NaN，不抛出整数除零异常。 |

正 X、正 Y、正 Z 分别为右、上、前。`Cross(Right, Up)` 等于 `Forward`；绕正 Y 轴正转四分之一周，`Forward` 转为 `Right`。长度与距离继承开方量化和回退边界。

值相等使用分量的 `float.Equals`，不使用容差：NaN 分量可以相等，正负零相等。`Quat` 的 `q` 与 `-q` 可以表示相同旋转，但分量比较会判为不相等。哈希仅用于进程内集合；`ToString` 使用当前区域设置，不作为协议格式。Numerics 转换原样复制分量，不变换坐标或单位。

## 四元数

分量顺序为 X、Y、Z、W；默认结构值是零四元数，与 `Identity` 不同。构造及 Numerics 转换不归一化。

- `FromAxisAngle` 的角度单位为弧度；轴先归一化。零轴产生零向量部及半角余弦标量部，不保证单位长度。
- `Normalized`、`Inverse` 在长度平方不大于 `Epsilon` 时返回 `Identity`；NaN 不触发该比较回退。
- `Rotate` 执行 `q * (v, 0) * Inverse(q)`。有效非单位四元数使用实际逆；零四元数得到零向量。近零输入受逆运算回退影响，不应作为有效旋转使用。
- Hamilton 乘积 `a * b` 用于有效旋转时表示先应用 b，再应用 a；乘积不自动归一化。
- `LookRotation` 以局部正 Z 对齐前方向，用上方向叉乘前方向生成正 X。退化时只尝试世界正 Y；仍退化则返回 `Identity`，不报告失败原因。它不会自动再尝试正 X 等其他轴。

## 点、方向及逆变换

点按逐轴缩放、旋转、平移顺序转换。`TransformDirection` 同样包含缩放，只忽略平移；`Forward`、`Right`、`Up` 也不保证单位长度。与其他引擎的同名接口对接时必须核对语义。

直接逆变换先减平移（方向不需要此步），再逆旋转，最后逐轴乘缩放倒数。缩放绝对值不大于 `Epsilon` 时使用零倒数，信息已经丢失，不返回不可逆标志。负缩放可以正常参与非退化计算。

`Inverse` 返回仍按“先缩放再旋转”应用的另一个 `Transform3`。非均匀缩放与旋转一般不可交换，因此它不能总是表示精确逆变换。例如零平移、绕正 Z 轴旋转四分之一周、缩放 `(2, 4, 1)`，将 `Right` 正向变换后，直接逆变换会恢复 `Right`，而 `Inverse.TransformPoint` 得到 `(0.5, 0, 0)`。

需要逐次逆变换点或方向时应调用 `InverseTransformPoint/Direction`。本批保留 `Inverse` 的兼容结果并增加反例测试；若需要一般逆矩阵或可逆性结果，应另行评审表示、API 及迁移方案。

```csharp
using System;
using AbilityKit.Core.Mathematics;

var transform = new Transform3(
    new Vec3(10f, 0f, 0f),
    Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f),
    new Vec3(2f, 3f, 4f));
var local = new Vec3(1f, 2f, 3f);
var world = transform.TransformPoint(local);
var recovered = transform.InverseTransformPoint(world);
```

上例的有限、非退化输入可在浮点误差范围内恢复，不承诺位级相等。`default(Transform3)` 的旋转和缩放均为零，恒等变换需要显式使用 `Identity`。

## 射线参数

`Ray3` 原样保存方向。`GetPoint(distance)` 返回 `Origin + Direction * distance`，允许负参数。仅在单位方向下，参数才等于沿射线的有符号距离。零方向和有限参数得到起点。碰撞 API 是否要求单位方向由碰撞领域决定，Core 不在构造时调整。

## 验证与后续范围

六个数学原语已统一中文多行契约注释，核对计算代码未变，并补充退化输入、插值、开方、旋转顺序、逆变换及射线参数测试。桥接类型已按单独批次补齐契约和测试，实际命令与范围见 Core 稳定化实施记录；跨平台验证、干净消费及发布门槛继续按包级计划推进。
