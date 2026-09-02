using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>
    /// 测试用脚本节点：行为完全由黑板驱动（resultKey/condKey），无静态可变状态，
    /// 兼容 xunit 并行。定义在测试程序集中，同时验证包外扩展（ScanAssembly + 属性 schema）。
    /// </summary>
    public sealed class ScriptedResultActionNode : BtActionNodeBase
    {
        public const string ResultKeyProperty = "resultKey";
        private string _resultKey = "test.result";

        public override void OnInit(in BtNodeInitContext context)
        {
            _resultKey = context.Properties.GetString(ResultKeyProperty, "test.result");
        }

        public override BtNodeState OnTick(BtExecutionContext context)
        {
            context.Blackboard.TryGetInt64(_resultKey, out var result);
            return result switch
            {
                1 => BtNodeState.Success,
                2 => BtNodeState.Running,
                _ => BtNodeState.Failure,
            };
        }
    }

    /// <summary>测试用计数动作：Start/Stop 各自累加黑板计数，用于验证执行顺序与中止行为。</summary>
    public sealed class CountingActionNode : BtActionNodeBase
    {
        public const string ResultKeyProperty = "resultKey";
        public const string StartCounterKeyProperty = "startCounterKey";
        public const string StopCounterKeyProperty = "stopCounterKey";

        private string _resultKey = "test.result";
        private string _startCounterKey = "test.startCount";
        private string _stopCounterKey = "test.stopCount";

        public override void OnInit(in BtNodeInitContext context)
        {
            _resultKey = context.Properties.GetString(ResultKeyProperty, "test.result");
            _startCounterKey = context.Properties.GetString(StartCounterKeyProperty, "test.startCount");
            _stopCounterKey = context.Properties.GetString(StopCounterKeyProperty, "test.stopCount");
        }

        public override void OnStart(BtExecutionContext context)
        {
            // 计数 key 未声明时跳过（测试辅助容错），声明后按黑板类型累加
            if (context.Blackboard.TryGetInt64(_startCounterKey, out var count))
            {
                context.Blackboard.SetInt64(_startCounterKey, count + 1);
            }
        }

        public override void OnStop(BtExecutionContext context)
        {
            if (context.Blackboard.TryGetInt64(_stopCounterKey, out var count))
            {
                context.Blackboard.SetInt64(_stopCounterKey, count + 1);
            }
        }

        public override BtNodeState OnTick(BtExecutionContext context)
        {
            context.Blackboard.TryGetInt64(_resultKey, out var result);
            return result switch
            {
                1 => BtNodeState.Success,
                2 => BtNodeState.Running,
                _ => BtNodeState.Failure,
            };
        }
    }

    /// <summary>测试用脚本条件：读黑板 bool key。</summary>
    public sealed class ScriptedConditionNode : BtConditionNodeBase
    {
        public const string CondKeyProperty = "condKey";
        private string _condKey = "test.cond";

        public override void OnInit(in BtNodeInitContext context)
        {
            _condKey = context.Properties.GetString(CondKeyProperty, "test.cond");
        }

        protected override bool Validate(BtExecutionContext context)
        {
            return context.Blackboard.TryGetBool(_condKey, out var value) && value;
        }
    }

    public static class TestNodeTypes
    {
        public const string ScriptedAction = "test.scriptedAction";
        public const string CountingAction = "test.countingAction";
        public const string ScriptedCondition = "test.scriptedCondition";

        /// <summary>注册测试节点目录（含属性 schema）。</summary>
        public static BtNodeRegistry CreateRegistry()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            registry.Register(new BtNodeDescriptor(
                ScriptedAction, "脚本动作", "测试", BtNodeKind.Action, 0, 0,
                () => new ScriptedResultActionNode(),
                new[] { new BtPropertyField(ScriptedResultActionNode.ResultKeyProperty, BtValueType.String) }));

            registry.Register(new BtNodeDescriptor(
                CountingAction, "计数动作", "测试", BtNodeKind.Action, 0, 0,
                () => new CountingActionNode(),
                new[]
                {
                    new BtPropertyField(CountingActionNode.ResultKeyProperty, BtValueType.String),
                    new BtPropertyField(CountingActionNode.StartCounterKeyProperty, BtValueType.String),
                    new BtPropertyField(CountingActionNode.StopCounterKeyProperty, BtValueType.String),
                }));

            registry.Register(new BtNodeDescriptor(
                ScriptedCondition, "脚本条件", "测试", BtNodeKind.Condition, 0, 0,
                () => new ScriptedConditionNode(),
                new[] { new BtPropertyField(ScriptedConditionNode.CondKeyProperty, BtValueType.String) }));

            return registry;
        }
    }

    /// <summary>建树 DSL：快速组装 BtTreeDefinition。</summary>
    public sealed class TreeBuilder
    {
        private readonly BtTreeDefinition _definition = new() { TreeId = "test.tree" };

        public static TreeBuilder Create(string treeId = "test.tree") => new() { _definition = { TreeId = treeId } };

        public TreeBuilder() { }

        public TreeBuilder Node(string id, string type, params string[] childIds)
        {
            var node = new BtNodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            _definition.Nodes.Add(node);
            return this;
        }

        public TreeBuilder Node(string id, string type, long abortType, params string[] childIds)
        {
            Node(id, type, childIds);
            LastNode.Properties.Set(BtCompositeNode.AbortTypeProperty, BtPropertyValue.Of(abortType));
            return this;
        }

        public BtNodeDefinition LastNode => _definition.Nodes[_definition.Nodes.Count - 1];

        public TreeBuilder Blackboard(string key, BtValueType type, BtPropertyValue? @default = null)
        {
            _definition.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = key, Type = type, Default = @default });
            return this;
        }

        public BtTreeDefinition Root(string rootId)
        {
            _definition.RootNodeId = rootId;
            return _definition;
        }
    }
}
