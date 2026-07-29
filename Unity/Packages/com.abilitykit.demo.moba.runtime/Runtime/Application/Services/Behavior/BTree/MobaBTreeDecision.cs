using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Moba.Behavior;
using BTCore.Runtime;
using BTCore.Runtime.Blackboards;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// Adapts a BTCore tree to the shared behavior runtime. MOBA trees are expected to be reactive:
    /// frame facts are refreshed before Update, and intent nodes must publish their request each tick.
    /// Persistent state belongs under memory.* rather than in transient fact or intent keys.
    /// </summary>
    internal sealed class MobaBTreeDecision : IBehaviorDecision
    {
        private readonly global::BTCore.Runtime.BTree _tree;
        private readonly MobaBTreeRuntimeContext _runtimeContext;
        private bool _reportedRunningRoot;

        public string DecisionType => "MobaBTree";
        public string CurrentState { get; private set; } = "Running";
        internal Blackboard Blackboard => _tree.Blackboard;

        private MobaBTreeDecision(
            global::BTCore.Runtime.BTree tree,
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
            Func<long> currentTimeMsProvider = null)
        {
            if (string.IsNullOrEmpty(json)) return null;

            global::BTCore.Runtime.BTree tree;
            try
            {
                json = NormalizeSerializedTypes(json);
                tree = JsonConvert.DeserializeObject<global::BTCore.Runtime.BTree>(
                    json,
                    BTDef.SerializerSettingsAuto);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaBTreeDecision] deserialize behavior tree failed");
                return null;
            }

            if (tree == null) return null;

            MobaBTreeBlackboard.Initialize(tree.Blackboard);
            var runtimeContext = new MobaBTreeRuntimeContext(
                registry,
                config,
                searchTargets,
                currentTimeMsProvider);
            foreach (var node in tree.BTData.Nodes)
            {
                if (node is IMobaBTreeContextNode contextNode) contextNode.Bind(runtimeContext);
            }

            tree.RebuildTree();
            tree.Enable();
            return new MobaBTreeDecision(tree, runtimeContext);
        }

        private static string NormalizeSerializedTypes(string json)
        {
            var root = JObject.Parse(json);
            var nodeTypes = typeof(MobaBTreeDecision).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract
                               && typeof(BTNode).IsAssignableFrom(type)
                               && type.Namespace == typeof(MobaBTreeDecision).Namespace)
                .ToDictionary(type => type.Name, StringComparer.Ordinal);
            var btAssemblyName = typeof(global::BTCore.Runtime.BTree).Assembly.GetName().Name;

            foreach (var obj in new[] { root }.Concat(root.Descendants().OfType<JObject>()))
            {
                if (obj["$type"]?.Type != JTokenType.String) continue;
                var serializedType = obj["$type"].Value<string>();
                if (serializedType.StartsWith("BTEXT:", StringComparison.Ordinal))
                {
                    var nodeName = serializedType.Substring("BTEXT:".Length);
                    if (!nodeTypes.TryGetValue(nodeName, out var nodeType))
                        throw new JsonSerializationException($"Unknown MOBA behavior-tree node: {nodeName}");
                    serializedType = nodeType.AssemblyQualifiedName;
                }
                else
                {
                    serializedType = serializedType.Replace(", BTRuntime", ", " + btAssemblyName);
                }

                obj["$type"] = serializedType;
            }

            return root.ToString(Formatting.None);
        }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            if (_tree == null || context == null || world == null)
                return DecisionResult.Continue(CurrentState);

            var bb = _tree.Blackboard;
            _runtimeContext.BeginEvaluation(context, world);
            SyncFrameFacts(bb, context, world);
            _tree.Update();

            var rootState = _tree.BTData.EntryNode?.GetChild()?.State ?? _tree.TreeState;
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
            var outputKind = (MobaBTreeIntentKind)bb.GetValue<int>(MobaBTreeKeys.OutputKind);
            if (outputKind == MobaBTreeIntentKind.Move && bb.GetValue<bool>(MobaBTreeKeys.HasMove))
            {
                parameters["MoveTarget"] = new Vec3(
                    bb.GetValue<float>(MobaBTreeKeys.MoveX),
                    bb.GetValue<float>(MobaBTreeKeys.MoveY),
                    bb.GetValue<float>(MobaBTreeKeys.MoveZ));
                parameters["MoveSpeed"] = world.GetMoveSpeed(context.OwnerId, 5f);
                CurrentState = "Chasing";
            }
            else if (outputKind == MobaBTreeIntentKind.Cast && bb.GetValue<bool>(MobaBTreeKeys.HasCast))
            {
                parameters[MobaBrainExecutor.SkillIdParam] = bb.GetValue<int>(MobaBTreeKeys.CastSkillId);
                parameters[MobaBrainExecutor.SkillSlotParam] = bb.GetValue<int>(MobaBTreeKeys.CastSkillSlot);
                parameters[MobaBrainExecutor.TargetActorIdParam] =
                    bb.GetValue<int>(MobaBTreeKeys.CastTargetActorId);
                parameters[MobaBrainExecutor.AimPositionParam] = new Vec3(
                    bb.GetValue<float>(MobaBTreeKeys.CastAimX),
                    bb.GetValue<float>(MobaBTreeKeys.CastAimY),
                    bb.GetValue<float>(MobaBTreeKeys.CastAimZ));
                parameters[MobaBrainExecutor.AimDirectionParam] = new Vec3(
                    bb.GetValue<float>(MobaBTreeKeys.CastDirectionX),
                    bb.GetValue<float>(MobaBTreeKeys.CastDirectionY),
                    bb.GetValue<float>(MobaBTreeKeys.CastDirectionZ));
                CurrentState = "Casting";
            }
            else
            {
                CurrentState = "Holding";
            }

            return DecisionResult.Continue(CurrentState).WithParams(parameters);
        }

        private static void SyncFrameFacts(Blackboard bb, IBehaviorContext context, IWorldQuery world)
        {
            var ownerPosition = world.GetPosition(context.OwnerId);
            bb.SetValue(MobaBTreeKeys.OwnerId, context.OwnerId.Value);
            bb.SetValue(MobaBTreeKeys.OwnerX, ownerPosition.X);
            bb.SetValue(MobaBTreeKeys.OwnerY, ownerPosition.Y);
            bb.SetValue(MobaBTreeKeys.OwnerZ, ownerPosition.Z);
            bb.SetValue(MobaBTreeKeys.OwnerSpeed, world.GetMoveSpeed(context.OwnerId, 5f));
            bb.SetValue(MobaBTreeKeys.OwnerCanMove, world.CanMove(context.OwnerId));
            bb.SetValue(MobaBTreeKeys.OwnerCanCast, world.CanCast(context.OwnerId));
            bb.SetValue(MobaBTreeKeys.EvaluationFrame, context.CurrentFrame);
            MobaBTreeBlackboard.ClearTransientIntents(bb);
        }
    }
}
