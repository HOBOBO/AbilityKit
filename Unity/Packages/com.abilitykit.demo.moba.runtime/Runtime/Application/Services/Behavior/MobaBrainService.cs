using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 为带 ActorBrain 的 Actor 创建并驱动 BehaviorRuntime。
    /// BrainId 只通过战斗模板 Brain 目录解析；解析或驱动创建失败时不创建运行时。
    /// </summary>
    [WorldService(typeof(MobaBrainService), WorldLifetime.Scoped)]
    public sealed class MobaBrainService : IService
    {
        private const string DefaultBehaviorKind = "moba.actor.brain";
        private readonly MobaActorRegistry _registry;
        private readonly IMobaActorBrainCatalog _catalog;
        private readonly MobaConfigDatabase _config;
        private readonly MobaWorldQuery _worldQuery;
        private readonly BehaviorManager _behaviors = new BehaviorManager();
        private readonly MobaBrainDecisionDriverRegistry _decisionDrivers;
        private readonly IMobaActorStateMachineProfileCatalog _stateMachineProfiles;
        private readonly Dictionary<int, BrainCreationIdentity> _failedCreations = new();
        [WorldInject(required: false)] private IFrameTime _frameTime;
        [WorldInject(required: false)] private SearchTargetService _searchTargets;

        public MobaBrainDecisionDriverRegistry DecisionDrivers => _decisionDrivers;

        public MobaBrainService(
            MobaActorRegistry registry,
            IMobaActorBrainCatalog catalog,
            MobaConfigDatabase config = null)
            : this(registry, catalog, config, MobaBrainDecisionDriverRegistry.CreateDefault(), null)
        {
        }

        public MobaBrainService(
            MobaActorRegistry registry,
            IMobaActorBrainCatalog catalog,
            MobaConfigDatabase config,
            MobaBrainDecisionDriverRegistry decisionDrivers)
            : this(registry, catalog, config, decisionDrivers, null)
        {
        }

        public MobaBrainService(
            MobaActorRegistry registry,
            IMobaActorBrainCatalog catalog,
            MobaConfigDatabase config,
            MobaBrainDecisionDriverRegistry decisionDrivers,
            IMobaActorStateMachineProfileCatalog stateMachineProfiles)
        {
            _registry = registry;
            _catalog = catalog;
            _config = config;
            _decisionDrivers = decisionDrivers ?? MobaBrainDecisionDriverRegistry.CreateDefault();
            _stateMachineProfiles = stateMachineProfiles;
            _worldQuery = new MobaWorldQuery(
                new MobaBrainEntityManager(registry),
                new MobaBrainBuffManager(registry),
                new MobaBrainAttributeSystem(registry),
                allowMutations: false);
        }

        public BehaviorRuntime EnsureBehavior(global::ActorEntity actor)
        {
            if (actor == null || !actor.hasActorId || !actor.hasActorBrain) return null;

            var brain = actor.actorBrain;
            if (brain.BrainId <= 0) return null;

            var existing = brain.BehaviorInstanceId > 0 ? _behaviors.GetBehavior(brain.BehaviorInstanceId) : null;
            if (existing != null && existing.Phase == BehaviorPhase.Running)
            {
                _failedCreations.Remove(actor.actorId.Value);
                return existing;
            }

            var ownerActorId = actor.actorId.Value;

            if (_catalog == null || !_catalog.TryGet(brain.BrainId, out var definition))
            {
                var missingIdentity = BrainCreationIdentity.Missing(in brain);
                if (IsSuppressed(ownerActorId, in missingIdentity)) return null;

                _failedCreations[ownerActorId] = missingIdentity;
                Log.Error($"[MobaBrain] brain definition was not found. brainId={brain.BrainId} sourceKind={brain.SourceKind} sourceId={brain.SourceId}");
                return null;
            }

            if (IsLogicHfsm(in definition))
            {
                _failedCreations.Remove(ownerActorId);
                ReleaseBehavior(actor, "HfsmOwnership");
                StopMovement(actor);
                return null;
            }

            var identity = new BrainCreationIdentity(in brain, in definition);
            if (IsSuppressed(ownerActorId, in identity)) return null;

            if (!TryCreateBehaviorRuntime(
                    actor,
                    in definition,
                    brain.SourceKind,
                    brain.SourceId,
                    out var runtime))
            {
                _failedCreations[ownerActorId] = identity;
                Log.Error($"[MobaBrain] brain driver failed to create a decision. brainId={brain.BrainId} driver={definition.DriverKind} definition={definition.DecisionName}");
                return null;
            }

            var previousInstanceId = brain.BehaviorInstanceId;
            actor.ReplaceActorBrain(
                brain.BrainId,
                brain.OwnerActorId,
                brain.SourceKind,
                brain.SourceId,
                runtime.InstanceId);
            RemoveBrainOwnedStateMachine(actor);

            if (previousInstanceId > 0 && previousInstanceId != runtime.InstanceId)
                _behaviors.Interrupt(previousInstanceId, "BrainReplaced");
            _failedCreations.Remove(ownerActorId);

            return runtime;
        }

        public bool ActivateBrain(global::ActorEntity actor, int brainId, int sourceKind, int sourceId)
        {
            if (actor == null || !actor.hasActorId || brainId <= 0) return false;
            if (_catalog == null || !_catalog.TryGet(brainId, out var definition))
            {
                Log.Error($"[MobaBrain] source references an unknown brain. brainId={brainId} sourceKind={sourceKind} sourceId={sourceId}");
                return false;
            }

            if (IsLogicHfsm(in definition))
            {
                if (_stateMachineProfiles == null
                    || !_stateMachineProfiles.TryGet(definition.DecisionName, out _))
                {
                    Log.Error($"[MobaBrain] HFSM profile was not found. brainId={brainId} profile={definition.DecisionName}");
                    return false;
                }

                var previousInstanceId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
                CommitBrain(actor, brainId, sourceKind, sourceId, behaviorInstanceId: 0L);
                RemoveBrainOwnedStateMachine(actor);
                if (previousInstanceId > 0) _behaviors.Interrupt(previousInstanceId, "BrainReplaced");
                StopMovement(actor);
                _failedCreations.Remove(actor.actorId.Value);
                return true;
            }

            if (!TryCreateBehaviorRuntime(actor, in definition, sourceKind, sourceId, out var runtime))
            {
                Log.Error($"[MobaBrain] brain driver failed to create a decision. brainId={brainId} driver={definition.DriverKind} definition={definition.DecisionName}");
                return false;
            }

            var oldInstanceId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            CommitBrain(actor, brainId, sourceKind, sourceId, runtime.InstanceId);
            RemoveBrainOwnedStateMachine(actor);
            if (oldInstanceId > 0 && oldInstanceId != runtime.InstanceId)
                _behaviors.Interrupt(oldInstanceId, "BrainReplaced");
            _failedCreations.Remove(actor.actorId.Value);
            return true;
        }

        public bool DeactivateBrain(global::ActorEntity actor)
        {
            if (actor == null) return false;

            var hadBrain = actor.hasActorBrain;
            var instanceId = hadBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            RemoveBrainOwnedStateMachine(actor);
            if (hadBrain)
            {
                actor.RemoveActorBrain();
            }

            if (instanceId > 0) _behaviors.Interrupt(instanceId, "BrainDisabled");
            if (actor.hasActorId) _failedCreations.Remove(actor.actorId.Value);

            if (actor.hasMoveInput) actor.ReplaceMoveInput(0f, 0f);
            return hadBrain;
        }

        public bool ReleaseBehavior(global::ActorEntity actor, string reason)
        {
            if (actor == null || !actor.hasActorBrain) return false;

            var brain = actor.actorBrain;
            var instanceId = brain.BehaviorInstanceId;
            if (instanceId <= 0) return false;

            actor.ReplaceActorBrain(
                brain.BrainId,
                brain.OwnerActorId,
                brain.SourceKind,
                brain.SourceId,
                0L);
            _behaviors.Interrupt(instanceId, string.IsNullOrWhiteSpace(reason) ? "BrainReleased" : reason);
            StopMovement(actor);
            if (actor.hasActorId) _failedCreations.Remove(actor.actorId.Value);
            return true;
        }

        public bool TryGetBehavior(long instanceId, out BehaviorRuntime behavior)
        {
            behavior = instanceId > 0 ? _behaviors.GetBehavior(instanceId) : null;
            return behavior != null;
        }

        public bool TryRestoreBehaviorSnapshot(
            global::ActorEntity actor,
            string snapshotType,
            byte[] payload)
        {
            if (actor == null || !actor.hasActorBrain || actor.actorBrain.BehaviorInstanceId <= 0
                || string.IsNullOrWhiteSpace(snapshotType) || payload == null || payload.Length == 0)
                return false;
            if (!(_behaviors.GetBehavior(actor.actorBrain.BehaviorInstanceId) is { } behavior)
                || behavior.Decision is not IBehaviorRuntimeSnapshot snapshot
                || !string.Equals(snapshot.SnapshotType, snapshotType, StringComparison.Ordinal))
                return false;

            try
            {
                snapshot.RestoreSnapshot(payload);
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBrain] behavior snapshot restore failed. actor={actor.actorId.Value}");
                return false;
            }
        }

        public void Tick(float deltaTimeSeconds, long frame)
        {
            if (deltaTimeSeconds <= 0f) return;
            _behaviors.Tick(deltaTimeSeconds, frame);
        }

        private long GetCurrentTimeMs()
        {
            return MobaSkillRuntimeAccess.GetCurrentTimeMs(_frameTime);
        }

        public void Dispose()
        {
            _behaviors.Dispose();
            _failedCreations.Clear();
        }

        private static void StopMovement(global::ActorEntity actor)
        {
            if (actor != null && actor.hasMoveInput)
                actor.ReplaceMoveInput(0f, 0f);
        }

        private static void RemoveBrainOwnedStateMachine(global::ActorEntity actor)
        {
            if (actor == null || !actor.hasActorStateMachine) return;
            if (actor.actorStateMachine.OwnerKind ==
                global::AbilityKit.Demo.Moba.Components.MobaActorStateMachineOwnerKind.Projectile)
                return;
            actor.RemoveActorStateMachine();
        }

        private bool IsLogicHfsm(in MobaActorBrainDefinition definition)
        {
            return string.Equals(
                    definition.DriverKind,
                    MobaBrainDriverKeys.Hfsm,
                    StringComparison.Ordinal)
                && !_decisionDrivers.Contains(MobaBrainDriverKeys.Hfsm);
        }

        private bool TryCreateBehaviorRuntime(
            global::ActorEntity actor,
            in MobaActorBrainDefinition definition,
            int sourceKind,
            int sourceId,
            out BehaviorRuntime runtime)
        {
            runtime = null;
            var ownerActorId = actor.actorId.Value;
            var context = new MobaBrainDecisionCreateContext(
                in definition,
                _registry,
                _config,
                ownerActorId,
                sourceKind,
                sourceId,
                _searchTargets,
                GetCurrentTimeMs);

            try
            {
                if (!_decisionDrivers.TryCreate(in context, out var decision) || decision == null) return false;

                runtime = _behaviors.CreateBehavior(new BehaviorCreateConfig
                {
                    BehaviorKind = DefaultBehaviorKind,
                    SourceContextId = definition.BrainId,
                    OwnerId = new BehaviorEntityId(ownerActorId),
                    Decision = decision,
                    Executor = new MobaBrainExecutor(),
                    World = _worldQuery
                });
                return runtime != null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBrain] brain runtime create failed. brainId={definition.BrainId} driver={definition.DriverKind} definition={definition.DecisionName}");
                return false;
            }
        }

        private void CommitBrain(
            global::ActorEntity actor,
            int brainId,
            int sourceKind,
            int sourceId,
            long behaviorInstanceId)
        {
            if (actor.hasActorBrain)
            {
                actor.ReplaceActorBrain(
                    brainId,
                    actor.actorId.Value,
                    sourceKind,
                    sourceId,
                    behaviorInstanceId);
            }
            else
            {
                actor.AddActorBrain(
                    brainId,
                    actor.actorId.Value,
                    sourceKind,
                    sourceId,
                    behaviorInstanceId);
            }
        }

        private bool IsSuppressed(int actorId, in BrainCreationIdentity identity)
        {
            return _failedCreations.TryGetValue(actorId, out var failed) && failed.Equals(identity);
        }

        private readonly struct BrainCreationIdentity : IEquatable<BrainCreationIdentity>
        {
            public BrainCreationIdentity(
                in AbilityKit.Demo.Moba.Components.ActorBrainComponent brain,
                in MobaActorBrainDefinition definition)
            {
                BrainId = brain.BrainId;
                OwnerActorId = brain.OwnerActorId;
                SourceKind = brain.SourceKind;
                SourceId = brain.SourceId;
                DriverKind = definition.DriverKind;
                DecisionName = definition.DecisionName ?? string.Empty;
            }

            private BrainCreationIdentity(
                int brainId,
                int ownerActorId,
                int sourceKind,
                int sourceId)
            {
                BrainId = brainId;
                OwnerActorId = ownerActorId;
                SourceKind = sourceKind;
                SourceId = sourceId;
                DriverKind = string.Empty;
                DecisionName = string.Empty;
            }

            public int BrainId { get; }
            public int OwnerActorId { get; }
            public int SourceKind { get; }
            public int SourceId { get; }
            public string DriverKind { get; }
            public string DecisionName { get; }

            public static BrainCreationIdentity Missing(
                in AbilityKit.Demo.Moba.Components.ActorBrainComponent brain)
            {
                return new BrainCreationIdentity(
                    brain.BrainId,
                    brain.OwnerActorId,
                    brain.SourceKind,
                    brain.SourceId);
            }

            public bool Equals(BrainCreationIdentity other)
            {
                return BrainId == other.BrainId
                    && OwnerActorId == other.OwnerActorId
                    && SourceKind == other.SourceKind
                    && SourceId == other.SourceId
                    && string.Equals(DriverKind, other.DriverKind, StringComparison.Ordinal)
                    && string.Equals(DecisionName, other.DecisionName, StringComparison.Ordinal);
            }
        }
    }
}
