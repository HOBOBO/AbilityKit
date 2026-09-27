using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Triggering
{
    /// <summary>
    /// owner-bound 触发器执行门控：用于执行前必须检查的运行时状态，以及触发器成功返回后才提交的状态变更。
    /// </summary>
    public interface IMobaOwnerBoundTriggerGate
    {
        bool IsMatch(long ownerKey, int triggerId);
        bool CanExecute(long ownerKey, int triggerId);
        void Complete(long ownerKey, int triggerId);
    }

    public interface IMobaOwnerBoundTriggerExecutionSourceProvider
    {
        bool TryGetExecutionSource(long ownerKey, int triggerId, out MobaOwnerBoundTriggerExecutionSource source);
    }

    public readonly struct MobaOwnerBoundTriggerExecutionSource
    {
        public MobaOwnerBoundTriggerExecutionSource(
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            int sourceConfigId)
        {
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SourceConfigId = sourceConfigId;
        }

        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int SourceConfigId { get; }
        public bool HasExecutionSource => SourceActorId > 0 && SourceContextId != 0;

        public MobaEffectLineageInput ToLineageInput()
        {
            return new MobaEffectLineageInput(
                EffectContextKind.Trigger,
                MobaExecutionKind.EffectExecution,
                SourceActorId,
                TargetActorId > 0 ? TargetActorId : SourceActorId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId != 0 ? OwnerContextId : SourceContextId,
                SourceConfigId);
        }
    }
}
