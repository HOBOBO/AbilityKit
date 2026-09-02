using System.Collections.Generic;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 把常用组的一个 (concern, value) 选择展开成一组构建原语。这是「常用组 → 原语」的项目侧映射：
/// 例如 "unit-class: jungle" 展开成几个带 jungle 标签的野怪生成原语。框架保持载体中立，只定义这个边界，
/// 由项目实现具体映射（MOBA 才知道野怪营地的构成）。
/// </summary>
public interface IEnvironmentGroupExpander
{
    /// <summary>尝试把 (concernId, value) 展开为原语列表。返回 false 表示该组合无法展开。</summary>
    bool TryExpand(string concernId, string value, out IReadOnlyList<EnvironmentPrimitive> primitives);
}
}
