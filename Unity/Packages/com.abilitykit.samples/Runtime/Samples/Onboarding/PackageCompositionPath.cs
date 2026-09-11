#nullable enable

using System.Collections.Generic;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 新手导览：按需组合模块。
    /// </summary>
    [Sample(2, "onboarding", "composition")]
    public sealed class PackageCompositionPath : SampleBase
    {
        public override string Title => "Package Composition Path";
        public override string Description => "用一个玩法需求演示如何挑选 AbilityKit 模块，而不是整包全接";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            var scenarios = new[]
            {
                new FeatureScenario(
                    "只需要技能释放前检查",
                    new[] { "GameplayTags", "Pipeline" },
                    "用标签描述状态，用 Pipeline 串联 PreCheck / Consume / Execute。"),
                new FeatureScenario(
                    "需要持续 Buff 和衰减",
                    new[] { "Modifiers", "Continuous", "Flow" },
                    "Modifier 管属性变化，Continuous/Flow 管持续时间和阶段推进。"),
                new FeatureScenario(
                    "需要副本、房间或服务器战斗",
                    new[] { "Host", "World.DI", "Pipeline", "Triggering" },
                    "World 承载生命周期和服务，Triggering/Pipeline 承载玩法规则。"),
                new FeatureScenario(
                    "需要完整技能最佳实践参考",
                    new[] { "demo.moba.*", "按需复制真实需要的模块" },
                    "moba 示例是参考工程，不是所有项目的必选依赖。")
            };

            Section("按需组合不是少写代码，而是少背不需要的复杂度");
            for (var i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                Numbered(i + 1, scenario.Name);
                KeyValue("推荐模块", string.Join(" + ", scenario.Modules));
                KeyValue("组合理由", scenario.Reason);
                Line();
            }

            Divider();
            Section("新人继续阅读的路线");
            Bullet("Foundation：先看日志、事件、对象池、类型注册这些底座。");
            Bullet("Tags / Config：再看数据如何描述玩法概念。");
            Bullet("Pipeline / Flow：然后看一次性阶段和跨帧流程。");
            Bullet("Triggering / HFSM / Modifiers：最后看规则、状态和属性如何互相驱动。");
            Bullet("World / Demo：把模块放进可运行世界，理解生命周期和集成边界。");
        }

        private sealed class FeatureScenario
        {
            public FeatureScenario(string name, IReadOnlyList<string> modules, string reason)
            {
                Name = name;
                Modules = modules;
                Reason = reason;
            }

            public string Name { get; }
            public IReadOnlyList<string> Modules { get; }
            public string Reason { get; }
        }
    }
}
