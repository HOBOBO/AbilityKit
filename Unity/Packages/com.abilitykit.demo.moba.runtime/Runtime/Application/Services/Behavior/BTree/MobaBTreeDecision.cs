using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Services.Behavior.AI;
using AbilityKit.Demo.Moba.Input;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// 将确定性行为树接入共享行为运行时。逐帧事实在求值前刷新，意图每次重新输出；
    /// 跨帧记忆保存在 memory.*。可选恢复包含树值状态和决策外壳，不执行节点求值。
    /// </summary>
    internal sealed class MobaBTreeDecision : IBehaviorDecision, IBehaviorSnapshotValidator, IMobaActorIntentDecision, IDisposable
    {
        private readonly TreeRuntime _tree;
        private readonly MobaBTreeRuntimeContext _runtimeContext;
        private bool _reportedRunningRoot;
        private bool _disposed;

        public string DecisionType => "MobaBTree";
        public string SnapshotType => "MobaBTree.Runtime.v3";
        public string CurrentState { get; private set; } = "Running";
        public MobaActorIntent CurrentIntent { get; private set; } = MobaActorIntent.Hold;
        internal Blackboard Blackboard => _tree.Blackboard;

        private MobaBTreeDecision(
            TreeRuntime tree,
            MobaBTreeRuntimeContext runtimeContext)
        {
            _tree = tree;
            _runtimeContext = runtimeContext;
        }

        public static MobaBTreeDecision Create(
            string json,
            MobaActorRegistry registry,
            MobaConfigDatabase config = null,
            SearchTargetService searchTargets = null,
            Func<long> currentTimeMsProvider = null,
            MobaBrainSkillSelectionPolicy skillSelectionPolicy = MobaBrainSkillSelectionPolicy.FirstReady,
            string debugName = null,
            string debugOwnerLabel = null,
            MobaSkillLoadoutService skillLoadout = null,
            ulong randomSeed = 0,
            ulong randomScope = 0,
            long ownerActorId = 0)
        {
            if (string.IsNullOrEmpty(json)) return null;

            TreeDefinition definition;
            try
            {
                definition = DeserializeAndValidate(json);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaBTreeDecision] deserialize behavior tree failed");
                return null;
            }

            var runtimeContext = new MobaBTreeRuntimeContext(
                registry,
                config,
                searchTargets,
                currentTimeMsProvider,
                skillSelectionPolicy,
                skillLoadout);
            var services = new DefaultServiceResolver().Add(runtimeContext);

            // DebugName 非空才登记进 BtDebugRegistry，编辑器观察窗口按需拉取；
            // Dispose 时自动注销
            var options = new TreeRunOptions
            {
                /*
                 * 从世界随机服务使用的相同对局种子派生隔离树随机流。节点流保持独立，所有运行时共享同一算法。
                 */
                Seed = DeterministicRandom.DeriveSeed(randomSeed, randomScope),
                DebugName = string.IsNullOrEmpty(debugName) ? null : debugName,
                DebugOwnerLabel = debugOwnerLabel,
            };
            var tree = TreeRuntime.Create(definition, MobaBTreeCatalog.Registry, services, options, new MobaBTreeAssetResolver());
            tree.Enable(0, Fixed64.Zero);
            var decision = new MobaBTreeDecision(tree, runtimeContext);
            if (ownerActorId > 0 && ownerActorId <= int.MaxValue && registry != null &&
                registry.TryGet((int)ownerActorId, out var owner) && owner != null && owner.hasTransform)
                CaptureLaunchHeading(tree.Blackboard, owner.transform.Value.Forward);
            return decision;
        }

        /// <summary>
        /// 子树引用解析器：从同目录（Resources/Configs 的 moba/bt）按 treeId 读兄弟树 JSON。
        /// 使 MOBA 树可用 builtin.subtree 跨树组合。
        /// </summary>
        private sealed class MobaBTreeAssetResolver : TreeDefinitionResolver
        {
            public bool TryResolve(string treeId, out TreeDefinition definition)
            {
                definition = null!;
                if (string.IsNullOrEmpty(treeId) || !MobaBTreeAssetLoader.TryLoad(null, treeId, out var json))
                {
                    return false;
                }

                definition = TreeJson.Load(json);
                MobaBTreeBlackboard.EnsureStandardSchema(definition);
                return true;
            }
        }

        internal static void ValidateConfiguration(string json)
        {
            DeserializeAndValidate(json);
        }

        private static TreeDefinition DeserializeAndValidate(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("MOBA behavior-tree JSON is required.", nameof(json));

            var definition = TreeJson.Load(json);
            MobaBTreeBlackboard.EnsureStandardSchema(definition);
            var errors = TreeValidator.Validate(definition, MobaBTreeCatalog.Registry);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "MOBA behavior tree is invalid: " + string.Join("; ", errors));
            }
            return definition;
        }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            if (_disposed || context == null || world == null)
                return DecisionResult.Continue(CurrentState);

            var bb = _tree.Blackboard;
            _runtimeContext.BeginEvaluation(context, world);
            SyncFrameFacts(bb, context, world);
            var nowMs = _runtimeContext.GetCurrentTimeMs();
            _tree.Update((int)context.CurrentFrame, Fixed64.FromRatio(nowMs, 1000L));

            var rootState = _tree.RootNodeState;
            if (rootState == NodeState.Success || rootState == NodeState.Failure)
            {
                _tree.Restart();
            }
            else if (rootState == NodeState.Running && !_reportedRunningRoot)
            {
                _reportedRunningRoot = true;
                Log.Warning("[MobaBTreeDecision] root is Running. A running MOBA node must refresh " +
                            "every fact it consumes and re-emit its intent on every update.");
            }

            var parameters = new Dictionary<string, object>();
            if (bb.GetBool(MobaBTreeKeys.HasMove))
            {
                if (!bb.GetBool(MobaBTreeKeys.MoveIsDirection))
                    parameters["MoveTarget"] = new Vec3(
                        bb.GetFixed64(MobaBTreeKeys.MoveX).ToSingle(),
                        bb.GetFixed64(MobaBTreeKeys.MoveY).ToSingle(),
                        bb.GetFixed64(MobaBTreeKeys.MoveZ).ToSingle());
                parameters["MoveSpeed"] = world.GetMoveSpeed(context.OwnerId, 5f);
                parameters["MoveStopRange"] = bb.GetFixed64(MobaBTreeKeys.MoveStopRange).ToSingle();
                CurrentState = "Chasing";
            }
            if (bb.GetBool(MobaBTreeKeys.HasCast))
            {
                parameters[MobaBrainExecutor.SkillIdParam] = (int)bb.GetInt64(MobaBTreeKeys.CastSkillId);
                parameters[MobaBrainExecutor.SkillSlotParam] = (int)bb.GetInt64(MobaBTreeKeys.CastSkillSlot);
                parameters[MobaBrainExecutor.TargetActorIdParam] =
                    (int)bb.GetInt64(MobaBTreeKeys.CastTargetActorId);
                parameters[MobaBrainExecutor.AimPositionParam] = new Vec3(
                    bb.GetFixed64(MobaBTreeKeys.CastAimX).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.CastAimY).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.CastAimZ).ToSingle());
                parameters[MobaBrainExecutor.AimDirectionParam] = new Vec3(
                    bb.GetFixed64(MobaBTreeKeys.CastDirectionX).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.CastDirectionY).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.CastDirectionZ).ToSingle());
                CurrentState = "Casting";
            }
            else if (!bb.GetBool(MobaBTreeKeys.HasMove))
            {
                CurrentState = "Holding";
            }

            CurrentIntent = ReadIntent(bb);
            return DecisionResult.Continue(CurrentState).WithParams(parameters);
        }

        private static MobaActorIntent ReadIntent(Blackboard bb)
        {
            var intent = MobaActorIntent.Hold;
            if (bb.GetBool(MobaBTreeKeys.HasMove))
            {
                var value = new Vec3(bb.GetFixed64(MobaBTreeKeys.MoveX).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.MoveY).ToSingle(), bb.GetFixed64(MobaBTreeKeys.MoveZ).ToSingle());
                var stopRange = bb.GetFixed64(MobaBTreeKeys.MoveStopRange).ToSingle();
                intent = bb.GetBool(MobaBTreeKeys.MoveIsDirection)
                    ? MobaActorIntent.MoveDirection(value.X, value.Z)
                    : bb.GetBool(MobaBTreeKeys.MoveFollowsOwner)
                        ? MobaActorIntent.FollowOwner(in value, stopRange)
                        : MobaActorIntent.MoveTo(in value, stopRange);
            }
            if (bb.GetBool(MobaBTreeKeys.HasCast))
                intent = intent.WithCast((int)bb.GetInt64(MobaBTreeKeys.CastSkillSlot),
                    (int)bb.GetInt64(MobaBTreeKeys.CastSkillId), (int)bb.GetInt64(MobaBTreeKeys.CastTargetActorId),
                    new Vec3(bb.GetFixed64(MobaBTreeKeys.CastAimX).ToSingle(), bb.GetFixed64(MobaBTreeKeys.CastAimY).ToSingle(), bb.GetFixed64(MobaBTreeKeys.CastAimZ).ToSingle()),
                    new Vec3(bb.GetFixed64(MobaBTreeKeys.CastDirectionX).ToSingle(), bb.GetFixed64(MobaBTreeKeys.CastDirectionY).ToSingle(), bb.GetFixed64(MobaBTreeKeys.CastDirectionZ).ToSingle()));
            return intent;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _runtimeContext.EndEvaluation();
            _tree.Dispose();
            CurrentIntent = MobaActorIntent.Hold;
            CurrentState = "Disposed";
        }

        /// <summary>
        /// 捕获树、黑板及当前状态；已释放对象显式失败。
        /// </summary>
        public byte[] CaptureSnapshot()
        {
            if (_disposed) throw new InvalidOperationException("Cannot capture a disposed MOBA tree.");
            return System.Text.Encoding.UTF8.GetBytes(
                Newtonsoft.Json.JsonConvert.SerializeObject(new BrainTreeSnapshot
                { Version = 3, CurrentState = CurrentState, Tree = TreeJson.SaveSnapshot(_tree.CaptureState()) }));
        }

        /// <summary>
        /// 只读验证完整包络和树定义，不创建临时树或执行恢复。
        /// </summary>
        public void ValidateSnapshot(byte[] payload) => ReadSnapshot(payload);

        /// <summary>
        /// 先验证后恢复，当前意图从已恢复黑板静默投影。
        /// </summary>
        public void RestoreSnapshot(byte[] payload)
        {
            var snapshot = ReadSnapshot(payload);
            _tree.RestoreState(TreeJson.LoadSnapshot(snapshot.Tree));
            CurrentIntent = ReadIntent(_tree.Blackboard);
            CurrentState = snapshot.CurrentState;
            _reportedRunningRoot = false;
            _runtimeContext.EndEvaluation();
        }

        private BrainTreeSnapshot ReadSnapshot(byte[] payload)
        {
            if (_disposed || payload == null || payload.Length == 0 || payload.Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Missing, oversized or disposed MOBA tree snapshot.");
            var snapshot = Newtonsoft.Json.JsonConvert.DeserializeObject<BrainTreeSnapshot>(
                System.Text.Encoding.UTF8.GetString(payload));
            if (snapshot == null || snapshot.Version != 3 || string.IsNullOrEmpty(snapshot.Tree) ||
                (snapshot.CurrentState != "Running" && snapshot.CurrentState != "Chasing" &&
                 snapshot.CurrentState != "Casting" && snapshot.CurrentState != "Holding"))
                throw new InvalidOperationException("Invalid MOBA tree envelope; v3 is required.");
            var tree = TreeJson.LoadSnapshot(snapshot.Tree);
            if (tree.Blackboard == null) throw new InvalidOperationException("MOBA tree requires complete blackboard state.");
            _tree.ValidateState(tree);
            return snapshot;
        }

        /// <summary>
        /// 决策外壳和树快照的版本化值包络。
        /// </summary>
        private sealed class BrainTreeSnapshot
        {
            public int Version { get; set; }
            public string CurrentState { get; set; }
            public string Tree { get; set; }
        }

        private static void CaptureLaunchHeading(Blackboard bb, Vec3 forward)
        {
            var x = Fixed64.FromSingle(forward.X);
            var z = Fixed64.FromSingle(forward.Z);
            var length = DeterministicMath.Sqrt(x * x + z * z);
            if (length <= Fixed64.Zero) return;
            bb.SetFixed64(MobaBTreeKeys.LaunchHeadingX, x / length);
            bb.SetFixed64(MobaBTreeKeys.LaunchHeadingZ, z / length);
            bb.SetBool(MobaBTreeKeys.LaunchHeadingValid, true);
        }

        private static void SyncFrameFacts(Blackboard bb, IBehaviorContext context, IWorldQuery world)
        {
            var ownerPosition = world.GetPosition(context.OwnerId);
            var forward = world.GetForward(context.OwnerId);
            bb.SetFixed64(MobaBTreeKeys.OwnerForwardX, Fixed64.FromSingle(forward.X));
            bb.SetFixed64(MobaBTreeKeys.OwnerForwardZ, Fixed64.FromSingle(forward.Z));
            bb.SetInt64(MobaBTreeKeys.OwnerId, context.OwnerId.Value);
            bb.SetFixed64(MobaBTreeKeys.OwnerX, Fixed64.FromSingle(ownerPosition.X));
            bb.SetFixed64(MobaBTreeKeys.OwnerY, Fixed64.FromSingle(ownerPosition.Y));
            bb.SetFixed64(MobaBTreeKeys.OwnerZ, Fixed64.FromSingle(ownerPosition.Z));
            bb.SetFixed64(MobaBTreeKeys.OwnerSpeed, Fixed64.FromSingle(world.GetMoveSpeed(context.OwnerId, 5f)));
            bb.SetBool(MobaBTreeKeys.OwnerCanMove, world.CanMove(context.OwnerId));
            bb.SetBool(MobaBTreeKeys.OwnerCanCast, world.CanCast(context.OwnerId));
            bb.SetInt64(MobaBTreeKeys.EvaluationFrame, context.CurrentFrame);
            MobaBTreeBlackboard.ClearTransientIntents(bb);
        }
    }
}
