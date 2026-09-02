using System;
using System.Collections.Generic;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 一个具名环境场景：可选的基础 Profile、每个关注点一个取值、自由参数（位置/等级/数值微调），以及显式的构建原语。
/// 这是「场景 Profile」层。
///
/// 两条组合规则：
/// <list type="bullet">
/// <item><description><b>组内互斥</b>——<see cref="Selections"/> 以关注点 id 为键，一个 Profile 天然只能为每个关注点选一个取值。</description></item>
/// <item><description><b>组间叠加</b>——不同关注点各自贡献一个取值，共同构成场景。</description></item>
/// </list>
///
/// <see cref="Primitives"/> 是「未压缩」的显式覆盖：当常用组不足以表达某个具体构造（某只怪 HP=5000、某堵墙的尺寸）
/// 时，直接写原语。常用组与显式原语都是数据，最终由项目的 binder 执行。
/// </summary>
public sealed class EnvironmentProfile
{
    /// <summary>唯一 Profile id，大小写不敏感。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>可选的基础 Profile，其取值/参数/原语会先合并（派生者覆盖）。</summary>
    public string? BaseProfileId { get; init; }

    /// <summary>关注点 id → 取值。取值在解析时对照 Catalog 的关注点取值域做校验。</summary>
    public IReadOnlyDictionary<string, string> Selections { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>自由参数（位置、等级、数值覆盖），原样透传给 binder。</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>显式的构建原语（载体中立，字段为不透明 token）。</summary>
    public IReadOnlyList<EnvironmentPrimitive> Primitives { get; init; } = Array.Empty<EnvironmentPrimitive>();
}
}
