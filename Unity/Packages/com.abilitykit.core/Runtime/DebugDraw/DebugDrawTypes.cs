using System;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Core.Debugging
{
    /// <summary>
    /// 保留任意整数位模式的旧绘制分类掩码，按整数值比较。
    /// 仅兼容至下一主版本，新代码使用 AbilityKit.Diagnostics.DebugDraw 中的对应类型。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public readonly struct DebugDrawMask : IEquatable<DebugDrawMask>
    {
        /// <summary>
        /// 获取原始位模式，不限制未知位、负值或组合。
        /// </summary>
        public readonly int Value;

        /// <summary>
        /// 原样保存分类位模式，不执行范围校验。
        /// </summary>
        /// <param name="value">原始整数位模式。</param>
        public DebugDrawMask(int value)
        {
            Value = value;
        }

        /// <summary>
        /// 比较两个掩码的原始整数值。
        /// </summary>
        /// <param name="other">待比较掩码。</param>
        /// <returns>原始值是否相同。</returns>
        public bool Equals(DebugDrawMask other) => Value == other.Value;
        /// <summary>
        /// 仅与装箱后的同类型掩码按整数值比较；空值和其他类型均不相等。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>对象是否为原始值相同的旧掩码。</returns>
        public override bool Equals(object? obj) => obj is DebugDrawMask other && Equals(other);
        /// <summary>
        /// 返回原始整数作为哈希值。
        /// </summary>
        /// <returns>原始整数位模式。</returns>
        public override int GetHashCode() => Value;
        /// <summary>
        /// 使用当前区域设置返回原始整数的十进制字符串，不展开分类名称。
        /// </summary>
        /// <returns>受当前区域设置影响的整数文本。</returns>
        public override string ToString() => Value.ToString();

        /// <summary>
        /// 获取零位模式，表示未启用任何分类。
        /// </summary>
        public static DebugDrawMask None => new DebugDrawMask(0);
        /// <summary>
        /// 获取全部位为一的模式，原始整数为负一，包含未知分类位。
        /// </summary>
        public static DebugDrawMask All => new DebugDrawMask(~0);
    }

    /// <summary>
    /// 保存四个字节通道的旧绘制颜色，不进行色彩空间转换。
    /// 默认结构体为透明黑色；仅兼容至下一主版本，新代码改用 Diagnostics 对应类型。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public readonly struct DebugDrawColor
    {
        /// <summary>
        /// 获取红色通道，范围为零至二百五十五。
        /// </summary>
        public readonly byte R;
        /// <summary>
        /// 获取绿色通道，范围为零至二百五十五。
        /// </summary>
        public readonly byte G;
        /// <summary>
        /// 获取蓝色通道，范围为零至二百五十五。
        /// </summary>
        public readonly byte B;
        /// <summary>
        /// 获取透明度通道，零为透明，二百五十五为不透明。
        /// </summary>
        public readonly byte A;

        /// <summary>
        /// 原样保存通道，省略透明度时使用不透明值。
        /// 直接取得默认结构体不会执行该构造方法，透明度为零。
        /// </summary>
        /// <param name="r">红色通道。</param>
        /// <param name="g">绿色通道。</param>
        /// <param name="b">蓝色通道。</param>
        /// <param name="a">透明度通道，默认为二百五十五。</param>
        public DebugDrawColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// 获取不透明绿色，通道为零、二百五十五、零、二百五十五。
        /// </summary>
        public static DebugDrawColor Green => new DebugDrawColor(0, 255, 0, 255);
        /// <summary>
        /// 获取不透明红色，通道为二百五十五、零、零、二百五十五。
        /// </summary>
        public static DebugDrawColor Red => new DebugDrawColor(255, 0, 0, 255);
        /// <summary>
        /// 获取不透明黄色，通道为二百五十五、二百五十五、零、二百五十五。
        /// </summary>
        public static DebugDrawColor Yellow => new DebugDrawColor(255, 255, 0, 255);
        /// <summary>
        /// 获取不透明青色，通道为零、二百五十五、二百五十五、二百五十五。
        /// </summary>
        public static DebugDrawColor Cyan => new DebugDrawColor(0, 255, 255, 255);
        /// <summary>
        /// 获取不透明白色，四个通道均为二百五十五。
        /// </summary>
        public static DebugDrawColor White => new DebugDrawColor(255, 255, 255, 255);
    }

    /// <summary>
    /// 仅保存颜色的旧绘制样式，不提供线宽、持续时长或深度策略。
    /// 仅兼容至下一主版本，新代码使用 Diagnostics 对应类型。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public readonly struct DebugDrawStyle
    {
        /// <summary>
        /// 获取构造时复制的颜色值。
        /// </summary>
        public readonly DebugDrawColor Color;

        /// <summary>
        /// 复制颜色值，不持有传入变量的引用。
        /// </summary>
        /// <param name="color">绘制颜色。</param>
        public DebugDrawStyle(in DebugDrawColor color)
        {
            Color = color;
        }

        /// <summary>
        /// 获取不透明绿色样式；与透明黑色的默认结构体值不同。
        /// </summary>
        public static DebugDrawStyle Default => new DebugDrawStyle(DebugDrawColor.Green);
    }

    /// <summary>
    /// 仅携带启用分类掩码的旧绘制上下文，不执行过滤或限制绘制数量。
    /// 仅兼容至下一主版本，新代码使用 Diagnostics 对应类型。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public readonly struct DebugDrawContext
    {
        /// <summary>
        /// 获取构造时复制的分类位模式。
        /// </summary>
        public readonly DebugDrawMask EnabledMask;

        /// <summary>
        /// 原样复制分类掩码，不验证未知位或负值。
        /// </summary>
        /// <param name="enabledMask">本轮启用的分类位模式。</param>
        public DebugDrawContext(DebugDrawMask enabledMask)
        {
            EnabledMask = enabledMask;
        }
    }

    /// <summary>
    /// 定义旧绘制请求接口，本身不提供默认绘制器、空绘制器或参数校验。
    /// 单位、坐标空间与退化形状由实现决定；下一主版本前迁往 Diagnostics 对应接口。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public interface IDebugDraw
    {
        /// <summary>
        /// 请求绘制球体线框，具体表示和非法半径处理由实现决定。
        /// </summary>
        /// <param name="center">球心。</param>
        /// <param name="radius">半径。</param>
        /// <param name="style">颜色样式。</param>
        void DrawWireSphere(in Vec3 center, float radius, in DebugDrawStyle style);
        /// <summary>
        /// 请求绘制两端点和半径描述的胶囊线框，具体近似和退化处理由实现决定。
        /// </summary>
        /// <param name="a">第一端点。</param>
        /// <param name="b">第二端点。</param>
        /// <param name="radius">半径。</param>
        /// <param name="style">颜色样式。</param>
        void DrawWireCapsule(in Vec3 a, in Vec3 b, float radius, in DebugDrawStyle style);
        /// <summary>
        /// 请求绘制轴对齐包围盒线框，尺寸解释和非法值处理由实现决定。
        /// </summary>
        /// <param name="center">中心。</param>
        /// <param name="size">尺寸；旧编辑器实现作为完整边长传入 Unity。</param>
        /// <param name="style">颜色样式。</param>
        void DrawWireAabb(in Vec3 center, in Vec3 size, in DebugDrawStyle style);
        /// <summary>
        /// 请求绘制两点间的线段，具体绘制与退化处理由实现决定。
        /// </summary>
        /// <param name="a">起点。</param>
        /// <param name="b">终点。</param>
        /// <param name="style">颜色样式。</param>
        void DrawLine(in Vec3 a, in Vec3 b, in DebugDrawStyle style);
    }

    /// <summary>
    /// 定义向旧驱动提供绘制内容的兼容接口，不自行注册或管理生命周期。
    /// 下一主版本前迁往 Diagnostics 对应接口，由所有者管理登记与注销。
    /// </summary>
    [Obsolete("Debug draw belongs to AbilityKit.Diagnostics; migrate before the next major version.")]
    public interface IDebugDrawContributor
    {
        /// <summary>
        /// 获取绘制分类；旧编辑器驱动在绘制异常隔离范围之外读取该属性。
        /// </summary>
        DebugDrawMask Mask { get; }
        /// <summary>
        /// 同步提交本轮绘制请求，不应假定驱动具备快照、重入或并发隔离。
        /// </summary>
        /// <param name="ctx">本轮捕获的启用掩码。</param>
        /// <param name="draw">驱动提供的绘制器，本接口不转移所有权。</param>
        void Draw(in DebugDrawContext ctx, IDebugDraw draw);
    }
}
