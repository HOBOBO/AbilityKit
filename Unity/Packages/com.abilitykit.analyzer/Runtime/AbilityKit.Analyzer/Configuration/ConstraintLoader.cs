/// <summary>
/// 文件名称: ConstraintLoader.cs
/// 
/// 功能描述: 包约束配置加载器，负责从 JSON 文件加载约束配置并提供查询接口。
/// 支持从多个位置加载配置、缓存、以及从 asmdef 内嵌字段合并配置。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Analyzer.Configuration
{

/// <summary>
/// 包约束配置加载器，提供配置的加载、缓存和查询功能。
/// </summary>
public sealed class ConstraintLoader
{
    private PackageConstraintsConfig _config;
    private readonly string _configPath;
    private readonly Dictionary<string, PackageConstraint> _constraintCache = new Dictionary<string, PackageConstraint>();
    private bool _isLoaded;

    /// <summary>默认配置文件路径（相对于 Assets 目录）</summary>
    public const string DefaultConfigPath = "Config/PackageConstraints.json";

    /// <summary>
    /// 配置文件的标准搜索路径列表，按优先级从高到低。
    /// </summary>
    public static readonly string[] SearchPaths = new[]
    {
        "Assets/Config/PackageConstraints.json",
        "Packages/com.abilitykit.analyzer/Config/PackageConstraints.json",
        "Packages/com.abilitykit.core/Config/PackageConstraints.json"
    };

    /// <summary>
    /// 使用默认搜索路径创建加载器。
    /// </summary>
    public ConstraintLoader()
    {
        _configPath = ResolveConfigPath();
    }

    /// <summary>
    /// 使用指定配置文件路径创建加载器。
    /// </summary>
    /// <param name="configPath">配置文件路径（绝对路径或相对于 Assets 的路径）</param>
    public ConstraintLoader(string configPath)
    {
        _configPath = configPath;
    }

    /// <summary>
    /// 从配置的搜索路径中解析出实际存在的配置文件路径。
    /// </summary>
    /// <returns>找到的配置文件路径，未找到则返回 null</returns>
    public static string ResolveConfigPath()
    {
        foreach (var path in SearchPaths)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    /// <summary>
    /// 加载并解析配置文件。
    /// </summary>
    /// <returns>解析后的配置对象</returns>
    /// <exception cref="FileNotFoundException">配置文件不存在</exception>
    public PackageConstraintsConfig Load()
    {
        if (_isLoaded && _config != null)
            return _config;

        if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath))
        {
            _config = CreateDefaultConfig();
            _isLoaded = true;
            return _config;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            _config = JsonConvert.DeserializeObject<PackageConstraintsConfig>(json);
            BuildCache();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                $"[ConstraintLoader] Failed to load config from '{_configPath}': {ex.Message}. Using default config.");
            _config = CreateDefaultConfig();
        }

        _isLoaded = true;
        return _config;
    }

    /// <summary>
    /// 重新加载配置文件（清除缓存）。
    /// </summary>
    public void Reload()
    {
        _isLoaded = false;
        _constraintCache.Clear();
        Load();
    }

    /// <summary>
    /// 获取指定包的约束配置。
    /// </summary>
    /// <param name="packageName">包名（asmdef 名称）</param>
    /// <returns>该包的约束配置，如果无配置则返回 null</returns>
    public PackageConstraint GetConstraint(string packageName)
    {
        if (!_isLoaded)
            Load();

        if (_constraintCache.TryGetValue(packageName, out var cached))
            return cached;

        var constraint = _config.GetEffectiveConstraint(packageName);
        if (constraint != null)
            _constraintCache[packageName] = constraint;

        return constraint;
    }

    /// <summary>
    /// 检查指定包是否启用了约束检查。
    /// </summary>
    /// <param name="packageName">包名</param>
    /// <returns>如果启用约束检查则返回 true</returns>
    public bool IsConstraintEnabled(string packageName)
    {
        var constraint = GetConstraint(packageName);
        return constraint != null && constraint.IsEnabled;
    }

    /// <summary>
    /// 检查指定命名空间是否在指定包中被禁止。
    /// </summary>
    /// <param name="packageName">包名</param>
    /// <param name="namespace">要检查的命名空间</param>
    /// <returns>如果禁止则返回 true</returns>
    public bool IsNamespaceForbidden(string packageName, string @namespace)
    {
        var constraint = GetConstraint(packageName);
        return constraint != null && constraint.IsNamespaceForbidden(@namespace);
    }

    /// <summary>
    /// 从 asmdef JSON 内容中提取内嵌的约束配置并合并到全局配置中。
    /// </summary>
    /// <param name="asmdefJson">asmdef 文件的 JSON 内容</param>
    /// <param name="packageName">包名（asmdef 的 name 字段）</param>
    /// <returns>合并后的约束配置</returns>
    public PackageConstraint MergeAsmdefConstraints(string asmdefJson, string packageName)
    {
        if (string.IsNullOrEmpty(asmdefJson) || string.IsNullOrEmpty(packageName))
            return GetConstraint(packageName);

        try
        {
            var root = JObject.Parse(asmdefJson);

            if (!root.TryGetValue("forbiddenNamespaces", out var nsToken))
                return GetConstraint(packageName);

            var existingConstraint = GetConstraint(packageName);
            var merged = existingConstraint ?? new PackageConstraint { PackageName = packageName };

            if (nsToken is JArray nsArray)
            {
                foreach (var item in nsArray)
                {
                    if (item.Type == JTokenType.String)
                    {
                        var ns = item.Value<string>();
                        if (!string.IsNullOrEmpty(ns) && !merged.ForbiddenNamespaces.Contains(ns))
                            merged.ForbiddenNamespaces.Add(ns);
                    }
                }
            }

            if (root.TryGetValue("namespaceConstraintSeverity", out var sevToken))
            {
                var sevStr = sevToken.Value<string>();
                if (!string.IsNullOrEmpty(sevStr) && Enum.TryParse<AKDiagnosticSeverity>(sevStr, true, out var severity))
                    merged.Severity = severity;
            }

            if (root.TryGetValue("namespaceConstraintEnabled", out var enabledToken))
            {
                if (enabledToken.Type == JTokenType.Boolean)
                    merged.IsEnabled = enabledToken.Value<bool>();
            }

            _constraintCache[packageName] = merged;
            return merged;
        }
        catch
        {
            return GetConstraint(packageName);
        }
    }

    /// <summary>
    /// 获取当前配置文件路径。
    /// </summary>
    public string ConfigPath => _configPath;

    /// <summary>
    /// 创建默认的空配置。
    /// </summary>
    private static PackageConstraintsConfig CreateDefaultConfig()
    {
        return new PackageConstraintsConfig();
    }

    /// <summary>
    /// 从配置构建缓存以加速查询。
    /// </summary>
    private void BuildCache()
    {
        _constraintCache.Clear();
        if (_config?.Constraints == null)
            return;

        foreach (var kvp in _config.Constraints)
        {
            _constraintCache[kvp.Key] = kvp.Value;
        }
    }
}

/// <summary>
/// 约束配置验证工具。
/// </summary>
public static class ConstraintValidator
{
    /// <summary>
    /// 验证配置是否有效。
    /// </summary>
    /// <param name="config">要验证的配置</param>
    /// <returns>验证错误列表，无错误则返回空列表</returns>
    public static List<string> Validate(PackageConstraintsConfig config)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("Configuration is null.");
            return errors;
        }

        if (config.Constraints != null)
        {
            foreach (var kvp in config.Constraints)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    errors.Add("Found constraint with empty or null package name.");

                if (kvp.Value != null)
                {
                    if (kvp.Value.ForbiddenNamespaces != null)
                    {
                        foreach (var ns in kvp.Value.ForbiddenNamespaces)
                        {
                            if (string.IsNullOrWhiteSpace(ns))
                                errors.Add($"Package '{kvp.Key}' has empty namespace entry in ForbiddenNamespaces.");
                        }
                    }
                }
            }
        }

        return errors;
    }
}

}
