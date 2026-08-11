namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaGameplayOriginBuilder
    {
        private int _sourceActorId;
        private int _targetActorId;
        private MobaTraceKind _immediateKind;
        private int _immediateConfigId;
        private long _immediateContextId;
        private long _parentContextId;
        private long _rootContextId;
        private long _ownerContextId;
        private MobaSkillCastRuntimeHandle _skillRuntimeHandle;

        private MobaGameplayOriginBuilder()
        {
            Reset();
        }

        public static MobaGameplayOriginBuilder Create()
        {
            return new MobaGameplayOriginBuilder();
        }

        public MobaGameplayOriginBuilder Reset()
        {
            _sourceActorId = 0;
            _targetActorId = 0;
            _immediateKind = MobaTraceKind.None;
            _immediateConfigId = 0;
            _immediateContextId = 0L;
            _parentContextId = 0L;
            _rootContextId = 0L;
            _ownerContextId = 0L;
            _skillRuntimeHandle = default;
            return this;
        }

        public MobaGameplayOriginBuilder FromOrigin(in MobaGameplayOrigin origin)
        {
            if (!origin.IsValid) return this;

            _sourceActorId = origin.SourceActorId;
            _targetActorId = origin.TargetActorId;
            _immediateKind = origin.ImmediateKind;
            _immediateConfigId = origin.ImmediateConfigId;
            _immediateContextId = origin.ImmediateContextId;
            _parentContextId = origin.ParentContextId;
            _rootContextId = origin.RootContextId;
            _ownerContextId = origin.OwnerContextId;
            _skillRuntimeHandle = origin.SkillRuntimeHandle;
            return this;
        }

        public MobaGameplayOriginBuilder FromLineageContext(in MobaTriggerLineageContext lineageContext)
        {
            _sourceActorId = lineageContext.SourceActorId;
            _targetActorId = lineageContext.TargetActorId;
            _immediateKind = lineageContext.OriginKind;
            _immediateConfigId = lineageContext.SourceConfigId;
            _immediateContextId = lineageContext.SourceContextId;
            _parentContextId = lineageContext.SourceContextId;
            _rootContextId = lineageContext.RootContextId;
            _ownerContextId = lineageContext.OwnerContextId;
            return this;
        }

        public MobaGameplayOriginBuilder WithActors(int sourceActorId, int targetActorId)
        {
            _sourceActorId = sourceActorId;
            _targetActorId = targetActorId;
            return this;
        }

        public MobaGameplayOriginBuilder WithImmediate(MobaTraceKind kind, int configId, long contextId)
        {
            _immediateKind = kind;
            _immediateConfigId = configId;
            _immediateContextId = contextId;
            if (contextId != 0L) _parentContextId = contextId;
            return this;
        }

        public MobaGameplayOriginBuilder WithLifecycleNode(MobaTraceKind kind, int configId, long contextId)
        {
            if (contextId == 0L)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(contextId),
                    "A formal lifecycle node requires a non-zero trace context id.");
            }

            _immediateKind = kind;
            _immediateConfigId = configId;
            _immediateContextId = contextId;
            _parentContextId = contextId;
            if (_rootContextId == 0L) _rootContextId = contextId;
            if (_ownerContextId == 0L) _ownerContextId = contextId;
            return this;
        }

        public MobaGameplayOriginBuilder WithRootContext(long rootContextId)
        {
            _rootContextId = rootContextId;
            return this;
        }

        public MobaGameplayOriginBuilder WithOwnerContext(long ownerContextId)
        {
            _ownerContextId = ownerContextId;
            return this;
        }

        public MobaGameplayOriginBuilder WithSkillRuntime(in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            _skillRuntimeHandle = skillRuntimeHandle;
            return this;
        }

        public MobaGameplayOriginBuilder WithSkillRuntimeIfMissing(in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            if (!_skillRuntimeHandle.IsValid && skillRuntimeHandle.IsValid)
            {
                _skillRuntimeHandle = skillRuntimeHandle;
            }

            return this;
        }

        public MobaGameplayOriginBuilder CompleteDefaults()
        {
            if (_parentContextId == 0L) _parentContextId = _immediateContextId;
            if (_rootContextId == 0L) _rootContextId = _parentContextId != 0L ? _parentContextId : _immediateContextId;
            return this;
        }

        public MobaGameplayOrigin Build()
        {
            CompleteDefaults();
            return new MobaGameplayOrigin(
                _sourceActorId,
                _targetActorId,
                _immediateKind,
                _immediateConfigId,
                _immediateContextId,
                _parentContextId,
                _rootContextId,
                _ownerContextId,
                _skillRuntimeHandle);
        }
    }
}
