using System;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaRuntimeContextLifecycleState
    {
        None = 0,
        Active = 1,
        Refreshed = 2,
        Interval = 3,
        Ended = 4,
        Destroyed = 5
    }

    public readonly struct MobaBuffRuntimeContextData
    {
        public MobaBuffRuntimeContextData(
            int buffId,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int stackCount,
            float remainingSeconds,
            float intervalRemainingSeconds,
            MobaRuntimeContextLifecycleState lifecycleState,
            int frame,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            BuffId = buffId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            StackCount = stackCount;
            RemainingSeconds = remainingSeconds;
            IntervalRemainingSeconds = intervalRemainingSeconds;
            LifecycleState = lifecycleState;
            Frame = frame;
            SkillRuntimeHandle = skillRuntimeHandle;
        }

        public int BuffId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int StackCount { get; }
        public float RemainingSeconds { get; }
        public float IntervalRemainingSeconds { get; }
        public MobaRuntimeContextLifecycleState LifecycleState { get; }
        public int Frame { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }

        public bool TryGetValue<T>(long contextId, long version, string key, out T value)
        {
            object raw = null;
            switch (key)
            {
                case MobaRuntimeContextKeys.ContextId:
                    raw = contextId;
                    break;
                case MobaRuntimeContextKeys.Version:
                    raw = version;
                    break;
                case MobaRuntimeContextKeys.BuffId:
                    raw = BuffId;
                    break;
                case MobaRuntimeContextKeys.SourceActorId:
                    raw = SourceActorId;
                    break;
                case MobaRuntimeContextKeys.TargetActorId:
                    raw = TargetActorId;
                    break;
                case MobaRuntimeContextKeys.SourceContextId:
                    raw = SourceContextId;
                    break;
                case MobaRuntimeContextKeys.RootContextId:
                    raw = RootContextId;
                    break;
                case MobaRuntimeContextKeys.OwnerContextId:
                    raw = OwnerContextId;
                    break;
                case MobaRuntimeContextKeys.StackCount:
                    raw = StackCount;
                    break;
                case MobaRuntimeContextKeys.RemainingSeconds:
                    raw = RemainingSeconds;
                    break;
                case MobaRuntimeContextKeys.IntervalRemainingSeconds:
                    raw = IntervalRemainingSeconds;
                    break;
                case MobaRuntimeContextKeys.LifecycleState:
                    raw = LifecycleState;
                    break;
                case MobaRuntimeContextKeys.Frame:
                    raw = Frame;
                    break;
                case MobaRuntimeContextKeys.SkillRuntimeHandle:
                    raw = SkillRuntimeHandle;
                    break;
            }

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public static MobaBuffRuntimeContextData FromRuntime(BuffRuntime runtime, int targetActorId, int frame, MobaRuntimeContextLifecycleState state)
        {
            var origin = runtime != null ? runtime.Origin : default;
            return new MobaBuffRuntimeContextData(
                runtime != null ? runtime.BuffId : 0,
                runtime != null ? runtime.SourceId : 0,
                targetActorId,
                runtime != null ? runtime.SourceContextId : 0L,
                origin.EffectiveRootContextId,
                origin.OwnerContextId,
                runtime != null ? runtime.StackCount : 0,
                runtime != null ? runtime.Remaining : 0f,
                runtime != null ? runtime.IntervalRemainingSeconds : 0f,
                state,
                frame,
                runtime != null ? runtime.SkillRuntimeHandle : default);
        }
    }

    public sealed class MobaBuffContextProperty : IProperty, IContextValueProvider
    {
        public MobaBuffContextProperty(in MobaBuffRuntimeContextData data, long version = 1L)
        {
            BuffId = data.BuffId;
            SourceActorId = data.SourceActorId;
            TargetActorId = data.TargetActorId;
            SourceContextId = data.SourceContextId;
            RootContextId = data.RootContextId;
            OwnerContextId = data.OwnerContextId;
            StackCount = data.StackCount;
            RemainingSeconds = data.RemainingSeconds;
            IntervalRemainingSeconds = data.IntervalRemainingSeconds;
            LifecycleState = data.LifecycleState;
            Frame = data.Frame;
            SkillRuntimeHandle = data.SkillRuntimeHandle;
            Version = version;
        }

        public int TypeId => PropertyTypeRegistry.Instance.Register<MobaBuffContextProperty>().Id;
        public long ContextId { get; private set; }
        public long Version { get; }
        public int BuffId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int StackCount { get; }
        public float RemainingSeconds { get; }
        public float IntervalRemainingSeconds { get; }
        public MobaRuntimeContextLifecycleState LifecycleState { get; }
        public int Frame { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }

        public void SetContextId(long contextId)
        {
            ContextId = contextId;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            var data = new MobaBuffRuntimeContextData(
                BuffId,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId,
                OwnerContextId,
                StackCount,
                RemainingSeconds,
                IntervalRemainingSeconds,
                LifecycleState,
                Frame,
                SkillRuntimeHandle);
            return data.TryGetValue(ContextId, Version, key, out value);
        }

        public static MobaBuffContextProperty FromRuntime(BuffRuntime runtime, int targetActorId, int frame, MobaRuntimeContextLifecycleState state)
        {
            var data = MobaBuffRuntimeContextData.FromRuntime(runtime, targetActorId, frame, state);
            var property = new MobaBuffContextProperty(in data, runtime != null ? runtime.RuntimeContextVersion : 1L);
            if (runtime != null)
                property.SetContextId(runtime.RuntimeContextId);

            return property;
        }
    }

    public sealed class MobaBuffContextSnapshot : IImmutableContextSnapshot, ISnapshotAccessor, ISourceContext, IOwnerContext
    {
        private readonly MobaBuffRuntimeContextData _data;

        public MobaBuffContextSnapshot(MobaBuffContextProperty property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            EntityId = property.ContextId;
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Version = property.Version;
            Frame = property.Frame;
            _data = new MobaBuffRuntimeContextData(
                property.BuffId,
                property.SourceActorId,
                property.TargetActorId,
                property.SourceContextId,
                property.RootContextId,
                property.OwnerContextId,
                property.StackCount,
                property.RemainingSeconds,
                property.IntervalRemainingSeconds,
                property.LifecycleState,
                property.Frame,
                property.SkillRuntimeHandle);
        }

        private MobaBuffContextSnapshot(long contextId, long version, int frame, in MobaBuffRuntimeContextData data)
        {
            EntityId = contextId;
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Version = version;
            Frame = frame;
            _data = data;
        }

        public static MobaBuffContextSnapshot FromRuntime(BuffRuntime runtime, int targetActorId, int frame, MobaRuntimeContextLifecycleState state)
        {
            var data = MobaBuffRuntimeContextData.FromRuntime(runtime, targetActorId, frame, state);
            return new MobaBuffContextSnapshot(
                runtime != null ? runtime.RuntimeContextId : 0L,
                runtime != null ? runtime.RuntimeContextVersion : 0L,
                frame,
                in data);
        }

        public long EntityId { get; }
        public long CreatedAtMs { get; }
        public long Version { get; }
        public int Frame { get; }
        public long SourceEntityId => _data.SourceContextId;
        public long OwnerEntityId => _data.OwnerContextId;
        public bool IsRealtimeAvailable => false;
        public bool IsDestroyed => _data.LifecycleState == MobaRuntimeContextLifecycleState.Ended ||
            _data.LifecycleState == MobaRuntimeContextLifecycleState.Destroyed;

        public T GetValue<T>(string key, T snapshotDefault = default)
        {
            return TryGetValue(key, out T value) ? value : snapshotDefault;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return _data.TryGetValue(EntityId, Version, key, out value);
        }
    }

    public static class MobaRuntimeContextKeys
    {
        public const string ContextId = "ContextId";
        public const string Version = "Version";
        public const string BuffId = "BuffId";
        public const string SourceActorId = "SourceActorId";
        public const string TargetActorId = "TargetActorId";
        public const string SourceContextId = "SourceContextId";
        public const string RootContextId = "RootContextId";
        public const string OwnerContextId = "OwnerContextId";
        public const string StackCount = "StackCount";
        public const string RemainingSeconds = "RemainingSeconds";
        public const string IntervalRemainingSeconds = "IntervalRemainingSeconds";
        public const string LifecycleState = "LifecycleState";
        public const string Frame = "Frame";
        public const string SkillRuntimeHandle = "SkillRuntimeHandle";
    }
}
