using System;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerExecutionSnapshotBuilder
    {
        private EffectContextKind _kind;
        private int _sourceActorId;
        private int _targetActorId;
        private long _sourceContextId;
        private long _rootContextId;
        private long _ownerContextId;
        private int _triggerId;
        private int _configId;
        private int _frame;
        private MobaSkillCastRuntimeHandle _skillRuntimeHandle;

        private MobaTriggerExecutionSnapshotBuilder()
        {
        }

        public static MobaTriggerExecutionSnapshotBuilder Create()
        {
            return new MobaTriggerExecutionSnapshotBuilder();
        }

        public MobaTriggerExecutionSnapshotBuilder FromLineage(in MobaEffectLineageInput lineageInput)
        {
            if (lineageInput.ContextKind != EffectContextKind.Unknown) _kind = lineageInput.ContextKind;
            MergeIdentity(ref _sourceActorId, lineageInput.SourceActorId, "sourceActorId");
            if (lineageInput.TargetActorId != 0) _targetActorId = lineageInput.TargetActorId;
            MergeIdentity(ref _sourceContextId, lineageInput.ParentContextId, "sourceContextId");
            MergeIdentity(ref _rootContextId, lineageInput.EffectiveRootContextId, "rootContextId");
            MergeIdentity(ref _ownerContextId, lineageInput.OwnerContextId, "ownerContextId");
            if (lineageInput.OriginConfigId != 0) _configId = lineageInput.OriginConfigId;
            return this;
        }

        public MobaTriggerExecutionSnapshotBuilder FromPayload(object payload)
        {
            if (payload == null) return this;

            if (payload.TryResolveExecutionSnapshot(out var snapshot))
            {
                FromSnapshot(in snapshot);
            }

            if (payload is IMobaTriggerSkillRuntimeContext skillRuntimeContext
                && skillRuntimeContext.TryGetSkillRuntimeHandle(out var handle)
                && handle.IsValid)
            {
                MergeSkillRuntimeHandle(in handle);
            }

            return this;
        }

        public MobaTriggerExecutionSnapshotBuilder FromSnapshot(in MobaTriggerExecutionSnapshot snapshot)
        {
            if (!snapshot.IsValid) return this;
            if (snapshot.Kind != EffectContextKind.Unknown) _kind = snapshot.Kind;
            MergeIdentity(ref _sourceActorId, snapshot.SourceActorId, "sourceActorId");
            if (snapshot.TargetActorId != 0) _targetActorId = snapshot.TargetActorId;
            MergeIdentity(ref _sourceContextId, snapshot.SourceContextId, "sourceContextId");
            MergeIdentity(ref _rootContextId, snapshot.RootContextId, "rootContextId");
            MergeIdentity(ref _ownerContextId, snapshot.OwnerContextId, "ownerContextId");
            if (snapshot.TriggerId != 0) _triggerId = snapshot.TriggerId;
            if (snapshot.ConfigId != 0) _configId = snapshot.ConfigId;
            if (snapshot.Frame != 0) _frame = snapshot.Frame;
            var skillRuntimeHandle = snapshot.SkillRuntimeHandle;
            if (skillRuntimeHandle.IsValid) MergeSkillRuntimeHandle(in skillRuntimeHandle);
            return this;
        }

        public MobaTriggerExecutionSnapshotBuilder WithTrigger(int triggerId, int configId)
        {
            if (triggerId != 0) _triggerId = triggerId;
            if (configId != 0) _configId = configId;
            return this;
        }

        public MobaTriggerExecutionSnapshotBuilder WithFrame(int frame)
        {
            if (frame != 0) _frame = frame;
            return this;
        }

        public MobaTriggerExecutionSnapshotBuilder WithFrameIfMissing(int frame)
        {
            if (_frame == 0 && frame != 0) _frame = frame;
            return this;
        }

        private static void MergeIdentity(ref int current, int incoming, string fieldName)
        {
            if (incoming == 0) return;
            if (current != 0 && current != incoming)
            {
                throw CreateConflict(fieldName, current, incoming);
            }

            current = incoming;
        }

        private static void MergeIdentity(ref long current, long incoming, string fieldName)
        {
            if (incoming == 0L) return;
            if (current != 0L && current != incoming)
            {
                throw CreateConflict(fieldName, current, incoming);
            }

            current = incoming;
        }

        private void MergeSkillRuntimeHandle(in MobaSkillCastRuntimeHandle incoming)
        {
            if (!incoming.IsValid) return;
            if (_skillRuntimeHandle.IsValid && !_skillRuntimeHandle.Equals(incoming))
            {
                throw CreateConflict("skillRuntimeHandle", _skillRuntimeHandle, incoming);
            }

            _skillRuntimeHandle = incoming;
        }

        private static InvalidOperationException CreateConflict(string fieldName, object current, object incoming)
        {
            return new InvalidOperationException(
                $"[MobaTriggerExecutionSnapshotBuilder] Conflicting execution provenance. field={fieldName}, current={current}, incoming={incoming}.");
        }

        public MobaTriggerExecutionSnapshot Build()
        {
            return new MobaTriggerExecutionSnapshot(
                _kind,
                _sourceActorId,
                _targetActorId,
                _sourceContextId,
                _rootContextId,
                _ownerContextId,
                _triggerId,
                _configId,
                _frame,
                _skillRuntimeHandle);
        }
    }
}
