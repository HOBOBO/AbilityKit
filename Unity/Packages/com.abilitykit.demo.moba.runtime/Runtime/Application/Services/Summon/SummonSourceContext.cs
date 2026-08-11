namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct SummonSourceContext : IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider, IMobaPersistentContextSourceProvider
    {
        public readonly int SourceActorId;
        public readonly int SummonActorId;
        public readonly int SummonConfigId;
        public readonly long SourceContextId;
        public readonly long RootContextId;
        public readonly long OwnerContextId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;
        public readonly MobaGameplayOrigin Origin;

        public SummonSourceContext(
            int sourceActorId,
            int summonActorId,
            int summonConfigId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle,
            in MobaGameplayOrigin origin)
        {
            SourceActorId = sourceActorId;
            SummonActorId = summonActorId;
            SummonConfigId = summonConfigId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            Origin = origin.IsValid
                ? origin
                : new MobaGameplayOrigin(
                    sourceActorId,
                    summonActorId,
                    MobaTraceKind.SummonSpawn,
                    summonConfigId,
                    sourceContextId,
                    sourceContextId,
                    rootContextId != 0 ? rootContextId : sourceContextId,
                    ownerContextId != 0 ? ownerContextId : sourceContextId,
                    skillRuntimeHandle);
        }

        public bool IsValid => SourceActorId > 0 && SourceContextId != 0;

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = Origin.IsValid
                ? Origin
                : new MobaGameplayOrigin(
                    SourceActorId,
                    SummonActorId,
                    MobaTraceKind.SummonSpawn,
                    SummonConfigId,
                    SourceContextId,
                    SourceContextId,
                    RootContextId != 0 ? RootContextId : SourceContextId,
                    OwnerContextId != 0 ? OwnerContextId : SourceContextId,
                    SkillRuntimeHandle);
            return origin.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = new MobaTriggerLineageContext(
                EffectContextKind.Summon,
                MobaTraceKind.SummonSpawn,
                SourceActorId,
                SummonActorId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId != 0 ? OwnerContextId : SourceContextId,
                SummonConfigId);
            return lineageContext.SourceActorId > 0 && lineageContext.SourceContextId != 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    SkillRuntimeHandle,
                    false,
                    "Summon",
                    SummonConfigId);
                return source.IsValid;
            }

            source = default;
            return false;
        }

        public bool TryGetPersistentContextSource(out MobaPersistentContextSourceSnapshot snapshot)
        {
            if (TryGetContextSource(out var source))
            {
                snapshot = MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);
                return snapshot.HasExecutionSource;
            }

            snapshot = default;
            return false;
        }

        public SummonSourceContext WithSpawnContext(long sourceContextId, int summonActorId)
        {
            return SummonSourceContextBuilder.Create()
                .FromSourceContext(in this)
                .WithActors(SourceActorId, summonActorId)
                .WithSpawnContext(sourceContextId)
                .Build();
        }
    }

    public sealed class SummonSourceContextBuilder
    {
        private int _sourceActorId;
        private int _summonActorId;
        private int _summonConfigId;
        private long _sourceContextId;
        private long _rootContextId;
        private long _ownerContextId;
        private MobaSkillCastRuntimeHandle _skillRuntimeHandle;
        private MobaGameplayOrigin _origin;

        private SummonSourceContextBuilder()
        {
            Reset();
        }

        public static SummonSourceContextBuilder Create()
        {
            return new SummonSourceContextBuilder();
        }

        public SummonSourceContextBuilder Reset()
        {
            _sourceActorId = 0;
            _summonActorId = 0;
            _summonConfigId = 0;
            _sourceContextId = 0L;
            _rootContextId = 0L;
            _ownerContextId = 0L;
            _skillRuntimeHandle = default;
            _origin = default;
            return this;
        }

        public SummonSourceContextBuilder FromSourceContext(in SummonSourceContext sourceContext)
        {
            _sourceActorId = sourceContext.SourceActorId;
            _summonActorId = sourceContext.SummonActorId;
            _summonConfigId = sourceContext.SummonConfigId;
            _sourceContextId = sourceContext.SourceContextId;
            _rootContextId = sourceContext.RootContextId;
            _ownerContextId = sourceContext.OwnerContextId;
            _skillRuntimeHandle = sourceContext.SkillRuntimeHandle;
            _origin = sourceContext.Origin;
            return this;
        }

        public SummonSourceContextBuilder WithActors(int sourceActorId, int summonActorId)
        {
            _sourceActorId = sourceActorId;
            _summonActorId = summonActorId;
            return this;
        }

        public SummonSourceContextBuilder WithSummonConfig(int summonConfigId)
        {
            _summonConfigId = summonConfigId;
            return this;
        }

        public SummonSourceContextBuilder WithSourceContext(long sourceContextId)
        {
            _sourceContextId = sourceContextId;
            return this;
        }

        public SummonSourceContextBuilder WithRootContext(long rootContextId)
        {
            _rootContextId = rootContextId;
            return this;
        }

        public SummonSourceContextBuilder WithOwnerContext(long ownerContextId)
        {
            _ownerContextId = ownerContextId;
            return this;
        }

        public SummonSourceContextBuilder WithSkillRuntime(in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            _skillRuntimeHandle = skillRuntimeHandle;
            return this;
        }

        public SummonSourceContextBuilder WithOrigin(in MobaGameplayOrigin origin)
        {
            _origin = origin;
            if (!_skillRuntimeHandle.IsValid && origin.SkillRuntimeHandle.IsValid)
            {
                _skillRuntimeHandle = origin.SkillRuntimeHandle;
            }

            return this;
        }

        public SummonSourceContextBuilder WithSpawnContext(long sourceContextId)
        {
            _sourceContextId = sourceContextId;
            if (_rootContextId == 0L) _rootContextId = sourceContextId;
            if (_ownerContextId == 0L) _ownerContextId = sourceContextId;

            if (_origin.IsValid)
            {
                _origin = MobaGameplayOriginBuilder.Create()
                    .FromOrigin(in _origin)
                    .WithActors(_sourceActorId, _summonActorId)
                    .WithLifecycleNode(MobaTraceKind.SummonSpawn, _summonConfigId, sourceContextId)
                    .WithRootContext(_rootContextId)
                    .WithOwnerContext(_ownerContextId)
                    .WithSkillRuntimeIfMissing(in _skillRuntimeHandle)
                    .Build();
            }

            return this;
        }

        public SummonSourceContext Build()
        {
            if (_rootContextId == 0L) _rootContextId = _sourceContextId;
            if (_ownerContextId == 0L) _ownerContextId = _sourceContextId;

            if (!_origin.IsValid)
            {
                _origin = new MobaGameplayOrigin(
                    _sourceActorId,
                    _summonActorId,
                    MobaTraceKind.SummonSpawn,
                    _summonConfigId,
                    _sourceContextId,
                    _sourceContextId,
                    _rootContextId != 0 ? _rootContextId : _sourceContextId,
                    _ownerContextId != 0 ? _ownerContextId : _sourceContextId,
                    _skillRuntimeHandle);
            }

            if (!_skillRuntimeHandle.IsValid && _origin.SkillRuntimeHandle.IsValid)
            {
                _skillRuntimeHandle = _origin.SkillRuntimeHandle;
            }

            return new SummonSourceContext(
                _sourceActorId,
                _summonActorId,
                _summonConfigId,
                _sourceContextId,
                _rootContextId,
                _ownerContextId,
                in _skillRuntimeHandle,
                in _origin);
        }
    }
}
