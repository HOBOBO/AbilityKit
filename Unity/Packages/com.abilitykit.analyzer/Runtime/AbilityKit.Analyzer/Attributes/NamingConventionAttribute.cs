/// <summary>
/// 文件名称: NamingConventionAttribute.cs
/// 
/// 功能描述: 定义命名规范检查的特性，用于标记类型、成员并指定期望的命名风格。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

namespace AbilityKit.Analyzer.Attributes
{
/// <summary>
/// 指定代码元素期望的命名规范。
/// </summary>
[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Interface |
    AttributeTargets.Method |
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Event,
    AllowMultiple = false,
    Inherited = false)]
public sealed class NamingConventionAttribute : Attribute
{
    /// <summary>期望的命名约定类型</summary>
    public NamingConventionKind Convention { get; }

    /// <summary>期望的前缀（如接口的 "I"）</summary>
    public string Prefix { get; set; }

    /// <summary>期望的后缀（如 SubFeature 的 "SubFeature"）</summary>
    public string Suffix { get; set; }

    /// <summary>是否检查完整名称包括前后缀</summary>
    public bool CheckFullName { get; set; } = true;

    public NamingConventionAttribute(NamingConventionKind convention)
    {
        Convention = convention;
    }
}

/// <summary>
/// 命名约定的标准类型枚举。
/// </summary>
public enum NamingConventionKind
{
    /// <summary>PascalCase（首字母大写）</summary>
    PascalCase,

    /// <summary>camelCase（首字母小写）</summary>
    CamelCase,

    /// <summary>SCREAMING_SNAKE_CASE（全大写加下划线）</summary>
    ScreamingSnakeCase,

    /// <summary>SNAKE_CASE（全小写加下划线）</summary>
    SnakeCase,

    /// <summary>匈牙利命名法（不推荐）</summary>
    Hungarian,

    /// <summary>接口命名（必须以 I 开头）</summary>
    Interface,

    /// <summary>抽象类命名（必须以 Abstract 或 Base 开头）</summary>
    Abstract,

    /// <summary>事件处理器命名（必须以 Event 或 Handler 结尾）</summary>
    EventHandler,

    /// <summary>异常类命名（必须以 Exception 或 Error 结尾）</summary>
    Exception,

    /// <summary>特性类命名（必须以 Attribute 结尾）</summary>
    Attribute,

    /// <summary>SubFeature 命名（必须以 SubFeature 结尾）</summary>
    SubFeature
}
}