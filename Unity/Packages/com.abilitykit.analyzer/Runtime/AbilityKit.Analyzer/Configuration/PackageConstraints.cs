/// <summary>
/// 文件名称: PackageConstraints.cs
/// 
/// 功能描述: 定义包约束配置的数据模型，用于描述每个包的命名空间和程序集禁止引用规则。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;

namespace AbilityKit.Analyzer.Configuration
{

/// <summary>
/// 单个包的命名空间约束配置。
/// </summary>
[Serializable]
public sealed class PackageConstraint
{
    /// <summary>包的 Assembly Definition 名称（不含 .asmdef 后缀）</summary>
    public string PackageName { get; set; }

    /// <summary>禁止引用的命名空间前缀列表（前缀匹配，如 "UnityEngine" 会匹配 "UnityEngine.UI"）</summary>
    public List<string> ForbiddenNamespaces { get; set; } = new();

    /// <summary>禁止引用的程序集名称列表（精确匹配，如 "UnityEngine"）</summary>
    public List<string> ForbiddenAssemblies { get; set; } = new();

    /// <summary>此包是否启用约束检查（可通过配置覆盖单包开关）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>约束违反时的严重级别</summary>
    public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

    /// <summary>是否同时检查 using 别名（using Foo = UnityEngine.Bar;）</summary>
    public bool CheckUsingAliases { get; set; } = true;

    /// <summary>约束的描述说明（用于生成诊断消息）</summary>
    public string Description { get; set; }

    /// <summary>
    /// 检查给定命名空间是否违反约束。
    /// </summary>
    /// <param name="namespace">要检查的命名空间</param>
    /// <returns>如果违反约束则返回 true</returns>
    public bool IsNamespaceForbidden(string @namespace)
    {
        if (string.IsNullOrEmpty(@namespace) || !IsEnabled)
            return false;

        foreach (var forbidden in ForbiddenNamespaces)
        {
            if (@namespace == forbidden || @namespace.StartsWith(forbidden + "."))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 检查给定程序集名称是否违反约束。
    /// </summary>
    /// <param name="assemblyName">要检查的程序集名称</param>
    /// <returns>如果违反约束则返回 true</returns>
    public bool IsAssemblyForbidden(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName) || !IsEnabled)
            return false;

        foreach (var forbidden in ForbiddenAssemblies)
        {
            if (assemblyName == forbidden || assemblyName.StartsWith(forbidden))
                return true;
        }
        return false;
    }
}

/// <summary>
/// 完整的包约束配置文件模型。
/// </summary>
[Serializable]
public sealed class PackageConstraintsConfig
{
    /// <summary>所有包的约束规则，key 为包名（支持通配符如 "AbilityKit.Demo.*"）</summary>
    public Dictionary<string, PackageConstraint> Constraints { get; set; } = new();

    /// <summary>全局默认设置，适用于所有未单独配置的包</summary>
    public GlobalConstraintDefaults GlobalDefaults { get; set; } = new();

    /// <summary>
    /// 获取指定包名的约束配置，支持通配符匹配。
    /// </summary>
    /// <param name="packageName">包名</param>
    /// <returns>匹配的约束配置，如果无匹配则返回全局默认</returns>
    public PackageConstraint GetConstraint(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return null;

        if (Constraints.TryGetValue(packageName, out var constraint))
            return constraint;

        // 通配符匹配（支持 "AbilityKit.Demo.*" 形式）
        foreach (var key in Constraints.Keys)
        {
            if (key.EndsWith(".*") && packageName.StartsWith(key.TrimEnd('*')))
            {
                return Constraints[key];
            }
        }

        return null;
    }

    /// <summary>
    /// 获取应用到指定包的实际约束（合并全局默认）。
    /// </summary>
    /// <param name="packageName">包名</param>
    /// <returns>合并后的约束（如果包无单独配置且 ApplyToUnlistedPackages=false 则返回 null）</returns>
    public PackageConstraint GetEffectiveConstraint(string packageName)
    {
        var constraint = GetConstraint(packageName);
        if (constraint != null)
            return constraint;

        // 如果包没有单独配置，且不允许对未配置的包应用全局规则，则返回 null
        if (!GlobalDefaults.ApplyToUnlistedPackages)
            return null;

        if (!GlobalDefaults.Enabled)
            return null;

        return new PackageConstraint
        {
            PackageName = packageName,
            ForbiddenNamespaces = GlobalDefaults.ForbiddenNamespaces,
            ForbiddenAssemblies = GlobalDefaults.ForbiddenAssemblies,
            IsEnabled = GlobalDefaults.Enabled,
            Severity = GlobalDefaults.Severity,
            CheckUsingAliases = GlobalDefaults.CheckUsingAliases
        };
    }
}

/// <summary>
/// 全局默认约束设置。
/// </summary>
[Serializable]
public sealed class GlobalConstraintDefaults
{
    /// <summary>是否对所有包启用约束检查（除非包单独配置为 false）</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>全局默认禁止的命名空间列表</summary>
    public List<string> ForbiddenNamespaces { get; set; } = new();

    /// <summary>全局默认禁止的程序集列表</summary>
    public List<string> ForbiddenAssemblies { get; set; } = new();

    /// <summary>全局默认严重级别</summary>
    public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

    /// <summary>是否默认检查 using 别名</summary>
    public bool CheckUsingAliases { get; set; } = true;

    /// <summary>
    /// 是否对未明确配置的包也应用全局默认规则。
    /// 默认为 false，表示只有明确配置的包才会应用规则。
    /// </summary>
    public bool ApplyToUnlistedPackages { get; set; } = false;
}

}
