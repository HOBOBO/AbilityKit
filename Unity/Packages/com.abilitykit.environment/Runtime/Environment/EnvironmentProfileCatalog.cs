using System;
using System.Collections.Generic;

namespace AbilityKit.EnvironmentModel
{
/// <summary>
/// 环境 Profile 机制的注册表与扩展协议。项目通过 <see cref="AddConcern"/> 与 <see cref="AddProfile"/>
/// 声明自己的 taxonomy——关注点（含取值域）与具名 Profile。这种声明是数据、不是框架代码：框架从不硬编码任何关注点、取值或场景。
///
/// <para>注册表负责校验整个 Catalog（未知关注点、越界取值、悬空基础 Profile、基础环、非法原语），
/// 并把一个 Profile id 解析成扁平的 <see cref="ResolvedEnvironmentProfile"/>（可选地经 expander 把常用组展开成原语）。</para>
/// </summary>
public sealed class EnvironmentProfileCatalog
{
    private readonly Dictionary<string, EnvironmentConcern> _concerns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnvironmentProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>声明一个关注点（项目 taxonomy 的一个条目）。重复 id 会被拒绝。</summary>
    public EnvironmentProfileCatalog AddConcern(EnvironmentConcern concern)
    {
        if (concern is null) throw new ArgumentNullException(nameof(concern));
        if (string.IsNullOrWhiteSpace(concern.Id))
            throw new ArgumentException("Concern id is required.", nameof(concern));
        if (_concerns.ContainsKey(concern.Id))
            throw new ArgumentException($"Concern '{concern.Id}' is already registered.", nameof(concern));
        _concerns[concern.Id] = concern;
        return this;
    }

    /// <summary>声明一个具名场景 Profile。重复 id 会被拒绝。</summary>
    public EnvironmentProfileCatalog AddProfile(EnvironmentProfile profile)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Id))
            throw new ArgumentException("Profile id is required.", nameof(profile));
        if (_profiles.ContainsKey(profile.Id))
            throw new ArgumentException($"Profile '{profile.Id}' is already registered.", nameof(profile));
        _profiles[profile.Id] = profile;
        return this;
    }

    /// <summary>按 id 查找已声明的关注点。</summary>
    public bool TryGetConcern(string concernId, out EnvironmentConcern concern) =>
        _concerns.TryGetValue(concernId, out concern!);

    /// <summary>等价于 <see cref="TryResolve(string, IEnvironmentGroupExpander?, out ResolvedEnvironmentProfile)"/> 且不展开常用组。</summary>
    public bool TryResolve(string profileId, out ResolvedEnvironmentProfile resolved) =>
        TryResolve(profileId, null, out resolved);

    /// <summary>
    /// 把一个 Profile id 解析成扁平、完整的合并结果。基础 Profile 深度优先合并，再由派生取值/参数/原语覆盖；
    /// 提供 expander 时，扁平合并后的常用组选择会按序展开成原语并追加到原语列表之后。
    /// 当 id 未知或基础链缺失/成环时返回 false。
    /// </summary>
    public bool TryResolve(string profileId, IEnvironmentGroupExpander? expander, out ResolvedEnvironmentProfile resolved)
    {
        resolved = null!;
        if (!_profiles.TryGetValue(profileId, out var profile))
            return false;

        var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var primitives = new List<EnvironmentPrimitive>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Merge(profile, selections, parameters, primitives, visiting))
            return false;

        if (expander != null)
        {
            foreach (var selection in selections)
                if (expander.TryExpand(selection.Key, selection.Value, out var expanded))
                    primitives.AddRange(expanded);
        }

        resolved = new ResolvedEnvironmentProfile
        {
            ProfileId = profileId,
            Selections = selections,
            Parameters = parameters,
            Primitives = primitives,
        };
        return true;
    }

    /// <summary>校验整个 Catalog，返回人类可读的错误列表（为空即合法）。</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        foreach (var concern in _concerns.Values)
            ValidateConcern(concern, errors);

        foreach (var profile in _profiles.Values)
            ValidateProfile(profile, errors);

        foreach (var profile in _profiles.Values)
        {
            if (HasCycle(profile, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                errors.Add($"profile '{profile.Id}' participates in a base-profile cycle");
                break;
            }
        }

        return errors;
    }

    /// <summary>当 <see cref="Validate"/> 报告任何错误时抛出异常。</summary>
    public void ThrowIfInvalid()
    {
        var errors = Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid environment profile catalog: " + string.Join("; ", errors));
    }

    private static void ValidateConcern(EnvironmentConcern concern, List<string> errors)
    {
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in concern.Values ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"concern '{concern.Id}' has an empty value");
                continue;
            }
            if (!distinct.Add(value))
                errors.Add($"concern '{concern.Id}' has duplicate value '{value}'");
        }
        if (distinct.Count == 0)
            errors.Add($"concern '{concern.Id}' has an empty value domain");
    }

    private void ValidateProfile(EnvironmentProfile profile, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(profile.BaseProfileId) && !_profiles.ContainsKey(profile.BaseProfileId))
            errors.Add($"profile '{profile.Id}' base '{profile.BaseProfileId}' was not found");

        foreach (var selection in profile.Selections)
        {
            if (!_concerns.TryGetValue(selection.Key, out var concern))
            {
                errors.Add($"profile '{profile.Id}' selects unknown concern '{selection.Key}'");
                continue;
            }
            if (!concern.ContainsValue(selection.Value))
                errors.Add($"profile '{profile.Id}' selects value '{selection.Value}' outside concern '{selection.Key}' domain [{string.Join(",", concern.Values)}]");
        }

        ValidatePrimitives(profile.Id, profile.Primitives, errors);
    }

    private static void ValidatePrimitives(string profileId, IEnumerable<EnvironmentPrimitive>? primitives, List<string> errors)
    {
        if (primitives == null) return;
        var index = 0;
        foreach (var primitive in primitives)
        {
            index++;
            if (primitive == null)
            {
                errors.Add($"profile '{profileId}' primitive[{index}] is null");
                continue;
            }

            switch (primitive)
            {
                case SpawnPrimitive spawn:
                    if (string.IsNullOrWhiteSpace(spawn.EntityKind))
                        errors.Add($"profile '{profileId}' primitive[{index}] spawn entityKind is required");
                    if (spawn.Count < 1)
                        errors.Add($"profile '{profileId}' primitive[{index}] spawn count must be >= 1");
                    break;
                case ObstaclePrimitive obstacle:
                    if (string.IsNullOrWhiteSpace(obstacle.Shape))
                        errors.Add($"profile '{profileId}' primitive[{index}] obstacle shape is required");
                    if (obstacle.Size.X < 0 || obstacle.Size.Y < 0 || obstacle.Size.Z < 0)
                        errors.Add($"profile '{profileId}' primitive[{index}] obstacle size must be non-negative");
                    break;
                case TagPrimitive tag:
                    if (string.IsNullOrWhiteSpace(tag.TargetAlias))
                        errors.Add($"profile '{profileId}' primitive[{index}] tag targetAlias is required");
                    if (string.IsNullOrWhiteSpace(tag.Tag))
                        errors.Add($"profile '{profileId}' primitive[{index}] tag value is required");
                    break;
                case ModifierPrimitive modifier:
                    if (string.IsNullOrWhiteSpace(modifier.TargetAlias))
                        errors.Add($"profile '{profileId}' primitive[{index}] modifier targetAlias is required");
                    if (string.IsNullOrWhiteSpace(modifier.Operation))
                        errors.Add($"profile '{profileId}' primitive[{index}] modifier operation is required");
                    break;
            }
        }
    }

    private bool Merge(
        EnvironmentProfile profile,
        Dictionary<string, string> selections,
        Dictionary<string, string> parameters,
        List<EnvironmentPrimitive> primitives,
        HashSet<string> visiting)
    {
        if (!visiting.Add(profile.Id))
            return false;

        if (!string.IsNullOrWhiteSpace(profile.BaseProfileId))
        {
            if (!_profiles.TryGetValue(profile.BaseProfileId, out var baseProfile))
                return false;
            if (!Merge(baseProfile, selections, parameters, primitives, visiting))
                return false;
        }

        foreach (var kv in profile.Selections)
            selections[kv.Key] = kv.Value;
        foreach (var kv in profile.Parameters)
            parameters[kv.Key] = kv.Value;
        foreach (var primitive in profile.Primitives)
            primitives.Add(primitive);

        visiting.Remove(profile.Id);
        return true;
    }

    private bool HasCycle(EnvironmentProfile profile, HashSet<string> visiting)
    {
        if (!visiting.Add(profile.Id))
            return true;
        if (!string.IsNullOrWhiteSpace(profile.BaseProfileId)
            && _profiles.TryGetValue(profile.BaseProfileId, out var baseProfile)
            && HasCycle(baseProfile, visiting))
            return true;
        visiting.Remove(profile.Id);
        return false;
    }
}
}
