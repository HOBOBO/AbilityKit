using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 环境的一个命名正交维度（关注点），连同其闭合取值域。
/// 项目可能声明的关注点示例：<c>unit-class</c>（hero/minion/jungle/summon/neutral）、
/// <c>target-shape</c>（single/group/structure/none）、<c>geometry</c>（open/walled/obstacle/destructible）、
/// <c>state</c>（full/wounded/armored/cc-immune）。
///
/// 这是环境 Profile 机制里的「常用组」层。框架只定义这个形状——一个 id、一个显示名、一组取值——
/// 而不规定任何具体关注点。项目以数据形式声明自己的关注点与取值域；新增关注点只是数据声明，
/// 永远不需要改框架代码。
/// </summary>
public sealed class EnvironmentConcern
{
    /// <summary>唯一、大小写不敏感的关注点 id（例如 "unit-class"）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>可选的人类可读标签。</summary>
    public string? DisplayName { get; init; }

    /// <summary>闭合取值域。取值按大小写不敏感比较。</summary>
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();

    /// <summary>供序列化器使用的无参构造函数。</summary>
    public EnvironmentConcern() { }

    /// <summary>声明一个关注点及其闭合取值域。</summary>
    public EnvironmentConcern(string id, IEnumerable<string> values, string? displayName = null)
    {
        Id = id;
        Values = values?.ToArray() ?? Array.Empty<string>();
        DisplayName = displayName;
    }

    /// <summary>当 <paramref name="value"/> 属于该关注点的取值域时返回 true（大小写不敏感）。</summary>
    public bool ContainsValue(string? value) =>
        value != null && Values.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
}
}
