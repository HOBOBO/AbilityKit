using System;
using System.Collections.Generic;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 把 <see cref="EnvironmentProfile"/> 针对 <see cref="EnvironmentProfileCatalog"/> 解析后得到的完整合并结果：
/// 基础 Profile 已合并、本 Profile 的取值/参数/原语已叠加，常用组（当提供 expander 时）已展开成原语。
/// 它是扁平的、不含未解析引用，binder 无需了解继承或 Catalog 即可消费。
/// </summary>
public sealed class ResolvedEnvironmentProfile
{
    /// <summary>被解析的 Profile id。</summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>合并后的关注点 → 取值（基础已合并、派生已覆盖）。</summary>
    public IReadOnlyDictionary<string, string> Selections { get; init; } =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>合并后的自由参数（基础已合并、派生已覆盖）。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>解析后的扁平构建原语：显式原语（基础→派生）在前，展开后的常用组原语在后。</summary>
    public IReadOnlyList<EnvironmentPrimitive> Primitives { get; init; } = Array.Empty<EnvironmentPrimitive>();
}
}
