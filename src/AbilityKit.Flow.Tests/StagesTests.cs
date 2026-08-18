using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Ability.Flow.Stages;
using Xunit;

namespace AbilityKit.Flow.Tests;

public sealed class StagesTests
{
    private sealed class RecordingContributor : IFlowStageContributor<string>
    {
        public readonly string Name;
        public readonly HashSet<string> Stages;
        public readonly List<string> ContributedTo = new List<string>();
        private readonly List<string> _log;

        public RecordingContributor(string name, int order, List<string> log, params string[] stages)
        {
            Name = name;
            Order = order;
            Stages = new HashSet<string>(stages);
            _log = log;
        }

        public int Order { get; }

        public bool CanContribute(FlowStageKey stage) => Stages.Contains(stage.Value);

        public IFlowNode CreateNode(FlowStageKey stage, string args)
        {
            ContributedTo.Add($"{stage}:{args}");
            return new DoNode(onEnter: _ => _log.Add($"{Name}@{stage}"));
        }
    }

    private sealed class CoreProvider : IStagedFlowProvider<string>
    {
        public readonly List<string> CreatedStages = new List<string>();
        private readonly List<string> _log;

        public CoreProvider(List<string> log = null)
        {
            _log = log ?? new List<string>();
        }

        public IFlowNode CreateStage(FlowStageKey stage, string args)
        {
            CreatedStages.Add(stage.Value);
            return new DoNode(onEnter: _ => _log.Add($"core@{stage}"));
        }
    }

    [Fact]
    public void FlowStageKey_equality_and_toString()
    {
        var a = new FlowStageKey("running");
        var b = new FlowStageKey("running");
        var c = new FlowStageKey("exit");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("running", a.ToString());
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)c));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void FlowStageKey_null_value_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FlowStageKey(null));
    }

    [Fact]
    public void FlowStages_default_orders_are_stable()
    {
        Assert.Equal(7, FlowStages.DefaultOrder.Count);
        Assert.Equal(
            new[] { "pre_enter", "enter", "post_enter", "running", "pre_exit", "exit", "post_exit" },
            StringValues(FlowStages.DefaultOrder));
        Assert.Equal(
            new[] { "pre_enter", "enter", "post_enter", "running" },
            StringValues(FlowStages.DefaultTryOrder));
        Assert.Equal(
            new[] { "pre_exit", "exit", "post_exit" },
            StringValues(FlowStages.DefaultFinallyOrder));
    }

    private static string[] StringValues(IReadOnlyList<FlowStageKey> keys)
    {
        var values = new string[keys.Count];
        for (int i = 0; i < keys.Count; i++) values[i] = keys[i].Value;
        return values;
    }

    [Fact]
    public void StagedFlowRootProvider_orders_contributors_then_core_per_stage()
    {
        var late = new RecordingContributor("late", order: 10, log: null, stages: new[] { "enter" });
        var early = new RecordingContributor("early", order: -5, log: null, stages: new[] { "enter", "running" });
        var core = new CoreProvider();

        var provider = new StagedFlowRootProvider<string>(core, new[] { late, early, null });
        provider.CreateRoot("args");

        // CreateRoot 会同时构建 try 与 finally 两组阶段。
        Assert.Equal(
            new[] { "pre_enter", "enter", "post_enter", "running", "pre_exit", "exit", "post_exit" },
            core.CreatedStages);
        // 同一 stage 内：贡献者按 Order 升序，core 节点排在贡献者之后。
        Assert.Equal(new[] { "enter:args", "running:args" }, early.ContributedTo);
        Assert.Equal(new[] { "enter:args" }, late.ContributedTo);
    }

    [Fact]
    public void StagedFlowRootProvider_try_stages_run_in_order_before_finally_stages()
    {
        var log = new List<string>();
        var early = new RecordingContributor("early", order: -5, log: log, stages: new[] { "enter", "running" });
        var core = new CoreProvider(log);
        var provider = new StagedFlowRootProvider<string>(core, new[] { early });
        var root = provider.CreateRoot("");

        var result = root.Execute();

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                "core@pre_enter",
                "early@enter",
                "core@enter",
                "core@post_enter",
                "early@running",
                "core@running",
                // finally 阶段在全部 try 阶段之后执行。
                "core@pre_exit",
                "core@exit",
                "core@post_exit"
            },
            log);
    }

    [Fact]
    public void StagedFlowRootProvider_finally_stages_run_after_try_stages()
    {
        var core = new CoreProvider();
        var provider = new StagedFlowRootProvider<string>(core, Array.Empty<IFlowStageContributor<string>>());
        var root = provider.CreateRoot("");

        var result = root.Execute();

        Assert.True(result.Succeeded);
        // 全部 try 阶段先执行，随后 finally 阶段。
        Assert.Equal(
            new[] { "pre_enter", "enter", "post_enter", "running", "pre_exit", "exit", "post_exit" },
            core.CreatedStages);
    }

    [Fact]
    public void StagedFlowRootProvider_null_contributors_filtered()
    {
        var provider = new StagedFlowRootProvider<string>(
            new CoreProvider(),
            new IFlowStageContributor<string>[] { null, null });

        var root = provider.CreateRoot("");

        Assert.Equal(FlowStatus.Succeeded, root.Execute().Status);
    }

    [Fact]
    public void StagedFlowRootProvider_ctor_requires_core()
    {
        Assert.Throws<ArgumentNullException>(() => new StagedFlowRootProvider<string>(null, null));
    }

    [Fact]
    public void OrderedStagedFlowRootProvider_uses_custom_stage_lists()
    {
        var core = new CoreProvider();
        var provider = new OrderedStagedFlowRootProvider<string>(
            core,
            contributors: null,
            tryStages: new[] { FlowStages.Running },
            finallyStages: new[] { FlowStages.Exit });

        provider.CreateRoot("");

        Assert.Equal(new[] { "running", "exit" }, core.CreatedStages);
    }

    [Fact]
    public void OrderedStagedFlowRootProvider_ctor_validates_arguments()
    {
        var core = new CoreProvider();

        Assert.Throws<ArgumentNullException>(() => new OrderedStagedFlowRootProvider<string>(null, null, null, null));
        Assert.Throws<ArgumentNullException>(() => new OrderedStagedFlowRootProvider<string>(core, null, null, null));
        Assert.Throws<ArgumentNullException>(() => new OrderedStagedFlowRootProvider<string>(core, null, new[] { FlowStages.Running }, null));
    }

    [Fact]
    public void OrderedStagedFlowRootProvider_wraps_in_finally_semantics()
    {
        // try 的 running 阶段失败后，finally(exit) 阶段仍会执行，整体结果为 Failed。
        var log = new List<string>();
        var core = new FailingRunningCore(log);
        var provider = new OrderedStagedFlowRootProvider<string>(
            core,
            contributors: null,
            tryStages: new[] { FlowStages.Running },
            finallyStages: new[] { FlowStages.Exit });

        var result = provider.CreateRoot("").Execute();

        Assert.Equal(FlowStatus.Failed, result.Status);
        Assert.Equal(new[] { "try@running", "finally@exit" }, log);
    }

    private sealed class FailingRunningCore : IStagedFlowProvider<string>
    {
        private readonly List<string> _log;

        public FailingRunningCore(List<string> log) => _log = log;

        public IFlowNode CreateStage(FlowStageKey stage, string args)
        {
            if (stage == FlowStages.Running)
            {
                return new DoNode(onEnter: _ => _log.Add("try@running"), onTick: (_, _) => FlowStatus.Failed);
            }

            return new DoNode(onEnter: _ => _log.Add($"finally@{stage}"));
        }
    }

    [Fact]
    public void StagedFlowRootProvider_empty_stages_fallback_to_DoNode()
    {
        // 没有任何贡献者且 core 每个阶段都返回 null 的极端情形：
        // 通过一个返回 null 的 core 验证空阶段回退路径可用。
        var provider = new StagedFlowRootProvider<string>(new NullCoreProvider(), null);
        var root = provider.CreateRoot("");

        var result = root.Execute();

        Assert.True(result.Succeeded);
    }

    private sealed class NullCoreProvider : IStagedFlowProvider<string>
    {
        public IFlowNode CreateStage(FlowStageKey stage, string args) => null;
    }

    [Fact]
    public void Contributor_returning_null_node_is_skipped()
    {
        var contributor = new NullNodeContributor();
        var provider = new StagedFlowRootProvider<string>(new CoreProvider(), new[] { contributor });

        provider.CreateRoot("");

        Assert.Equal(1, contributor.CreateCount);
    }

    private sealed class NullNodeContributor : IFlowStageContributor<string>
    {
        public int CreateCount;

        public int Order => 0;

        public bool CanContribute(FlowStageKey stage) => stage == FlowStages.PreEnter;

        public IFlowNode CreateNode(FlowStageKey stage, string args)
        {
            CreateCount++;
            return null;
        }
    }

    [Fact]
    public void StagedFlowRootProvider_passes_args_to_contributors_and_core()
    {
        var seenArgs = new List<string>();
        var contributor = new ArgsCaptureContributor(seenArgs);
        var core = new ArgsCaptureCore(seenArgs);
        var provider = new StagedFlowRootProvider<string>(core, new[] { contributor });

        provider.CreateRoot("payload");

        Assert.Contains("contributor:payload", seenArgs);
        Assert.Contains("core:payload", seenArgs);
    }

    private sealed class ArgsCaptureContributor : IFlowStageContributor<string>
    {
        private readonly List<string> _sink;

        public ArgsCaptureContributor(List<string> sink) => _sink = sink;

        public int Order => 0;

        public bool CanContribute(FlowStageKey stage) => stage == FlowStages.Enter;

        public IFlowNode CreateNode(FlowStageKey stage, string args)
        {
            _sink.Add($"contributor:{args}");
            return new DoNode();
        }
    }

    private sealed class ArgsCaptureCore : IStagedFlowProvider<string>
    {
        private readonly List<string> _sink;

        public ArgsCaptureCore(List<string> sink) => _sink = sink;

        public IFlowNode CreateStage(FlowStageKey stage, string args)
        {
            _sink.Add($"core:{args}");
            return new DoNode();
        }
    }
}
