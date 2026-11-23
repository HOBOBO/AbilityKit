namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaEffectExecutionScopeSnapshot
    {
        public MobaEffectExecutionScopeSnapshot(
            long effectContextId,
            int effectConfigId,
            int triggerId,
            int sourceActorId,
            int targetActorId,
            bool isRoot,
            int currentActionIndex,
            long currentActionContextId,
            long currentActionId)
        {
            EffectContextId = effectContextId;
            EffectConfigId = effectConfigId;
            TriggerId = triggerId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            IsRoot = isRoot;
            CurrentActionIndex = currentActionIndex;
            CurrentActionContextId = currentActionContextId;
            CurrentActionId = currentActionId;
        }
 
        public long EffectContextId { get; }
        public int EffectConfigId { get; }
        public int TriggerId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public bool IsRoot { get; }
        public int CurrentActionIndex { get; }
        public long CurrentActionContextId { get; }
        public long CurrentActionId { get; }
    }
}
