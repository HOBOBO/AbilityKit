#if UNITY_EDITOR
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 战斗流程的断言集（编辑器侧、非测试 DTO）：断言积木累积到这里，塞进 TestScenario.Expectations（opaque）。
    /// 后续判 verdict 时由 runner 映射到 MobaAcceptanceExpectation（测试 DTO，在 UNITY_INCLUDE_TESTS 专属程序集里，编辑器编译引用不到）。
    /// </summary>
    public sealed class MobaBattleFlowAssertions
    {
        public List<MobaTraceAssertion> MustContain { get; } = new List<MobaTraceAssertion>();
        public List<MobaTraceAssertion> MustNotContain { get; } = new List<MobaTraceAssertion>();
        public List<MobaStateAssertion> State { get; } = new List<MobaStateAssertion>();
        public List<MobaContextAssertion> Context { get; } = new List<MobaContextAssertion>();
        public List<MobaRelationshipAssertion> Relationships { get; } = new List<MobaRelationshipAssertion>();
    }

    /// <summary>trace 断言（必须/禁止出现）。</summary>
    public sealed class MobaTraceAssertion
    {
        public string Kind { get; set; } = string.Empty;
        public int ConfigId { get; set; }
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; }
        public int UnderEffectId { get; set; }
    }

    /// <summary>状态断言（如 caster.hp &lt; 500）。</summary>
    public sealed class MobaStateAssertion
    {
        public string Alias { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Comparator { get; set; } = "eq";
        public string? ExpectedValue { get; set; }
    }

    /// <summary>上下文断言。</summary>
    public sealed class MobaContextAssertion
    {
        public string Alias { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Comparator { get; set; } = "eq";
        public string? ExpectedValue { get; set; }
    }

    /// <summary>因果关系断言。</summary>
    public sealed class MobaRelationshipAssertion
    {
        public string ParentKind { get; set; } = string.Empty;
        public int ParentConfigId { get; set; }
        public string ChildKind { get; set; } = string.Empty;
        public int ChildConfigId { get; set; }
    }
}
#endif
