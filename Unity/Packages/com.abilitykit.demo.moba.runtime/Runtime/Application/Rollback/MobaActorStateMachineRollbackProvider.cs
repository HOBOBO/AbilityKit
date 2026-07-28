using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaActorStateMachineRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10005;

        private static readonly ObjectPool<List<MobaActorStateMachineRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaActorStateMachineRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _actors;
        private readonly MobaActorStateMachineFactory _factory;

        public MobaActorStateMachineRollbackProvider(
            MobaActorRegistry actors,
            MobaActorStateMachineFactory factory)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public int Key => DefaultKey;
        public string Name => "ActorStateMachine";

        public byte[] Export(FrameIndex frame) => ExportState(frame);

        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var pair in _actors.Entries)
                {
                    var actor = pair.Value;
                    if (actor == null) continue;

                    if (!actor.hasActorStateMachine || actor.actorStateMachine.Runtime == null)
                    {
                        entries.Add(new MobaActorStateMachineRollbackEntry(
                            pair.Key,
                            hasRuntime: false,
                            profileId: string.Empty,
                            deltaTime: 0f,
                            state: default,
                            root: default));
                        continue;
                    }

                    var snapshot = actor.actorStateMachine.Runtime.CaptureSnapshot();
                    entries.Add(new MobaActorStateMachineRollbackEntry(
                        pair.Key,
                        hasRuntime: true,
                        snapshot.ProfileId,
                        snapshot.DeltaTime,
                        ToSerializable(snapshot.State),
                        ToSerializable(snapshot.Root)));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                var array = entries.Count == 0
                    ? Array.Empty<MobaActorStateMachineRollbackEntry>()
                    : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaActorStateMachineRollbackPayload(2, array));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var snapshot = MemoryPackSerializer.Deserialize<MobaActorStateMachineRollbackPayload>(payload);
            if (snapshot.Version != 2)
            {
                throw new InvalidOperationException(
                    $"Unsupported actor state-machine rollback payload version '{snapshot.Version}'.");
            }

            var entries = snapshot.Entries ?? Array.Empty<MobaActorStateMachineRollbackEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                RestoreEntry(entries[i]);
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private void RestoreEntry(in MobaActorStateMachineRollbackEntry entry)
        {
            if (!_actors.TryGet(entry.ActorId, out var actor) || actor == null) return;

            if (!entry.HasRuntime)
            {
                if (actor.hasActorStateMachine) actor.RemoveActorStateMachine();
                return;
            }

            MobaActorStateMachineRuntime runtime = null;
            if (actor.hasActorStateMachine
                && string.Equals(actor.actorStateMachine.ProfileId, entry.ProfileId, StringComparison.Ordinal))
            {
                runtime = actor.actorStateMachine.Runtime;
            }

            if (runtime == null)
            {
                if (!_factory.TryCreate(actor, entry.ProfileId, out runtime) || runtime == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot rebuild actor '{entry.ActorId}' state-machine profile '{entry.ProfileId}' during rollback.");
                }

                if (actor.hasActorStateMachine)
                    actor.ReplaceActorStateMachine(entry.ProfileId, runtime);
                else
                    actor.AddActorStateMachine(entry.ProfileId, runtime);
            }

            runtime.RestoreSnapshot(new MobaActorStateMachineRuntimeSnapshot(
                entry.ProfileId,
                entry.DeltaTime,
                FromSerializable(entry.State),
                FromSerializable(entry.Root)));
        }

        private static MobaActorStateMachineRollbackState ToSerializable(MobaActorStateMachineState state)
        {
            return new MobaActorStateMachineRollbackState(
                state.ActiveStatePath,
                state.EnteredFrame,
                state.LastUpdatedFrame,
                state.DurationFrames,
                state.DurationSeconds);
        }

        private static MobaActorStateMachineState FromSerializable(in MobaActorStateMachineRollbackState state)
        {
            return new MobaActorStateMachineState(
                state.ActiveStatePath,
                state.EnteredFrame,
                state.LastUpdatedFrame,
                state.DurationFrames,
                state.DurationSeconds);
        }

        private static MobaHfsmSnapshotNode ToSerializable(HfsmRuntimeSnapshot snapshot)
        {
            var children = new MobaHfsmSnapshotNode[snapshot.Children.Count];
            for (var i = 0; i < children.Length; i++) children[i] = ToSerializable(snapshot.Children[i]);

            return new MobaHfsmSnapshotNode(
                (int)snapshot.Kind,
                snapshot.StateId,
                snapshot.IsActive,
                snapshot.ActiveStateId,
                snapshot.RememberedStartStateId,
                snapshot.ActionState == null ? default : ToSerializable(snapshot.ActionState),
                children);
        }

        private static MobaCompositeActionSnapshot ToSerializable(CompositeActionStateSnapshot snapshot)
        {
            return new MobaCompositeActionSnapshot(
                snapshot.ExitRequested,
                snapshot.Completed,
                (int)snapshot.LastStatus,
                ToSerializable(snapshot.Root));
        }

        private static MobaActionBehaviourSnapshot ToSerializable(ActionBehaviourSnapshot snapshot)
        {
            var children = new MobaActionBehaviourSnapshot[snapshot.Children.Count];
            for (var i = 0; i < children.Length; i++) children[i] = ToSerializable(snapshot.Children[i]);
            return new MobaActionBehaviourSnapshot(
                snapshot.Kind,
                snapshot.IntegerValue,
                snapshot.FloatValue,
                snapshot.BooleanValue,
                children);
        }

        private static HfsmRuntimeSnapshot FromSerializable(in MobaHfsmSnapshotNode snapshot)
        {
            var sourceChildren = snapshot.Children ?? Array.Empty<MobaHfsmSnapshotNode>();
            var children = new HfsmRuntimeSnapshot[sourceChildren.Length];
            for (var i = 0; i < children.Length; i++) children[i] = FromSerializable(sourceChildren[i]);

            var kind = (HfsmRuntimeSnapshotNodeKind)snapshot.Kind;
            return new HfsmRuntimeSnapshot(
                kind,
                snapshot.StateId,
                snapshot.IsActive,
                snapshot.ActiveStateId,
                snapshot.RememberedStartStateId,
                kind == HfsmRuntimeSnapshotNodeKind.CompositeActionState
                    ? FromSerializable(snapshot.ActionState)
                    : null,
                children);
        }

        private static CompositeActionStateSnapshot FromSerializable(in MobaCompositeActionSnapshot snapshot)
        {
            return new CompositeActionStateSnapshot(
                snapshot.ExitRequested,
                snapshot.Completed,
                (ActionBehaviourStatus)snapshot.LastStatus,
                FromSerializable(snapshot.Root));
        }

        private static ActionBehaviourSnapshot FromSerializable(in MobaActionBehaviourSnapshot snapshot)
        {
            var sourceChildren = snapshot.Children ?? Array.Empty<MobaActionBehaviourSnapshot>();
            var children = new ActionBehaviourSnapshot[sourceChildren.Length];
            for (var i = 0; i < children.Length; i++) children[i] = FromSerializable(sourceChildren[i]);
            return new ActionBehaviourSnapshot(
                snapshot.Kind,
                snapshot.IntegerValue,
                snapshot.FloatValue,
                snapshot.BooleanValue,
                children);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorStateMachineRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaActorStateMachineRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorStateMachineRollbackPayload(int version, MobaActorStateMachineRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorStateMachineRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly bool HasRuntime;
        [MemoryPackOrder(2)] public readonly string ProfileId;
        [MemoryPackOrder(3)] public readonly float DeltaTime;
        [MemoryPackOrder(4)] public readonly MobaActorStateMachineRollbackState State;
        [MemoryPackOrder(5)] public readonly MobaHfsmSnapshotNode Root;

        public MobaActorStateMachineRollbackEntry(
            int actorId,
            bool hasRuntime,
            string profileId,
            float deltaTime,
            MobaActorStateMachineRollbackState state,
            MobaHfsmSnapshotNode root)
        {
            ActorId = actorId;
            HasRuntime = hasRuntime;
            ProfileId = profileId ?? string.Empty;
            DeltaTime = deltaTime;
            State = state;
            Root = root;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorStateMachineRollbackState
    {
        [MemoryPackOrder(0)] public readonly string ActiveStatePath;
        [MemoryPackOrder(1)] public readonly int EnteredFrame;
        [MemoryPackOrder(2)] public readonly int LastUpdatedFrame;
        [MemoryPackOrder(3)] public readonly int DurationFrames;
        [MemoryPackOrder(4)] public readonly float DurationSeconds;

        public MobaActorStateMachineRollbackState(
            string activeStatePath,
            int enteredFrame,
            int lastUpdatedFrame,
            int durationFrames,
            float durationSeconds)
        {
            ActiveStatePath = activeStatePath ?? string.Empty;
            EnteredFrame = enteredFrame;
            LastUpdatedFrame = lastUpdatedFrame;
            DurationFrames = durationFrames;
            DurationSeconds = durationSeconds;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaHfsmSnapshotNode
    {
        [MemoryPackOrder(0)] public readonly int Kind;
        [MemoryPackOrder(1)] public readonly string StateId;
        [MemoryPackOrder(2)] public readonly bool IsActive;
        [MemoryPackOrder(3)] public readonly string ActiveStateId;
        [MemoryPackOrder(4)] public readonly string RememberedStartStateId;
        [MemoryPackOrder(5)] public readonly MobaCompositeActionSnapshot ActionState;
        [MemoryPackOrder(6)] public readonly MobaHfsmSnapshotNode[] Children;

        public MobaHfsmSnapshotNode(
            int kind,
            string stateId,
            bool isActive,
            string activeStateId,
            string rememberedStartStateId,
            MobaCompositeActionSnapshot actionState,
            MobaHfsmSnapshotNode[] children)
        {
            Kind = kind;
            StateId = stateId ?? string.Empty;
            IsActive = isActive;
            ActiveStateId = activeStateId ?? string.Empty;
            RememberedStartStateId = rememberedStartStateId ?? string.Empty;
            ActionState = actionState;
            Children = children ?? Array.Empty<MobaHfsmSnapshotNode>();
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaCompositeActionSnapshot
    {
        [MemoryPackOrder(0)] public readonly bool ExitRequested;
        [MemoryPackOrder(1)] public readonly bool Completed;
        [MemoryPackOrder(2)] public readonly int LastStatus;
        [MemoryPackOrder(3)] public readonly MobaActionBehaviourSnapshot Root;

        public MobaCompositeActionSnapshot(
            bool exitRequested,
            bool completed,
            int lastStatus,
            MobaActionBehaviourSnapshot root)
        {
            ExitRequested = exitRequested;
            Completed = completed;
            LastStatus = lastStatus;
            Root = root;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActionBehaviourSnapshot
    {
        [MemoryPackOrder(0)] public readonly string Kind;
        [MemoryPackOrder(1)] public readonly int IntegerValue;
        [MemoryPackOrder(2)] public readonly float FloatValue;
        [MemoryPackOrder(3)] public readonly bool BooleanValue;
        [MemoryPackOrder(4)] public readonly MobaActionBehaviourSnapshot[] Children;

        public MobaActionBehaviourSnapshot(
            string kind,
            int integerValue,
            float floatValue,
            bool booleanValue,
            MobaActionBehaviourSnapshot[] children)
        {
            Kind = kind ?? string.Empty;
            IntegerValue = integerValue;
            FloatValue = floatValue;
            BooleanValue = booleanValue;
            Children = children ?? Array.Empty<MobaActionBehaviourSnapshot>();
        }
    }
}
