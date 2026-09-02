using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 环境绑定的结果：构造完成后，别名（<see cref="SpawnPrimitive.Alias"/>）→ 项目实体的 handle。
/// <typeparamref name="THandle"/> 是项目的实体 handle 类型（实体 id / 实体引用 / 接口），框架对其无任何约束、只透传。
/// 预览/测试会话据此拿到 caster / target 等实体，再施放技能并观测——这是「环境描述」与「预览/测试执行」之间的接缝。
/// </summary>
public sealed class EnvironmentBindResult<THandle>
{
    /// <summary>空结果（未绑定任何实体）。</summary>
    public EnvironmentBindResult() { }

    /// <summary>以给定的别名 → handle 映射构造绑定结果。</summary>
    public EnvironmentBindResult(IReadOnlyDictionary<string, THandle> handles)
    {
        Handles = handles ?? throw new ArgumentNullException(nameof(handles));
    }

    /// <summary>别名 → handle。键大小写不敏感。</summary>
    public IReadOnlyDictionary<string, THandle> Handles { get; init; } =
        new Dictionary<string, THandle>(StringComparer.OrdinalIgnoreCase);

    /// <summary>按别名取 handle。未命中时返回 false，<paramref name="handle"/> 为 default。</summary>
    public bool TryGetHandle(string alias, [MaybeNullWhen(false)] out THandle handle) => Handles.TryGetValue(alias, out handle);
}
}
