using System;
using System.Collections.Generic;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 环境构建原语的抽象基类。框架只定义四种原语形状（<see cref="SpawnPrimitive"/>/<see cref="ObstaclePrimitive"/>/
/// <see cref="TagPrimitive"/>/<see cref="ModifierPrimitive"/>），不内置任何业务枚举——原语类别由 C# 类型本身区分。
/// 所有字段都是不透明 token（实体类别、组件键值、标签、操作），由项目的 binder 解释并执行；框架不认识任何具体实体系统或标签体系。
/// </summary>
public abstract class EnvironmentPrimitive
{
}

/// <summary>生成一个（或一批）实体，并赋予组件/属性数值与初始标签。</summary>
public sealed class SpawnPrimitive : EnvironmentPrimitive
{
    /// <summary>实体类别（不透明 token，如 "jungle_warrior"）。</summary>
    public string EntityKind { get; init; } = string.Empty;

    /// <summary>可选别名，供后续 <see cref="TagPrimitive"/> / <see cref="ModifierPrimitive"/> 引用。</summary>
    public string? Alias { get; init; }

    /// <summary>生成数量。</summary>
    public int Count { get; init; } = 1;

    /// <summary>生成位置（可选，缺省由 binder 决定）。</summary>
    public EnvironmentVector3? Position { get; init; }

    /// <summary>组件/属性数值（key-value，不透明 token）。</summary>
    public IReadOnlyDictionary<string, string> Components { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>初始标签。</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>放置一个静态障碍物（墙体、立柱、可破坏物等）。</summary>
public sealed class ObstaclePrimitive : EnvironmentPrimitive
{
    /// <summary>障碍形状（不透明 token，如 "box" / "sphere"）。</summary>
    public string Shape { get; init; } = "box";

    /// <summary>障碍位置。</summary>
    public EnvironmentVector3 Position { get; init; }

    /// <summary>障碍尺寸。</summary>
    public EnvironmentVector3 Size { get; init; } = new(1f, 1f, 1f);

    /// <summary>障碍相关参数（key-value，不透明 token）。</summary>
    public IReadOnlyDictionary<string, string> Components { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>给某个已生成实体（按别名）挂一个标签。</summary>
public sealed class TagPrimitive : EnvironmentPrimitive
{
    /// <summary>引用某个 <see cref="SpawnPrimitive.Alias"/>。</summary>
    public string TargetAlias { get; init; } = string.Empty;

    /// <summary>标签（不透明 token）。</summary>
    public string Tag { get; init; } = string.Empty;
}

/// <summary>给某个已生成实体（按别名）施加一个修饰/数值覆盖。</summary>
public sealed class ModifierPrimitive : EnvironmentPrimitive
{
    /// <summary>引用某个 <see cref="SpawnPrimitive.Alias"/>。</summary>
    public string TargetAlias { get; init; } = string.Empty;

    /// <summary>操作（不透明 token，如 add / mul / override）。</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>数值（不透明，语义由项目定义）。</summary>
    public string Value { get; init; } = string.Empty;
}
}
