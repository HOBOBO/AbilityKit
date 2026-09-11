#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 新手导览：理解 AbilityKit 的项目定位。
    /// </summary>
    [Sample(0, "onboarding", "learning-path")]
    public sealed class AbilityKitOrientation : SampleBase
    {
        public override string Title => "AbilityKit Orientation";
        public override string Description => "从工具集合、按需组合、纯逻辑优先三个角度理解当前项目";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            Log("AbilityKit 不是一个必须整包接入的单体框架。");
            Log("它更接近一组玩法逻辑工具包：项目需要什么，就组合什么。");
            Divider();

            Section("它主要解决什么问题");
            Numbered(1, "把技能、触发、标签、流程、状态机、World 等玩法逻辑拆成可复用模块。");
            Numbered(2, "让核心逻辑先在纯 C# 环境里验证，再交给 Unity、MonoGame 或服务器宿主驱动。");
            Numbered(3, "用配置、注册表、上下文和执行器，把数据和运行时行为解耦。");

            Divider();
            Section("读 sample 的推荐姿势");
            Bullet("先运行 Onboarding，建立整体心智模型。");
            Bullet("再看 Foundation / Tags / Config，理解基础词汇和数据入口。");
            Bullet("然后看 Pipeline / Flow / HFSM / Triggering，理解行为编排。");
            Bullet("最后看 World 和 Demo，理解模块如何落到完整运行环境。");

            Divider();
            Section("一个最小玩法需求的拆分");
            var request = new GameplayRequest("Cast.Fireball", "玩家释放火球术", needsTarget: true, needsTimeline: true);
            KeyValue("需求", request.Description);
            KeyValue("标签", request.Name);
            KeyValue("是否需要目标", request.NeedsTarget ? "是，进入 Targeting / Tags" : "否");
            KeyValue("是否需要时间过程", request.NeedsTimeline ? "是，进入 Flow / Pipeline" : "否");
            Log("sample 后续会不断把这类需求拆成更小的工具组合。");
        }

        private readonly struct GameplayRequest
        {
            public GameplayRequest(string name, string description, bool needsTarget, bool needsTimeline)
            {
                Name = name;
                Description = description;
                NeedsTarget = needsTarget;
                NeedsTimeline = needsTimeline;
            }

            public string Name { get; }
            public string Description { get; }
            public bool NeedsTarget { get; }
            public bool NeedsTimeline { get; }
        }
    }
}
