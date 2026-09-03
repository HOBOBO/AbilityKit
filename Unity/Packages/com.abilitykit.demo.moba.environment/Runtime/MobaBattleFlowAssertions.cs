using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    /// <summary>
    /// MOBA 战斗流程的断言集（纯 C#、编辑器与 .NET runner 共用）：断言积木累积到这里，塞进 TestScenario.Expectations（opaque）。
    /// runner 判 verdict 时把它映射到 MobaAcceptanceExpectation（测试 DTO）。
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
