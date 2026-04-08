/// <summary>
/// 文件名称: NamespaceConstraintDescriptors.cs
/// 
/// 功能描述: 定义命名空间约束相关诊断规则的描述器，包含 AK1001（禁止命名空间引用）
/// 和 AK1002（禁止程序集引用）两个诊断规则。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

namespace AbilityKit.Analyzer.Rules
{

/// <summary>
/// 命名空间约束规则的诊断描述符常量。
/// </summary>
public static class NamespaceConstraintDescriptors
{
    /// <summary>
    /// AK1001: 检测到禁止的命名空间引用。
    /// 消息格式: "Namespace '{0}' is forbidden in assembly '{1}'. Package '{2}' restricts references to: {3}"
    /// </summary>
    public static readonly AKDiagnosticDescriptor AK1001_ForbiddenNamespaceReference =
        new(
            id: "AK1001",
            title: "Forbidden namespace reference",
            description: "检测到禁止的命名空间引用。该包配置中禁止引用此命名空间。",
            messageFormat: "Namespace '{0}' is forbidden in assembly '{1}'. " +
                           "Package '{2}' restricts references to: {3}",
            category: AKDiagnosticCategory.Framework,
            defaultSeverity: AKDiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLink: "https://docs.abilitykit.io/analyzer/ak1001");

    /// <summary>
    /// AK1002: 检测到禁止的程序集引用。
    /// 消息格式: "Assembly '{0}' is forbidden in assembly '{1}'. Package '{2}' forbids direct assembly reference."
    /// </summary>
    public static readonly AKDiagnosticDescriptor AK1002_ForbiddenAssemblyReference =
        new(
            id: "AK1002",
            title: "Forbidden assembly reference",
            description: "检测到禁止的程序集引用。该包配置中禁止引用此程序集。",
            messageFormat: "Assembly '{0}' is forbidden in assembly '{1}'. " +
                           "Package '{2}' forbids direct assembly references.",
            category: AKDiagnosticCategory.Framework,
            defaultSeverity: AKDiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLink: "https://docs.abilitykit.io/analyzer/ak1002");

    /// <summary>
    /// AK1003: 约束配置中指定的包名与当前编译的 asmdef 名称不匹配。
    /// 消息格式: "Constraint package name '{0}' does not match any loaded asmdef in compilation."
    /// </summary>
    public static readonly AKDiagnosticDescriptor AK1003_UnmatchedConstraintPackage =
        new(
            id: "AK1003",
            title: "Unmatched constraint package name",
            description: "约束配置中指定的包名在当前编译中找不到对应的 asmdef。",
            messageFormat: "Constraint package name '{0}' does not match any asmdef in compilation. " +
                           "Check the PackageConstraints.json configuration.",
            category: AKDiagnosticCategory.Maintainability,
            defaultSeverity: AKDiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            helpLink: "https://docs.abilitykit.io/analyzer/ak1003");
}

}
