using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.BehaviorTree;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// Adapts a deterministic AbilityKit behavior tree to the shared behavior runtime. MOBA trees
    /// are expected to be reactive: frame facts are refreshed before Update, and intent nodes must
    /// publish their request each tick. Persistent state belongs under memory.* rather than in
    /// transient fact or intent keys.
    /// </summary>
    internal sealed class MobaBTreeDecision : IBehaviorDecision, IBehaviorRuntimeSnapshot, IDisposable
    {
        private readonly BtTreeRuntime _tree;
        private readonly MobaBTreeRuntimeContext _runtimeContext;
        private bool _reportedRunningRoot;
        private bool _disposed;

        public string DecisionType => "MobaBTree";
        public string SnapshotType => "MobaBTree.Runtime.v2";
        public string CurrentState { get; private set; } = "Running";
        internal BtBlackboard Blackboard => _tree.Blackboard;

        private MobaBTreeDecision(
            BtTreeRuntime tree,
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
            string debugOwnerLabel = null)
        {
            if (string.IsNullOrEmpty(json)) return null;

            BtTreeDefinition definition;
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
                skillSelectionPolicy);
            var services = new BtServiceResolver().Add(runtimeContext);

            // DebugName 非空才登记进 BtDebugRegistry，编辑器观察窗口按需拉取；
            // Dispose 时自动注销
            var options = new BtTreeRunOptions
            {
                DebugName = string.IsNullOrEmpty(debugName) ? null : debugName,
                DebugOwnerLabel = debugOwnerLabel,
            };
            var tree = BtTreeRuntime.Create(definition, MobaBTreeCatalog.Registry, services, options, new MobaBTreeAssetResolver());
            tree.Enable(0, Fixed64.Zero);
            return new MobaBTreeDecision(tree, runtimeContext);
        }

        /// <summary>
        /// 子树引用解析器：从同目录（Resources/Configs 的 moba/bt）按 treeId 读兄弟树 JSON。
        /// 使 MOBA 树可用 builtin.subtree 跨树组合。
        /// </summary>
        private sealed class MobaBTreeAssetResolver : IBtTreeDefinitionResolver
        {
            public bool TryResolve(string treeId, out BtTreeDefinition definition)
            {
                definition = null!;
                if (string.IsNullOrEmpty(treeId) || !MobaBTreeAssetLoader.TryLoad(null, treeId, out var json))
                {
                    return false;
                }

                definition = BtTreeJson.Load(json);
                MobaBTreeBlackboard.EnsureStandardSchema(definition);
                return true;
            }
        }

        internal static void ValidateConfiguration(string json)
        {
            DeserializeAndValidate(json);
        }

        private static BtTreeDefinition DeserializeAndValidate(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("MOBA behavior-tree JSON is required.", nameof(json));

            var definition = BtTreeJson.Load(json);
            MobaBTreeBlackboard.EnsureStandardSchema(definition);
            var errors = BtTreeValidator.Validate(definition, MobaBTreeCatalog.Registry);
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
            if (rootState == BtNodeState.Success || rootState == BtNodeState.Failure)
            {
                _tree.Restart();
            }
            else if (rootState == BtNodeState.Running && !_reportedRunningRoot)
            {
                _reportedRunningRoot = true;
                Log.Warning("[MobaBTreeDecision] root is Running. A running MOBA node must refresh " +
                            "every fact it consumes and re-emit its intent on every update.");
            }

            var parameters = new Dictionary<string, object>();
            var outputKind = (MobaBTreeIntentKind)bb.GetInt64(MobaBTreeKeys.OutputKind);
            if (outputKind == MobaBTreeIntentKind.Move && bb.GetBool(MobaBTreeKeys.HasMove))
            {
                parameters["MoveTarget"] = new Vec3(
                    bb.GetFixed64(MobaBTreeKeys.MoveX).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.MoveY).ToSingle(),
                    bb.GetFixed64(MobaBTreeKeys.MoveZ).ToSingle());
                parameters["MoveSpeed"] = world.GetMoveSpeed(context.OwnerId, 5f);
                CurrentState = "Chasing";
            }
            else if (outputKind == MobaBTreeIntentKind.Cast && bb.GetBool(MobaBTreeKeys.HasCast))
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
            else
            {
                CurrentState = "Holding";
            }

            return DecisionResult.Continue(CurrentState).WithParams(parameters);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _runtimeContext.EndEvaluation();
            _tree.Dispose();
            CurrentState = "Disposed";
        }

        public byte[] CaptureSnapshot()
        {
            if (_disposed) return Array.Empty<byte>();
            return System.Text.Encoding.UTF8.GetBytes(
                BtTreeJson.SaveSnapshot(_tree.CaptureState()));
        }

        public void RestoreSnapshot(byte[] payload)
        {
            if (_disposed || payload == null || payload.Length == 0) return;
            var json = System.Text.Encoding.UTF8.GetString(payload);
            _tree.RestoreState(BtTreeJson.LoadSnapshot(json));
            _reportedRunningRoot = false;
        }

        private static void SyncFrameFacts(BtBlackboard bb, IBehaviorContext context, IWorldQuery world)
        {
            var ownerPosition = world.GetPosition(context.OwnerId);
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
