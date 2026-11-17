namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaTriggerExecutionSnapshotProvider
    {
        bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot);
    }

    public readonly struct MobaTriggerExecutionSnapshot
    {
        public MobaTriggerExecutionSnapshot(
            EffectContextKind kind,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int triggerId,
            int configId,
            int frame,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            MobaSummonInputIdentity summonInputIdentity = default)
        {
            Kind = kind;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            TriggerId = triggerId;
            ConfigId = configId;
            Frame = frame;
            SkillRuntimeHandle = skillRuntimeHandle;
            SummonInputIdentity = summonInputIdentity;
        }

        public EffectContextKind Kind { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int TriggerId { get; }
        public int ConfigId { get; }
        public int Frame { get; }
        public MobaSkillCastRuntimeHandle SkillRuntimeHandle { get; }
        public MobaSummonInputIdentity SummonInputIdentity { get; }

        public bool IsValid => Kind != EffectContextKind.Unknown || SourceActorId != 0 || TargetActorId != 0 || SourceContextId != 0 || RootContextId != 0 || OwnerContextId != 0 || TriggerId != 0 || ConfigId != 0 || Frame != 0 || SkillRuntimeHandle.IsValid;
        public bool HasExecutionSource => SourceActorId > 0 && SourceContextId != 0;
        public long EffectiveRootContextId => RootContextId != 0 ? RootContextId : SourceContextId;

        public MobaTriggerExecutionSnapshot WithTrigger(int triggerId, int configId)
        {
            return new MobaTriggerExecutionSnapshot(
                Kind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId,
                OwnerContextId,
                triggerId != 0 ? triggerId : TriggerId,
                configId != 0 ? configId : ConfigId,
                Frame,
                SkillRuntimeHandle,
                SummonInputIdentity);
        }

        public MobaTriggerExecutionSnapshot WithFrame(int frame)
        {
            return new MobaTriggerExecutionSnapshot(
                Kind,
                SourceActorId,
                TargetActorId,
                SourceContextId,
                RootContextId,
                OwnerContextId,
                TriggerId,
                ConfigId,
                frame != 0 ? frame : Frame,
                SkillRuntimeHandle,
                SummonInputIdentity);
        }

        public MobaTriggerExecutionSnapshot WithSummonInputIdentity(in MobaSummonInputIdentity identity)
        {
            return new MobaTriggerExecutionSnapshot(
                Kind, SourceActorId, TargetActorId, SourceContextId, RootContextId,
                OwnerContextId, TriggerId, ConfigId, Frame, SkillRuntimeHandle, identity);
        }
    }

}
