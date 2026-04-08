/// <summary>
/// 文件名称: GenerateCodeAttribute.cs
/// 
/// 功能描述: 定义用于标记类型触发代码生成的特性。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;

namespace AbilityKit.CodeGen.Attributes
{
/// <summary>
/// 标记类型以触发自动代码生成。
/// </summary>
[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Interface |
    AttributeTargets.Enum,
    AllowMultiple = false,
    Inherited = false)]
public sealed class GenerateCodeAttribute : Attribute
{
    /// <summary>要生成的代码类型标识</summary>
    public string Kind { get; set; }

    /// <summary>生成代码的命名空间覆盖</summary>
    public string Namespace { get; set; }

    /// <summary>是否将目标类型生成为分部类</summary>
    public bool GeneratePartial { get; set; } = true;

    /// <summary>额外的生成选项键值对</summary>
    public Dictionary<string, string> Options { get; set; } = new();

    public GenerateCodeAttribute() { }

    public GenerateCodeAttribute(string kind)
    {
        Kind = kind;
    }
}
}