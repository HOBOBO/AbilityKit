using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>
    /// 娴嬭瘯鐢ㄨ剼鏈妭鐐癸細琛屼负瀹屽叏鐢遍粦鏉块┍鍔紙resultKey/condKey锛夛紝鏃犻潤鎬佸彲鍙樼姸鎬侊紝
    /// 鍏煎 xunit 骞惰銆傚畾涔夊湪娴嬭瘯绋嬪簭闆嗕腑锛屽悓鏃堕獙璇佸寘澶栨墿灞曪紙ScanAssembly + 灞炴€?schema锛夈€?    /// </summary>
    public sealed class ScriptedResultActionNode : ActionNodeBase
    {
        public const string ResultKeyProperty = "resultKey";
        private string _resultKey = "test.result";

        public override void OnInit(in NodeInitContext context)
        {
            _resultKey = context.Properties.GetString(ResultKeyProperty, "test.result");
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            context.Blackboard.TryGetInt64(_resultKey, out var result);
            return result switch
            {
                1 => NodeState.Success,
                2 => NodeState.Running,
                _ => NodeState.Failure,
            };
        }
    }

    /// <summary>娴嬭瘯鐢ㄨ鏁板姩浣滐細Start/Stop 鍚勮嚜绱姞榛戞澘璁℃暟锛岀敤浜庨獙璇佹墽琛岄『搴忎笌涓琛屼负銆?/summary>
    public sealed class CountingActionNode : ActionNodeBase
    {
        public const string ResultKeyProperty = "resultKey";
        public const string StartCounterKeyProperty = "startCounterKey";
        public const string StopCounterKeyProperty = "stopCounterKey";

        private string _resultKey = "test.result";
        private string _startCounterKey = "test.startCount";
        private string _stopCounterKey = "test.stopCount";

        public override void OnInit(in NodeInitContext context)
        {
            _resultKey = context.Properties.GetString(ResultKeyProperty, "test.result");
            _startCounterKey = context.Properties.GetString(StartCounterKeyProperty, "test.startCount");
            _stopCounterKey = context.Properties.GetString(StopCounterKeyProperty, "test.stopCount");
        }

        public override void OnStart(ExecutionContext context)
        {
            if (context.Blackboard.TryGetInt64(_startCounterKey, out var count))
            {
                context.Blackboard.SetInt64(_startCounterKey, count + 1);
            }
        }

        public override void OnStop(ExecutionContext context)
        {
            if (context.Blackboard.TryGetInt64(_stopCounterKey, out var count))
            {
                context.Blackboard.SetInt64(_stopCounterKey, count + 1);
            }
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            context.Blackboard.TryGetInt64(_resultKey, out var result);
            return result switch
            {
                1 => NodeState.Success,
                2 => NodeState.Running,
                _ => NodeState.Failure,
            };
        }
    }

    /// <summary>娴嬭瘯鐢ㄨ剼鏈潯浠讹細璇婚粦鏉?bool key銆?/summary>
    public sealed class ScriptedConditionNode : ConditionNodeBase
    {
        public const string CondKeyProperty = "condKey";
        private string _condKey = "test.cond";

        public override void OnInit(in NodeInitContext context)
        {
            _condKey = context.Properties.GetString(CondKeyProperty, "test.cond");
        }

        protected override bool Validate(ExecutionContext context)
        {
            return context.Blackboard.TryGetBool(_condKey, out var value) && value;
        }
    }

    public static class TestNodeTypes
    {
        public const string ScriptedAction = "test.scriptedAction";
        public const string CountingAction = "test.countingAction";
        public const string ScriptedCondition = "test.scriptedCondition";

        /// <summary>娉ㄥ唽娴嬭瘯鑺傜偣鐩綍锛堝惈灞炴€?schema锛夈€?/summary>
        public static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            registry.Register(new NodeDescriptor(
                ScriptedAction, "鑴氭湰鍔ㄤ綔", "娴嬭瘯", NodeKind.Action, 0, 0,
                () => new ScriptedResultActionNode(),
                new[] { new PropertyField(ScriptedResultActionNode.ResultKeyProperty, TreeValueType.String) }));

            registry.Register(new NodeDescriptor(
                CountingAction, "璁℃暟鍔ㄤ綔", "娴嬭瘯", NodeKind.Action, 0, 0,
                () => new CountingActionNode(),
                new[]
                {
                    new PropertyField(CountingActionNode.ResultKeyProperty, TreeValueType.String),
                    new PropertyField(CountingActionNode.StartCounterKeyProperty, TreeValueType.String),
                    new PropertyField(CountingActionNode.StopCounterKeyProperty, TreeValueType.String),
                }));

            registry.Register(new NodeDescriptor(
                ScriptedCondition, "鑴氭湰鏉′欢", "娴嬭瘯", NodeKind.Condition, 0, 0,
                () => new ScriptedConditionNode(),
                new[] { new PropertyField(ScriptedConditionNode.CondKeyProperty, TreeValueType.String) }));

            return registry;
        }

        public static AbilityKit.BehaviorTree.Registry.NodeRegistry CreateApiRegistry()
        {
            var registry = new AbilityKit.BehaviorTree.Registry.NodeRegistry();
            AbilityKit.BehaviorTree.Nodes.BuiltInNodes.RegisterAll(registry);

            registry.Register(new AbilityKit.BehaviorTree.Registry.NodeDescriptor(
                ScriptedAction, "鑴氭湰鍔ㄤ綔", "娴嬭瘯", AbilityKit.BehaviorTree.Definition.NodeKind.Action, 0, 0,
                () => new ScriptedResultActionNode(),
                new[] { new AbilityKit.BehaviorTree.Registry.PropertyField(ScriptedResultActionNode.ResultKeyProperty, AbilityKit.BehaviorTree.Definition.ValueType.String) }));

            registry.Register(new AbilityKit.BehaviorTree.Registry.NodeDescriptor(
                CountingAction, "璁℃暟鍔ㄤ綔", "娴嬭瘯", AbilityKit.BehaviorTree.Definition.NodeKind.Action, 0, 0,
                () => new CountingActionNode(),
                new[]
                {
                    new AbilityKit.BehaviorTree.Registry.PropertyField(CountingActionNode.ResultKeyProperty, AbilityKit.BehaviorTree.Definition.ValueType.String),
                    new AbilityKit.BehaviorTree.Registry.PropertyField(CountingActionNode.StartCounterKeyProperty, AbilityKit.BehaviorTree.Definition.ValueType.String),
                    new AbilityKit.BehaviorTree.Registry.PropertyField(CountingActionNode.StopCounterKeyProperty, AbilityKit.BehaviorTree.Definition.ValueType.String),
                }));

            registry.Register(new AbilityKit.BehaviorTree.Registry.NodeDescriptor(
                ScriptedCondition, "鑴氭湰鏉′欢", "娴嬭瘯", AbilityKit.BehaviorTree.Definition.NodeKind.Condition, 0, 0,
                () => new ScriptedConditionNode(),
                new[] { new AbilityKit.BehaviorTree.Registry.PropertyField(ScriptedConditionNode.CondKeyProperty, AbilityKit.BehaviorTree.Definition.ValueType.String) }));

            return registry;
        }
    }

    /// <summary>寤烘爲 DSL锛氬揩閫熺粍瑁呮爲瀹氫箟銆?/summary>
    public sealed class TreeBuilder
    {
        private readonly TreeDefinition _definition = new() { TreeId = "test.tree" };

        public static TreeBuilder Create(string treeId = "test.tree") => new() { _definition = { TreeId = treeId } };

        public TreeBuilder() { }

        public TreeBuilder Node(string id, string type, params string[] childIds)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            _definition.Nodes.Add(node);
            return this;
        }

        public TreeBuilder Node(string id, string type, long abortType, params string[] childIds)
        {
            Node(id, type, childIds);
            LastNode.Properties.Set(CompositeNode.AbortTypeProperty, PropertyValue.Of(abortType));
            return this;
        }

        public NodeDefinition LastNode => _definition.Nodes[_definition.Nodes.Count - 1];

        public TreeBuilder Blackboard(string key, TreeValueType type, PropertyValue? @default = null)
        {
            _definition.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = key, Type = type, Default = @default });
            return this;
        }

        public TreeDefinition Root(string rootId)
        {
            _definition.RootNodeId = rootId;
            return _definition;
        }
    }

    public sealed class ApiTreeBuilder
    {
        private readonly AbilityKit.BehaviorTree.Definition.TreeDefinition _definition = new() { TreeId = "test.tree" };

        public static ApiTreeBuilder Create(string treeId = "test.tree") => new() { _definition = { TreeId = treeId } };

        public ApiTreeBuilder() { }

        public ApiTreeBuilder Node(string id, string type, params string[] childIds)
        {
            var node = new AbilityKit.BehaviorTree.Definition.NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            _definition.Nodes.Add(node);
            return this;
        }

        public ApiTreeBuilder Node(string id, string type, long abortType, params string[] childIds)
        {
            Node(id, type, childIds);
            LastNode.Properties.Set(CompositeNode.AbortTypeProperty, AbilityKit.BehaviorTree.Definition.PropertyValue.Of(abortType));
            return this;
        }

        public AbilityKit.BehaviorTree.Definition.NodeDefinition LastNode => _definition.Nodes[_definition.Nodes.Count - 1];

        public ApiTreeBuilder Blackboard(
            string key,
            AbilityKit.BehaviorTree.Definition.ValueType type,
            AbilityKit.BehaviorTree.Definition.PropertyValue? @default = null)
        {
            _definition.Blackboard.Keys.Add(new AbilityKit.BehaviorTree.Definition.BlackboardKeyDefinition
            {
                Name = key,
                Type = type,
                Default = @default,
            });
            return this;
        }

        public AbilityKit.BehaviorTree.Definition.TreeDefinition Root(string rootId)
        {
            _definition.RootNodeId = rootId;
            return _definition;
        }
    }
}
