/// <summary>
/// 文件名称: RegisterGeneratorAttribute.cs
/// 
/// 功能描述: 定义注册生成器实现的特性，用于标记生成器类并指定触发特性类型。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

namespace AbilityKit.CodeGen.Attributes
{
/// <summary>
/// 注册生成器实现的特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RegisterGeneratorAttribute : Attribute
{
    /// <summary>触发此生成器的特性类型</summary>
    public Type TriggerAttribute { get; }

    /// <summary>生成器优先级（越高越先执行）</summary>
    public int Priority { get; set; }

    /// <summary>生成器是否默认启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>生成器的描述</summary>
    public string Description { get; set; }

    public RegisterGeneratorAttribute(Type triggerAttribute)
    {
        if (triggerAttribute == null)
            throw new ArgumentNullException(nameof(triggerAttribute));

        TriggerAttribute = triggerAttribute;
    }
}
}