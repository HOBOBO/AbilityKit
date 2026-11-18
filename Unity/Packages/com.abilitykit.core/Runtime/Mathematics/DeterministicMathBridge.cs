using AbilityKit.Deterministic;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 将浮点边界输入交给 Q32.32 定点数学内核计算，再返回浮点视图。
    /// 保留各入口既有的量化、非有限值处理、退化回退及异常传播规则。
    /// </summary>
    /// <remarks>
    /// 标量守卫与向量、四元数的严格转换具有不同契约，不应相互替换。
    /// 输入可表示不保证中间乘法、平方和或结果可表示；本桥接不承诺完整浮点调用链跨平台位一致。
    /// </remarks>
    public static class DeterministicMathBridge
    {
        /// <summary>
        /// 获取 <see cref="MathUtil.Epsilon"/> 经 Q32.32 向零量化后的定点阈值。
        /// </summary>
        /// <remarks>
        /// 该阈值供调用方使用；本类的向量及四元数归一化按定点长度是否为零回退，不比较此阈值。
        /// </remarks>
        public static Fixed64 Epsilon { get; } = Fixed64.FromSingle(MathUtil.Epsilon);

        /// <summary>
        /// 将标量转换为 Q32.32，非有限输入返回零，有限输入按定点分辨率向零量化。
        /// </summary>
        /// <param name="value">待转换的浮点标量。</param>
        /// <returns>量化后的定点值；NaN、正无穷和负无穷均返回零。</returns>
        /// <exception cref="System.OverflowException">有限输入超出定点转换范围。</exception>
        public static Fixed64 ToFixed(float value)
        {
            return float.IsFinite(value) ? Fixed64.FromSingle(value) : Fixed64.Zero;
        }

        /// <summary>
        /// 将定点值转换为浮点视图，结果可能因浮点精度发生舍入。
        /// </summary>
        /// <param name="value">待转换的定点值。</param>
        /// <returns>定点值对应的浮点近似，不保证重新转回后保留全部原始位。</returns>
        public static float ToSingle(Fixed64 value)
        {
            return value.ToSingle();
        }

        /// <summary>
        /// 逐分量严格转换浮点向量，不使用标量入口的非有限值归零守卫。
        /// </summary>
        /// <param name="value">各分量均应为有限且可转换的浮点向量。</param>
        /// <returns>逐分量向零量化后的定点向量。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">任一分量为非有限值。</exception>
        /// <exception cref="System.OverflowException">任一分量超出定点转换范围。</exception>
        public static FixedVec3 ToFixed(in Vec3 value)
        {
            return new FixedVec3(Fixed64.FromSingle(value.X), Fixed64.FromSingle(value.Y), Fixed64.FromSingle(value.Z));
        }

        /// <summary>
        /// 逐分量转换定点向量为浮点视图，不改变坐标轴或单位。
        /// </summary>
        /// <param name="value">待转换的定点向量。</param>
        /// <returns>各分量可能经过浮点舍入的向量。</returns>
        public static Vec3 ToVec3(in FixedVec3 value)
        {
            return new Vec3(value.X.ToSingle(), value.Y.ToSingle(), value.Z.ToSingle());
        }

        /// <summary>
        /// 严格转换向量后通过定点长度归一化，定点长度为零时返回零向量。
        /// </summary>
        /// <remarks>
        /// 极小的非零输入也可能因分量或平方和量化而触发零长度回退。
        /// 不采用浮点 Vec3.Normalized 的全部计算路径或阈值，不能据同名操作推断结果相同。
        /// </remarks>
        /// <param name="value">待归一化的有限浮点向量。</param>
        /// <returns>定点归一化后的浮点视图，或零向量。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">任一分量为非有限值。</exception>
        /// <exception cref="System.OverflowException">分量转换或定点中间运算超出范围。</exception>
        public static Vec3 Normalize(in Vec3 value)
        {
            return ToVec3(ToFixed(value).Normalized);
        }

        /// <summary>
        /// 严格转换四元数分量并按定点长度归一化，定点长度为零时返回恒等四元数。
        /// </summary>
        /// <remarks>
        /// 按现有分量平方与求和顺序计算；极小非零输入也可能触发量化后的零长度回退。
        /// 不使用标量入口的非有限值归零守卫，也不使用 Epsilon 进行近零判断。
        /// </remarks>
        /// <param name="value">待归一化的有限浮点四元数。</param>
        /// <returns>定点归一化后的浮点四元数，或 Identity。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">任一分量为非有限值。</exception>
        /// <exception cref="System.OverflowException">分量转换或定点中间运算超出范围。</exception>
        public static Quat Normalize(in Quat value)
        {
            var x = Fixed64.FromSingle(value.X);
            var y = Fixed64.FromSingle(value.Y);
            var z = Fixed64.FromSingle(value.Z);
            var w = Fixed64.FromSingle(value.W);
            var len = DeterministicMath.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
            if (len == Fixed64.Zero)
            {
                return Quat.Identity;
            }

            return new Quat((x / len).ToSingle(), (y / len).ToSingle(), (z / len).ToSingle(), (w / len).ToSingle());
        }

        /// <summary>
        /// 严格转换向量后，通过定点平方和与开方计算长度。
        /// </summary>
        /// <remarks>
        /// 分量在表示范围内也可能在平方或求和时溢出；极小非零向量可能得到零长度。
        /// </remarks>
        /// <param name="value">待计算长度的有限浮点向量。</param>
        /// <returns>定点长度的浮点视图。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">任一分量为非有限值。</exception>
        /// <exception cref="System.OverflowException">分量转换或定点平方和超出范围。</exception>
        public static float Magnitude(in Vec3 value)
        {
            return DeterministicMath.Sqrt(ToFixed(value).SqrMagnitude).ToSingle();
        }

        /// <summary>
        /// 严格转换标量后执行定点开方，不使用非有限值归零守卫。
        /// </summary>
        /// <remarks>
        /// 量化后为负的输入会失败；绝对值不足一个定点计数的负输入可先量化为零。
        /// </remarks>
        /// <param name="value">待转换并开方的有限浮点标量。</param>
        /// <returns>定点开方结果的浮点视图。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">输入为非有限值，或量化后为负。</exception>
        /// <exception cref="System.OverflowException">输入超出定点转换范围。</exception>
        public static float Sqrt(float value)
        {
            return DeterministicMath.Sqrt(Fixed64.FromSingle(value)).ToSingle();
        }

        /// <summary>
        /// 将角度经标量守卫转换后，调用定点内核计算余弦。
        /// </summary>
        /// <param name="radians">弧度角；非有限值按零角处理。</param>
        /// <returns>定点余弦结果的浮点视图。</returns>
        /// <exception cref="System.OverflowException">有限角度超出定点转换范围。</exception>
        public static float Cos(float radians)
        {
            return DeterministicMath.Cos(ToFixed(radians)).ToSingle();
        }

        /// <summary>
        /// 将角度经标量守卫转换后，调用定点内核计算正弦。
        /// </summary>
        /// <param name="radians">弧度角；非有限值按零角处理。</param>
        /// <returns>定点正弦结果的浮点视图。</returns>
        /// <exception cref="System.OverflowException">有限角度超出定点转换范围。</exception>
        public static float Sin(float radians)
        {
            return DeterministicMath.Sin(ToFixed(radians)).ToSingle();
        }

        /// <summary>
        /// 将角度经标量守卫转换后，调用定点内核以正弦除以余弦计算正切。
        /// </summary>
        /// <param name="radians">弧度角；非有限值按零角处理。</param>
        /// <returns>定点正切结果的浮点视图，不提供极点饱和保证。</returns>
        /// <exception cref="System.DivideByZeroException">内核计算的余弦为零。</exception>
        /// <exception cref="System.OverflowException">输入转换或定点除法超出范围。</exception>
        public static float Tan(float radians)
        {
            return DeterministicMath.Tan(ToFixed(radians)).ToSingle();
        }

        /// <summary>
        /// 分别守卫两个标量后，计算向量方向对应的弧度角，参数顺序为 y、x。
        /// </summary>
        /// <remarks>
        /// 定点值不保留有符号零；零向量返回零，零 y 与负 x 返回正圆周角的一半。
        /// </remarks>
        /// <param name="y">纵向分量，非有限值归零。</param>
        /// <param name="x">横向分量，非有限值归零。</param>
        /// <returns>定点反正切结果的浮点视图，单位为弧度。</returns>
        /// <exception cref="System.OverflowException">有限分量超出定点转换范围。</exception>
        public static float Atan2(float y, float x)
        {
            return DeterministicMath.Atan2(ToFixed(y), ToFixed(x)).ToSingle();
        }

        /// <summary>
        /// 将输入经标量守卫转换后，计算自然指数，精度下溢行为由定点内核决定。
        /// </summary>
        /// <param name="value">指数，非有限值按零处理。</param>
        /// <returns>定点自然指数的浮点视图；非有限输入得到一。</returns>
        /// <exception cref="System.OverflowException">输入转换或内核计算超出可表示范围。</exception>
        public static float Exp(float value)
        {
            return DeterministicMath.Exp(ToFixed(value)).ToSingle();
        }

        /// <summary>
        /// 将输入经标量守卫转换后计算自然对数，量化后的值必须严格为正。
        /// </summary>
        /// <remarks>
        /// 正但不足定点分辨率的输入会量化为零；非有限输入也归零，随后均触发定义域异常。
        /// </remarks>
        /// <param name="value">待计算自然对数的浮点标量。</param>
        /// <returns>定点自然对数的浮点视图。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">守卫与量化后的输入不为正。</exception>
        /// <exception cref="System.OverflowException">有限输入超出定点转换范围。</exception>
        public static float Ln(float value)
        {
            return DeterministicMath.Ln(ToFixed(value)).ToSingle();
        }

        /// <summary>
        /// 分别守卫底数和指数后计算定点幂，沿用内核的整数幂及非整数幂规则。
        /// </summary>
        /// <remarks>
        /// 非有限参数各自归零；零的零次幂返回一。
        /// 负底数的非整数幂与零底数的负指数分别按内核定义域和除零规则失败。
        /// 最小负定点底数的整数幂会在取绝对值时溢出，即使指数为零也不例外。
        /// </remarks>
        /// <param name="baseValue">底数，非有限值归零。</param>
        /// <param name="exponent">指数，非有限值归零；是否为整数按量化后的值判断。</param>
        /// <returns>定点幂结果的浮点视图。</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">量化后为负底数的非整数幂。</exception>
        /// <exception cref="System.DivideByZeroException">零底数对应负指数。</exception>
        /// <exception cref="System.OverflowException">参数转换或定点内核计算超出范围。</exception>
        public static float Pow(float baseValue, float exponent)
        {
            return DeterministicMath.Pow(ToFixed(baseValue), ToFixed(exponent)).ToSingle();
        }
    }
}
