using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaSkillRuntimeEndReason
    {
        None = 0,
        PipelineCompleted = 1,
        Cancelled = 2,
        Failed = 3,
        OwnerRemoved = 4,
        RollbackCleanup = 5,
    }

    public enum MobaSkillRuntimeChildKind
    {
        Unknown = 0,
        Effect = 1,
        Projectile = 2,
        Area = 3,
        Buff = 4,
        Summon = 5,
        Periodic = 6,
        Presentation = 7,
    }

    public enum MobaSkillRuntimeLifecycleEventKind
    {
        Created = 0,
        ChildRetained = 1,
        ChildReleased = 2,
        PipelineEnded = 3,
        WaitingChildren = 4,
        Finalizing = 5,
        Finalized = 6,
        ForceTerminated = 7,
        Cleared = 8,
    }

    public readonly struct MobaSkillRuntimeLifecycleEvent
    {
        public MobaSkillRuntimeLifecycleEvent(
            MobaSkillRuntimeLifecycleEventKind kind,
            MobaSkillCastRuntime runtime,
            in MobaSkillRuntimeChildRef child,
            in MobaSkillRuntimeRetainHandle retainHandle,
            MobaSkillRuntimeEndReason reason,
            bool forced)
        {
            Kind = kind;
            Runtime = runtime;
            Child = child;
            RetainHandle = retainHandle;
            Reason = reason;
            Forced = forced;
        }

        public MobaSkillRuntimeLifecycleEventKind Kind { get; }
        public MobaSkillCastRuntime Runtime { get; }
        public MobaSkillCastRuntimeHandle RuntimeHandle => Runtime != null ? Runtime.Handle : default;
        public MobaSkillRuntimeChildRef Child { get; }
        public MobaSkillRuntimeRetainHandle RetainHandle { get; }
        public MobaSkillRuntimeEndReason Reason { get; }
        public bool Forced { get; }
    }

    public interface IMobaSkillRuntimeLifecycleHook
    {
        void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent);
    }

    public sealed class MobaSkillRuntimeLifecycleHookService
    {
        private readonly List<IMobaSkillRuntimeLifecycleHook> _hooks = new List<IMobaSkillRuntimeLifecycleHook>(4);

        public int Count => _hooks.Count;

        public void Register(IMobaSkillRuntimeLifecycleHook hook)
        {
            if (hook == null || _hooks.Contains(hook)) return;
            _hooks.Add(hook);
        }

        public bool Unregister(IMobaSkillRuntimeLifecycleHook hook)
        {
            return hook != null && _hooks.Remove(hook);
        }

        public void Clear()
        {
            _hooks.Clear();
        }

        public void Notify(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            for (var i = 0; i < _hooks.Count; i++)
            {
                _hooks[i]?.OnSkillRuntimeLifecycle(in lifecycleEvent);
            }
        }
    }

    public readonly struct MobaSkillRuntimeDiagnostics
    {
        public MobaSkillRuntimeDiagnostics(
            in MobaSkillCastRuntimeHandle handle,
            int skillId,
            int skillSlot,
            int skillLevel,
            int sequence,
            int casterActorId,
            int targetActorId,
            SkillCastStage stage,
            bool pipelineEnded,
            bool isEnding,
            bool isEnded,
            MobaSkillRuntimeEndReason endReason,
            int pendingChildren,
            int blackboardEntryCount,
            MobaSkillRuntimeChildRef[] children)
        {
            Handle = handle;
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            Sequence = sequence;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            Stage = stage;
            PipelineEnded = pipelineEnded;
            IsEnding = isEnding;
            IsEnded = isEnded;
            EndReason = endReason;
            PendingChildren = pendingChildren;
            BlackboardEntryCount = blackboardEntryCount;
            Children = children ?? Array.Empty<MobaSkillRuntimeChildRef>();
        }

        public MobaSkillCastRuntimeHandle Handle { get; }
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int Sequence { get; }
        public int CasterActorId { get; }
        public int TargetActorId { get; }
        public SkillCastStage Stage { get; }
        public bool PipelineEnded { get; }
        public bool IsEnding { get; }
        public bool IsEnded { get; }
        public MobaSkillRuntimeEndReason EndReason { get; }
        public int PendingChildren { get; }
        public int BlackboardEntryCount { get; }
        public IReadOnlyList<MobaSkillRuntimeChildRef> Children { get; }
        public bool IsWaitingChildren => PipelineEnded && !IsEnded && PendingChildren > 0;
    }

    public readonly struct MobaSkillRuntimeBlackboardEntryDiagnostics
    {
        public MobaSkillRuntimeBlackboardEntryDiagnostics(
            in MobaSkillRuntimeBlackboardKey key,
            in MobaSkillRuntimeValue value,
            int collectionCount)
        {
            Key = key;
            Value = value;
            CollectionCount = collectionCount;
        }

        public MobaSkillRuntimeBlackboardKey Key { get; }
        public MobaSkillRuntimeValue Value { get; }
        public int CollectionCount { get; }
        public bool IsCollection =>
            Key.ValueKind == MobaSkillRuntimeValueKind.ActorIdSet ||
            Key.ValueKind == MobaSkillRuntimeValueKind.ContextIdSet;
    }

    public readonly struct MobaSkillRuntimeDetailDiagnostics
    {
        public MobaSkillRuntimeDetailDiagnostics(
            in MobaSkillRuntimeDiagnostics runtime,
            in Vec3 aimPos,
            in Vec3 aimDir,
            MobaSkillRuntimeBlackboardEntryDiagnostics[] blackboardEntries)
        {
            Runtime = runtime;
            AimPos = aimPos;
            AimDir = aimDir;
            BlackboardEntries = blackboardEntries ?? Array.Empty<MobaSkillRuntimeBlackboardEntryDiagnostics>();
        }

        public MobaSkillRuntimeDiagnostics Runtime { get; }
        public Vec3 AimPos { get; }
        public Vec3 AimDir { get; }
        public IReadOnlyList<MobaSkillRuntimeBlackboardEntryDiagnostics> BlackboardEntries { get; }
    }

    public readonly struct MobaSkillRuntimeScanResult
    {
        public MobaSkillRuntimeScanResult(int activeRuntimes, int waitingChildrenRuntimes, int pendingChildren)
        {
            ActiveRuntimes = activeRuntimes;
            WaitingChildrenRuntimes = waitingChildrenRuntimes;
            PendingChildren = pendingChildren;
        }

        public int ActiveRuntimes { get; }
        public int WaitingChildrenRuntimes { get; }
        public int PendingChildren { get; }
        public bool HasWaitingChildren => WaitingChildrenRuntimes > 0 || PendingChildren > 0;
    }

    public enum MobaSkillRuntimeBlackboardScope
    {
        Cast = 0,
        Effect = 1,
        Target = 2,
        Child = 3,
    }

    [Flags]
    public enum MobaSkillRuntimeBlackboardFlags
    {
        None = 0,
        Rollback = 1 << 0,
        Snapshot = 1 << 1,
        NetworkSync = 1 << 2,
        Debug = 1 << 3,
    }

    public enum MobaSkillRuntimeValueKind
    {
        None = 0,
        Int = 1,
        Long = 2,
        Float = 3,
        Bool = 4,
        String = 5,
        ActorId = 6,
        ContextId = 7,
        Vec3 = 8,
        ActorIdSet = 9,
        ContextIdSet = 10,
    }

    public readonly struct MobaSkillRuntimeBlackboardKey : IEquatable<MobaSkillRuntimeBlackboardKey>
    {
        public MobaSkillRuntimeBlackboardKey(
            int id,
            string name,
            MobaSkillRuntimeValueKind valueKind,
            MobaSkillRuntimeBlackboardScope scope = MobaSkillRuntimeBlackboardScope.Cast,
            MobaSkillRuntimeBlackboardFlags flags = MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug,
            int ownerModuleId = 0)
        {
            Id = id;
            Name = name ?? string.Empty;
            ValueKind = valueKind;
            Scope = scope;
            Flags = flags;
            OwnerModuleId = ownerModuleId;
        }

        public int Id { get; }
        public string Name { get; }
        public MobaSkillRuntimeValueKind ValueKind { get; }
        public MobaSkillRuntimeBlackboardScope Scope { get; }
        public MobaSkillRuntimeBlackboardFlags Flags { get; }
        public int OwnerModuleId { get; }

        public bool IsValid => Id > 0 && ValueKind != MobaSkillRuntimeValueKind.None;

        public bool Equals(MobaSkillRuntimeBlackboardKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is MobaSkillRuntimeBlackboardKey other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => string.IsNullOrEmpty(Name) ? Id.ToString() : Name;
    }

    public readonly struct MobaSkillRuntimeValue
    {
        private readonly int _intValue;
        private readonly long _longValue;
        private readonly float _floatValue;
        private readonly bool _boolValue;
        private readonly string _stringValue;
        private readonly Vec3 _vec3Value;

        private MobaSkillRuntimeValue(MobaSkillRuntimeValueKind kind, int intValue, long longValue, float floatValue, bool boolValue, string stringValue, in Vec3 vec3Value)
        {
            Kind = kind;
            _intValue = intValue;
            _longValue = longValue;
            _floatValue = floatValue;
            _boolValue = boolValue;
            _stringValue = stringValue;
            _vec3Value = vec3Value;
        }

        public MobaSkillRuntimeValueKind Kind { get; }
        public int IntValue => _intValue;
        public long LongValue => _longValue;
        public float FloatValue => _floatValue;
        public bool BoolValue => _boolValue;
        public string StringValue => _stringValue;
        public Vec3 Vec3Value => _vec3Value;

        public static MobaSkillRuntimeValue FromInt(int value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.Int, value, 0L, 0f, false, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromLong(long value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.Long, 0, value, 0f, false, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromFloat(float value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.Float, 0, 0L, value, false, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromBool(bool value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.Bool, 0, 0L, 0f, value, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromString(string value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.String, 0, 0L, 0f, false, value, Vec3.Zero);
        public static MobaSkillRuntimeValue FromActorId(int value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.ActorId, value, 0L, 0f, false, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromContextId(long value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.ContextId, 0, value, 0f, false, null, Vec3.Zero);
        public static MobaSkillRuntimeValue FromVec3(in Vec3 value) => new MobaSkillRuntimeValue(MobaSkillRuntimeValueKind.Vec3, 0, 0L, 0f, false, null, value);
    }

    public static class MobaSkillRuntimeBlackboardKeys
    {
        public static readonly MobaSkillRuntimeBlackboardKey DamagedTargets = new MobaSkillRuntimeBlackboardKey(1, "skill.damagedTargets", MobaSkillRuntimeValueKind.ActorIdSet, MobaSkillRuntimeBlackboardScope.Cast);
        public static readonly MobaSkillRuntimeBlackboardKey HitCount = new MobaSkillRuntimeBlackboardKey(2, "skill.hitCount", MobaSkillRuntimeValueKind.Int, MobaSkillRuntimeBlackboardScope.Cast);
        public static readonly MobaSkillRuntimeBlackboardKey DecayFactor = new MobaSkillRuntimeBlackboardKey(3, "skill.decayFactor", MobaSkillRuntimeValueKind.Float, MobaSkillRuntimeBlackboardScope.Cast);
        public static readonly MobaSkillRuntimeBlackboardKey LoopGuards = new MobaSkillRuntimeBlackboardKey(4, "skill.loopGuards", MobaSkillRuntimeValueKind.ContextIdSet, MobaSkillRuntimeBlackboardScope.Cast);
    }

    public sealed class MobaSkillRuntimeBlackboard
    {
        private readonly Dictionary<int, MobaSkillRuntimeBlackboardKey> _keys = new Dictionary<int, MobaSkillRuntimeBlackboardKey>();
        private readonly Dictionary<int, MobaSkillRuntimeValue> _values = new Dictionary<int, MobaSkillRuntimeValue>();
        private readonly Dictionary<int, HashSet<int>> _actorIdSets = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, HashSet<long>> _contextIdSets = new Dictionary<int, HashSet<long>>();

        public int Count => _values.Count + _actorIdSets.Count + _contextIdSets.Count;

        public bool Register(in MobaSkillRuntimeBlackboardKey key)
        {
            if (!key.IsValid) return false;
            if (_keys.TryGetValue(key.Id, out var existing))
            {
                return existing.ValueKind == key.ValueKind && existing.Scope == key.Scope;
            }

            _keys.Add(key.Id, key);
            return true;
        }

        public bool Set(in MobaSkillRuntimeBlackboardKey key, in MobaSkillRuntimeValue value)
        {
            if (!Register(in key)) return false;
            if (!IsScalarKind(key.ValueKind)) return false;
            if (!IsCompatible(key.ValueKind, value.Kind)) return false;
            _values[key.Id] = value;
            return true;
        }

        public bool TryGet(in MobaSkillRuntimeBlackboardKey key, out MobaSkillRuntimeValue value)
        {
            value = default;
            return key.IsValid && IsScalarKind(key.ValueKind) && _values.TryGetValue(key.Id, out value) && IsCompatible(key.ValueKind, value.Kind);
        }

        public bool SetInt(in MobaSkillRuntimeBlackboardKey key, int value) => Set(in key, MobaSkillRuntimeValue.FromInt(value));
        public bool SetLong(in MobaSkillRuntimeBlackboardKey key, long value) => Set(in key, MobaSkillRuntimeValue.FromLong(value));
        public bool SetFloat(in MobaSkillRuntimeBlackboardKey key, float value) => Set(in key, MobaSkillRuntimeValue.FromFloat(value));
        public bool SetBool(in MobaSkillRuntimeBlackboardKey key, bool value) => Set(in key, MobaSkillRuntimeValue.FromBool(value));
        public bool SetString(in MobaSkillRuntimeBlackboardKey key, string value) => Set(in key, MobaSkillRuntimeValue.FromString(value));
        public bool SetActorId(in MobaSkillRuntimeBlackboardKey key, int value) => Set(in key, MobaSkillRuntimeValue.FromActorId(value));
        public bool SetContextId(in MobaSkillRuntimeBlackboardKey key, long value) => Set(in key, MobaSkillRuntimeValue.FromContextId(value));
        public bool SetVec3(in MobaSkillRuntimeBlackboardKey key, in Vec3 value) => Set(in key, MobaSkillRuntimeValue.FromVec3(in value));

        public bool TryGetInt(in MobaSkillRuntimeBlackboardKey key, out int value)
        {
            value = default;
            if (!TryGet(in key, out var raw)) return false;
            if (raw.Kind != MobaSkillRuntimeValueKind.Int && raw.Kind != MobaSkillRuntimeValueKind.ActorId) return false;
            value = raw.IntValue;
            return true;
        }

        public bool TryGetLong(in MobaSkillRuntimeBlackboardKey key, out long value)
        {
            value = default;
            if (!TryGet(in key, out var raw)) return false;
            if (raw.Kind != MobaSkillRuntimeValueKind.Long && raw.Kind != MobaSkillRuntimeValueKind.ContextId) return false;
            value = raw.LongValue;
            return true;
        }

        public bool TryGetFloat(in MobaSkillRuntimeBlackboardKey key, out float value)
        {
            value = default;
            if (!TryGet(in key, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.Float) return false;
            value = raw.FloatValue;
            return true;
        }

        public bool TryGetBool(in MobaSkillRuntimeBlackboardKey key, out bool value)
        {
            value = default;
            if (!TryGet(in key, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.Bool) return false;
            value = raw.BoolValue;
            return true;
        }

        public bool TryGetString(in MobaSkillRuntimeBlackboardKey key, out string value)
        {
            value = default;
            if (!TryGet(in key, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.String) return false;
            value = raw.StringValue;
            return true;
        }

        public bool TryGetVec3(in MobaSkillRuntimeBlackboardKey key, out Vec3 value)
        {
            value = default;
            if (!TryGet(in key, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.Vec3) return false;
            value = raw.Vec3Value;
            return true;
        }

        public int AddInt(in MobaSkillRuntimeBlackboardKey key, int delta = 1)
        {
            TryGetInt(in key, out var current);
            var next = current + delta;
            SetInt(in key, next);
            return next;
        }

        public float MultiplyFloat(in MobaSkillRuntimeBlackboardKey key, float factor, float defaultValue = 1f)
        {
            if (!TryGetFloat(in key, out var current)) current = defaultValue;
            var next = current * factor;
            SetFloat(in key, next);
            return next;
        }

        public bool AddActorId(in MobaSkillRuntimeBlackboardKey key, int actorId)
        {
            if (actorId <= 0) return false;
            if (!RegisterSetKey(in key, MobaSkillRuntimeValueKind.ActorIdSet)) return false;
            if (!_actorIdSets.TryGetValue(key.Id, out var set))
            {
                set = new HashSet<int>();
                _actorIdSets.Add(key.Id, set);
            }

            return set.Add(actorId);
        }

        public bool ContainsActorId(in MobaSkillRuntimeBlackboardKey key, int actorId)
        {
            return actorId > 0 && key.ValueKind == MobaSkillRuntimeValueKind.ActorIdSet && _actorIdSets.TryGetValue(key.Id, out var set) && set.Contains(actorId);
        }

        public int GetActorIdCount(in MobaSkillRuntimeBlackboardKey key)
        {
            return key.ValueKind == MobaSkillRuntimeValueKind.ActorIdSet && _actorIdSets.TryGetValue(key.Id, out var set) ? set.Count : 0;
        }

        public bool AddContextId(in MobaSkillRuntimeBlackboardKey key, long contextId)
        {
            if (contextId == 0L) return false;
            if (!RegisterSetKey(in key, MobaSkillRuntimeValueKind.ContextIdSet)) return false;
            if (!_contextIdSets.TryGetValue(key.Id, out var set))
            {
                set = new HashSet<long>();
                _contextIdSets.Add(key.Id, set);
            }

            return set.Add(contextId);
        }

        public bool ContainsContextId(in MobaSkillRuntimeBlackboardKey key, long contextId)
        {
            return contextId != 0L && key.ValueKind == MobaSkillRuntimeValueKind.ContextIdSet && _contextIdSets.TryGetValue(key.Id, out var set) && set.Contains(contextId);
        }

        public int GetContextIdCount(in MobaSkillRuntimeBlackboardKey key)
        {
            return key.ValueKind == MobaSkillRuntimeValueKind.ContextIdSet && _contextIdSets.TryGetValue(key.Id, out var set) ? set.Count : 0;
        }

        public bool Remove(in MobaSkillRuntimeBlackboardKey key)
        {
            if (!key.IsValid) return false;
            var removed = _values.Remove(key.Id);
            removed |= _actorIdSets.Remove(key.Id);
            removed |= _contextIdSets.Remove(key.Id);
            return removed;
        }

        public void Clear()
        {
            _values.Clear();
            _actorIdSets.Clear();
            _contextIdSets.Clear();
        }

        public int CopyDiagnosticsTo(List<MobaSkillRuntimeBlackboardEntryDiagnostics> results)
        {
            if (results == null) return 0;

            var start = results.Count;
            foreach (var pair in _keys)
            {
                var key = pair.Value;
                if (_values.TryGetValue(key.Id, out var value))
                {
                    results.Add(new MobaSkillRuntimeBlackboardEntryDiagnostics(key, value, 0));
                    continue;
                }

                if (_actorIdSets.TryGetValue(key.Id, out var actors))
                {
                    results.Add(new MobaSkillRuntimeBlackboardEntryDiagnostics(key, default, actors.Count));
                    continue;
                }

                if (_contextIdSets.TryGetValue(key.Id, out var contexts))
                {
                    results.Add(new MobaSkillRuntimeBlackboardEntryDiagnostics(key, default, contexts.Count));
                }
            }

            return results.Count - start;
        }

        private bool RegisterSetKey(in MobaSkillRuntimeBlackboardKey key, MobaSkillRuntimeValueKind expectedKind)
        {
            return key.ValueKind == expectedKind && Register(in key);
        }

        private static bool IsScalarKind(MobaSkillRuntimeValueKind kind)
        {
            return kind != MobaSkillRuntimeValueKind.ActorIdSet && kind != MobaSkillRuntimeValueKind.ContextIdSet;
        }

        private static bool IsCompatible(MobaSkillRuntimeValueKind keyKind, MobaSkillRuntimeValueKind valueKind)
        {
            return keyKind == valueKind;
        }
    }

    public interface IMobaSkillRuntimeStateSlot
    {
        int SlotId { get; }
        void OnRuntimeEnding(MobaSkillCastRuntime runtime, MobaSkillRuntimeEndReason reason);
    }

    public readonly struct MobaSkillRuntimeStateSlotKey<TState> where TState : class, IMobaSkillRuntimeStateSlot
    {
        public MobaSkillRuntimeStateSlotKey(int slotId, string name)
        {
            SlotId = slotId;
            Name = name ?? string.Empty;
        }

        public int SlotId { get; }
        public string Name { get; }
        public bool IsValid => SlotId > 0;
    }

    public readonly struct MobaSkillRuntimeChildRef : IEquatable<MobaSkillRuntimeChildRef>
    {
        public MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind kind, long childId, long traceContextId = 0L, int configId = 0)
        {
            Kind = kind;
            ChildId = childId;
            TraceContextId = traceContextId;
            ConfigId = configId;
        }

        public MobaSkillRuntimeChildKind Kind { get; }
        public long ChildId { get; }
        public long TraceContextId { get; }
        public int ConfigId { get; }
        public bool IsValid => Kind != MobaSkillRuntimeChildKind.Unknown && ChildId != 0L;

        public bool Equals(MobaSkillRuntimeChildRef other) => Kind == other.Kind && ChildId == other.ChildId;
        public override bool Equals(object obj) => obj is MobaSkillRuntimeChildRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ ChildId.GetHashCode();
        public override string ToString() => IsValid ? Kind + ":" + ChildId + "@" + TraceContextId + "#" + ConfigId : "Invalid";
    }

    public readonly struct MobaSkillCastRuntimeHandle : IEquatable<MobaSkillCastRuntimeHandle>
    {
        public MobaSkillCastRuntimeHandle(long runtimeId, int generation, long rootTraceContextId)
        {
            RuntimeId = runtimeId;
            Generation = generation;
            RootTraceContextId = rootTraceContextId;
        }

        public long RuntimeId { get; }
        public int Generation { get; }
        public long RootTraceContextId { get; }
        public bool IsValid => RuntimeId != 0L && Generation > 0;

        public bool Equals(MobaSkillCastRuntimeHandle other) => RuntimeId == other.RuntimeId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is MobaSkillCastRuntimeHandle other && Equals(other);
        public override int GetHashCode() => (RuntimeId.GetHashCode() * 397) ^ Generation;
        public override string ToString() => IsValid ? RuntimeId + ":" + Generation : "Invalid";
    }

    public readonly struct MobaSkillRuntimeRetainHandle : IEquatable<MobaSkillRuntimeRetainHandle>
    {
        public MobaSkillRuntimeRetainHandle(long retainId, in MobaSkillCastRuntimeHandle runtime, in MobaSkillRuntimeChildRef child)
        {
            RetainId = retainId;
            Runtime = runtime;
            Child = child;
        }

        public long RetainId { get; }
        public MobaSkillCastRuntimeHandle Runtime { get; }
        public MobaSkillRuntimeChildRef Child { get; }
        public bool IsValid => RetainId != 0L && Runtime.IsValid && Child.IsValid;

        public bool Equals(MobaSkillRuntimeRetainHandle other) => RetainId == other.RetainId && Runtime.Equals(other.Runtime);
        public override bool Equals(object obj) => obj is MobaSkillRuntimeRetainHandle other && Equals(other);
        public override int GetHashCode() => (RetainId.GetHashCode() * 397) ^ Runtime.GetHashCode();
        public override string ToString() => IsValid ? RetainId + "->" + Runtime + "/" + Child : "Invalid";
    }

    public sealed class MobaSkillCastRuntime : IMobaContextSourceProvider
    {
        private readonly List<MobaSkillRuntimeChildRef> _children = new List<MobaSkillRuntimeChildRef>(8);
        private readonly Dictionary<int, IMobaSkillRuntimeStateSlot> _stateSlots = new Dictionary<int, IMobaSkillRuntimeStateSlot>();

        internal MobaSkillCastRuntime(long runtimeId, int generation, in MobaSkillCastRuntimeCreateRequest request)
        {
            RuntimeId = runtimeId;
            Generation = generation;
            RootTraceContextId = request.RootTraceContextId;
            SkillId = request.SkillId;
            SkillSlot = request.SkillSlot;
            SkillLevel = request.SkillLevel;
            Sequence = request.Sequence;
            CasterActorId = request.CasterActorId;
            TargetActorId = request.TargetActorId;
            AimPos = request.AimPos;
            AimDir = request.AimDir;
            Stage = SkillCastStage.PreCast;
        }

        public long RuntimeId { get; }
        public int Generation { get; }
        public long RootTraceContextId { get; internal set; }
        public MobaSkillCastRuntimeHandle Handle => new MobaSkillCastRuntimeHandle(RuntimeId, Generation, RootTraceContextId);
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int Sequence { get; }
        public int CasterActorId { get; }
        public int TargetActorId { get; private set; }
        public Vec3 AimPos { get; private set; }
        public Vec3 AimDir { get; private set; }
        public SkillCastStage Stage { get; internal set; }
        public bool PipelineEnded { get; internal set; }
        public bool IsEnding { get; internal set; }
        public bool IsEnded { get; internal set; }
        public MobaSkillRuntimeEndReason EndReason { get; internal set; }
        public int PendingChildren => _children.Count;
        public MobaSkillRuntimeBlackboard Blackboard { get; } = new MobaSkillRuntimeBlackboard();
        public IReadOnlyList<MobaSkillRuntimeChildRef> Children => _children;

        public void UpdateInput(in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            if (!aimPos.Equals(Vec3.Zero)) AimPos = aimPos;
            if (!aimDir.Equals(Vec3.Zero)) AimDir = aimDir;
            if (targetActorId > 0) TargetActorId = targetActorId;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            var sourceContextId = RootTraceContextId != 0 ? RootTraceContextId : RuntimeId;
            source = new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime,
                EffectContextKind.Skill,
                MobaTraceKind.SkillCast,
                CasterActorId,
                TargetActorId,
                sourceContextId,
                sourceContextId,
                RootTraceContextId != 0 ? RootTraceContextId : sourceContextId,
                RuntimeId,
                SkillId,
                0,
                0,
                "Skill",
                SkillId,
                true,
                Handle);
            return source.IsValid;
        }

        internal bool RetainChild(in MobaSkillRuntimeChildRef child)
        {
            if (!child.IsValid) return false;
            if (_children.Contains(child)) return false;
            _children.Add(child);
            return true;
        }

        internal bool ReleaseChild(in MobaSkillRuntimeChildRef child)
        {
            if (!child.IsValid) return false;
            return _children.Remove(child);
        }

        public MobaSkillRuntimeDiagnostics CreateDiagnosticsSnapshot()
        {
            var children = _children.Count == 0 ? Array.Empty<MobaSkillRuntimeChildRef>() : _children.ToArray();
            return new MobaSkillRuntimeDiagnostics(
                Handle,
                SkillId,
                SkillSlot,
                SkillLevel,
                Sequence,
                CasterActorId,
                TargetActorId,
                Stage,
                PipelineEnded,
                IsEnding,
                IsEnded,
                EndReason,
                _children.Count,
                Blackboard.Count,
                children);
        }

        public MobaSkillRuntimeDetailDiagnostics CreateDetailDiagnosticsSnapshot()
        {
            var entries = new List<MobaSkillRuntimeBlackboardEntryDiagnostics>(Blackboard.Count);
            Blackboard.CopyDiagnosticsTo(entries);
            return new MobaSkillRuntimeDetailDiagnostics(
                CreateDiagnosticsSnapshot(),
                AimPos,
                AimDir,
                entries.Count == 0
                    ? Array.Empty<MobaSkillRuntimeBlackboardEntryDiagnostics>()
                    : entries.ToArray());
        }

        public int CopyChildrenTo(List<MobaSkillRuntimeChildRef> results, MobaSkillRuntimeChildKind kind = MobaSkillRuntimeChildKind.Unknown)
        {
            if (results == null) return 0;
            var start = results.Count;
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (kind != MobaSkillRuntimeChildKind.Unknown && child.Kind != kind) continue;
                results.Add(child);
            }

            return results.Count - start;
        }

        public int CountChildren(MobaSkillRuntimeChildKind kind)
        {
            if (kind == MobaSkillRuntimeChildKind.Unknown) return _children.Count;
            var count = 0;
            for (var i = 0; i < _children.Count; i++)
            {
                if (_children[i].Kind == kind) count++;
            }

            return count;
        }

        public bool SetState<TState>(in MobaSkillRuntimeStateSlotKey<TState> key, TState state) where TState : class, IMobaSkillRuntimeStateSlot
        {
            if (!key.IsValid || state == null) return false;
            if (state.SlotId != key.SlotId) return false;
            _stateSlots[key.SlotId] = state;
            return true;
        }

        public bool TryGetState<TState>(in MobaSkillRuntimeStateSlotKey<TState> key, out TState state) where TState : class, IMobaSkillRuntimeStateSlot
        {
            state = null;
            if (!key.IsValid) return false;
            if (!_stateSlots.TryGetValue(key.SlotId, out var raw)) return false;
            state = raw as TState;
            return state != null;
        }

        internal void NotifyEnding(MobaSkillRuntimeEndReason reason)
        {
            foreach (var slot in _stateSlots.Values)
            {
                slot?.OnRuntimeEnding(this, reason);
            }
        }
    }

    public readonly struct MobaSkillCastRuntimeCreateRequest
    {
        public MobaSkillCastRuntimeCreateRequest(
            int skillId,
            int skillSlot,
            int skillLevel,
            int sequence,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            long rootTraceContextId)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            Sequence = sequence;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            RootTraceContextId = rootTraceContextId;
        }

        public int SkillId { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int Sequence { get; }
        public int CasterActorId { get; }
        public int TargetActorId { get; }
        public Vec3 AimPos { get; }
        public Vec3 AimDir { get; }
        public long RootTraceContextId { get; }
    }
}
