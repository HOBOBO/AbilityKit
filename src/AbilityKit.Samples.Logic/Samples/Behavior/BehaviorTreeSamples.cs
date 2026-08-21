using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Behavior
{
    [Sample(720, "behavior", "bbtree", "basics", "package-api", "web")]
    public sealed class BTreeBasics : SampleBase
    {
        public override string Title => "Behavior Tree Basics";
        public override string Description => "展示行为树的基本概念：决策和执行分离";
        public override SampleCategory Category => SampleCategory.Behavior;

        protected override void OnRun()
        {
            var manager = new BehaviorManager();

            Section("Behavior Tree 核心概念");
            KeyValue("Decision", "决定做什么（返回 DecisionResult）");
            KeyValue("Executor", "执行决策（移动、攻击等）");
            KeyValue("IBehaviorContext", "行为上下文（Owner、Target 等）");

            Divider();
            Section("创建 Patrol 行为");

            var patrolDecision = new PatrolDecision();
            var worldQuery = new SampleWorldQuery();
            var patrolBehavior = manager.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = "Patrol",
                OwnerId = new BehaviorEntityId(1001),
                Priority = 1,
                Decision = patrolDecision,
                Executor = new DefaultExecutor(),
                World = worldQuery
            });

            KeyValue("BehaviorKind", patrolBehavior.BehaviorKind);
            KeyValue("OwnerId", patrolBehavior.OwnerId.ToString());
            KeyValue("Phase", patrolBehavior.Phase.ToString());

            Divider();
            Section("Tick 行为");

            for (int i = 0; i < 5; i++)
            {
                manager.Tick(0.1f, i);

                KeyValue($"Tick {i}",
                    $"Phase={patrolBehavior.Phase}, State={patrolDecision.CurrentState}");
            }

            Divider();
            Section("行为状态机");
            KeyValue("Created", BehaviorPhase.Created.ToString());
            KeyValue("Running", BehaviorPhase.Running.ToString());
            KeyValue("Paused", BehaviorPhase.Paused.ToString());
            KeyValue("Completed", BehaviorPhase.Completed.ToString());
            KeyValue("Interrupted", BehaviorPhase.Interrupted.ToString());

            Divider();
            Section("决策类型");
            KeyValue("Continue", "继续执行");
            KeyValue("ChangeState", "切换到新状态");
            KeyValue("Complete", "行为完成");
            KeyValue("Interrupt", "行为中断");
        }
    }

    [Sample(721, "behavior", "bbtree", "blackboard", "package-api", "web")]
    public sealed class BTreeBlackboardUsage : SampleBase
    {
        public override string Title => "Behavior Tree Blackboard";
        public override string Description => "展示如何使用黑板在行为树节点间共享数据";
        public override SampleCategory Category => SampleCategory.Behavior;

        protected override void OnRun()
        {
            var manager = new BehaviorManager();
            var blackboard = new SampleBlackboard();
            var worldQuery = new SampleWorldQuery();

            Section("黑板基础操作");
            KeyValue("SetValue<T>", "设置值");
            KeyValue("GetValue<T>", "获取值");
            KeyValue("HasKey", "检查键是否存在");

            blackboard.SetValue("PatrolCenter", new Vec3(0, 0, 0));
            blackboard.SetValue("PatrolRadius", 5f);
            blackboard.SetValue("CurrentTarget", new BehaviorEntityId(2001));

            KeyValue("PatrolCenter", blackboard.GetValue<Vec3>("PatrolCenter").ToString());
            KeyValue("PatrolRadius", blackboard.GetValue<float>("PatrolRadius").ToString());
            KeyValue("CurrentTarget", blackboard.GetValue<BehaviorEntityId>("CurrentTarget").ToString());
            KeyValue("Has EnemyDetected", blackboard.HasKey("EnemyDetected").ToString());

            Divider();
            Section("创建 Chase 行为（使用黑板数据）");

            var chaseDecision = new ChaseDecision(blackboard);
            var chaseBehavior = manager.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = "Chase",
                OwnerId = new BehaviorEntityId(1001),
                Priority = 5,
                Decision = chaseDecision,
                Executor = new DefaultExecutor(),
                World = worldQuery,
                Config = new Dictionary<string, object>
                {
                    { "targetId", (long)2001 }
                }
            });

            KeyValue("Priority", chaseBehavior.Priority.ToString());
            KeyValue("ChaseDecision.State", chaseDecision.CurrentState);

            Divider();
            Section("Tick 结果");

            for (int i = 0; i < 3; i++)
            {
                manager.Tick(0.1f, i);
                KeyValue($"Tick {i}", $"State={chaseDecision.CurrentState}");
            }

            Divider();
            Section("行为绑定来源");
            KeyValue("SourceContextId", chaseBehavior.SourceContextId.ToString());
            KeyValue("SourceContextId用途", "用于关联到 Effect/Buff/Skill");

            Divider();
            Section("管理器统计");
            KeyValue("RunningCount", manager.RunningCount.ToString());
            KeyValue("TotalCount", manager.TotalCount.ToString());
        }
    }

    [Sample(722, "behavior", "bbtree", "external-nodes", "package-api", "web")]
    public sealed class BTreeExternalNodes : SampleBase
    {
        public override string Title => "Behavior Tree External Nodes";
        public override string Description => "展示如何扩展行为树节点类型（Selector、Sequence）";
        public override SampleCategory Category => SampleCategory.Behavior;

        protected override void OnRun()
        {
            var manager = new BehaviorManager();
            var worldQuery = new SampleWorldQuery();

            Section("内置节点类型");
            KeyValue("Selector", "返回第一个成功的子节点");
            KeyValue("Sequence", "所有子节点都成功才成功");
            KeyValue("DelegateDecision", "委托决策器");

            Divider();
            Section("创建 Selector 决策树");

            var idleDecision = new DelegateDecision("Idle", (ctx, world) =>
            {
                return DecisionResult.Continue();
            });

            var patrolDecision = new PatrolDecision();

            var rootSelector = new SelectorDecision(idleDecision, patrolDecision);

            var compositeBehavior = manager.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = "AI",
                OwnerId = new BehaviorEntityId(1001),
                Priority = 10,
                Decision = rootSelector,
                Executor = new DefaultExecutor(),
                World = worldQuery
            });

            KeyValue("CompositeBehavior.Phase", compositeBehavior.Phase.ToString());
            KeyValue("Selector.Children.Count", rootSelector.Children.Count.ToString());

            Divider();
            Section("Tick 追踪");

            for (int i = 0; i < 4; i++)
            {
                manager.Tick(0.1f, i);

                KeyValue($"Tick {i}",
                    $"State={rootSelector.CurrentState}");
            }

            Divider();
            Section("创建 Sequence 决策树");

            var checkDecision = new DelegateDecision("Check", (ctx, world) =>
            {
                return DecisionResult.ChangeState("Action");
            });

            var actionDecision = new DelegateDecision("Action", (ctx, world) =>
            {
                return DecisionResult.Complete();
            });

            var sequenceDecision = new SequenceDecision(checkDecision, actionDecision);

            var seqBehavior = manager.CreateBehavior(new BehaviorCreateConfig
            {
                BehaviorKind = "Sequence",
                OwnerId = new BehaviorEntityId(1002),
                Priority = 5,
                Decision = sequenceDecision,
                Executor = new DefaultExecutor(),
                World = worldQuery
            });

            KeyValue("Sequence.Phase", seqBehavior.Phase.ToString());

            for (int i = 0; i < 4; i++)
            {
                manager.Tick(0.1f, i);
                KeyValue($"Tick {i}", $"State={sequenceDecision.CurrentState}, Phase={seqBehavior.Phase}");
            }

            Divider();
            Section("中断行为");
            manager.Interrupt(compositeBehavior.InstanceId, "Player died");
            KeyValue("After Interrupt", $"Phase={compositeBehavior.Phase}");

            Divider();
            Section("管理器统计");
            KeyValue("RunningCount", manager.RunningCount.ToString());
            KeyValue("TotalCount", manager.TotalCount.ToString());
        }
    }

    // ============ 辅助类 ============

    internal sealed class SampleWorldQuery : DefaultWorldQuery
    {
        private readonly Dictionary<long, Vec3> _positions = new Dictionary<long, Vec3>();

        public SampleWorldQuery()
        {
            _positions[1001] = new Vec3(0, 0, 0);
            _positions[1002] = new Vec3(5, 0, 0);
            _positions[2001] = new Vec3(10, 0, 0);
        }

        public new Vec3 GetPosition(BehaviorEntityId id)
        {
            return _positions.TryGetValue(id.Value, out var pos) ? pos : Vec3.Zero;
        }

        public new void SetPosition(BehaviorEntityId id, Vec3 position)
        {
            _positions[id.Value] = position;
        }
    }

    internal sealed class SampleBlackboard
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public T GetValue<T>(string key)
        {
            if (_values.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        public void SetValue<T>(string key, T value)
        {
            _values[key] = value;
        }

        public bool HasKey(string key) => _values.ContainsKey(key);

        public string BlackboardType => "Sample";
    }

    // ============ 示例 Decision 实现 ============

    internal sealed class PatrolDecision : IBehaviorDecision
    {
        private int _tickCount;
        private Vec3 _currentTarget = new Vec3(5, 0, 0);
        private string _currentState = "Patrol";
        private bool _reachedTarget = true;

        public string DecisionType => "Patrol";
        public string CurrentState => _currentState;

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            _tickCount++;

            if (_reachedTarget)
            {
                _currentTarget = _currentTarget.X > 5 ? new Vec3(0, 0, 0) : new Vec3(5, 0, 0);
                _reachedTarget = false;
            }

            var ownerPos = world.GetPosition(context.OwnerId);
            var distance = Vec3.Distance(ownerPos, _currentTarget);

            if (distance < 0.1f)
            {
                _reachedTarget = true;
            }

            return DecisionResult.Continue(_currentState);
        }
    }

    internal sealed class ChaseDecision : IBehaviorDecision
    {
        private readonly SampleBlackboard _blackboard;
        private int _tickCount;
        private string _currentState = "Chase";

        public string DecisionType => "Chase";
        public string CurrentState => _currentState;

        public ChaseDecision(SampleBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            _tickCount++;
            var targetId = _blackboard.GetValue<BehaviorEntityId>("CurrentTarget");

            var targetPos = world.GetPosition(targetId);
            var ownerPos = world.GetPosition(context.OwnerId);

            return DecisionResult.ChangeState(_currentState);
        }
    }
}
