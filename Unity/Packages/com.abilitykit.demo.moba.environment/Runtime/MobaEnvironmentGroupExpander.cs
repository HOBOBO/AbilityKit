using System;
using System.Collections.Generic;
using AbilityKit.EnvironmentModel;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
/// <summary>
/// MOBA 的「常用组 → 原语」展开：把 (concern, value) 映射成构建原语。
/// 实体类别用 <c>MobaEntityKind</c> 的字符串名（"Monster"/"Minion"/"Summon"...），由 moba.runtime 的 binder
/// 负责翻译成真正的实体类别并生成。数量/位置是 starter 占位，后续接真实 MOBA 营地配置替换。
/// <para>target-shape / state 描述的是目标而非环境，交给预览会话的目标装配，这里返回 false。</para>
/// </summary>
public sealed class MobaEnvironmentGroupExpander : IEnvironmentGroupExpander
{
    /// <inheritdoc/>
    public bool TryExpand(string concernId, string value, out IReadOnlyList<EnvironmentPrimitive> primitives)
    {
        if (string.Equals(concernId, MobaEnvironmentConcerns.UnitClass, StringComparison.OrdinalIgnoreCase))
        {
            primitives = ExpandUnitClass(value);
            return true;
        }

        if (string.Equals(concernId, MobaEnvironmentConcerns.Geometry, StringComparison.OrdinalIgnoreCase))
        {
            primitives = ExpandGeometry(value);
            return true;
        }

        // target-shape / state 是目标关注点，不属于环境展开。
        primitives = Array.Empty<EnvironmentPrimitive>();
        return false;
    }

    private static IReadOnlyList<EnvironmentPrimitive> ExpandUnitClass(string value)
    {
        switch (value)
        {
            case "minion":
                return new EnvironmentPrimitive[]
                {
                    new SpawnPrimitive { EntityKind = "Minion", Alias = "minion", Count = 5, Tags = new[] { "minion" } },
                };
            case "jungle":
                return new EnvironmentPrimitive[]
                {
                    new SpawnPrimitive { EntityKind = "Monster", Alias = "jungle", Count = 3, Tags = new[] { "jungle" } },
                };
            case "summon":
                return new EnvironmentPrimitive[]
                {
                    new SpawnPrimitive { EntityKind = "Summon", Alias = "summon", Count = 1 },
                };
            case "neutral":
                return new EnvironmentPrimitive[]
                {
                    new SpawnPrimitive { EntityKind = "Monster", Alias = "neutral", Count = 1, Tags = new[] { "neutral" } },
                };
            default:
                return Array.Empty<EnvironmentPrimitive>();
        }
    }

    private static IReadOnlyList<EnvironmentPrimitive> ExpandGeometry(string value)
    {
        switch (value)
        {
            case "walled":
                return new EnvironmentPrimitive[]
                {
                    new ObstaclePrimitive { Shape = "box", Size = new EnvironmentVector3(10f, 2f, 0.5f) },
                };
            case "obstacle":
                return new EnvironmentPrimitive[]
                {
                    new ObstaclePrimitive { Shape = "box", Size = new EnvironmentVector3(2f, 2f, 2f) },
                };
            default:
                return Array.Empty<EnvironmentPrimitive>();
        }
    }
}
}
