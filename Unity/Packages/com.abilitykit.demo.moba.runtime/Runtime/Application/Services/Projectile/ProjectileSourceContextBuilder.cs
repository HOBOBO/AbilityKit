namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public sealed class ProjectileSourceContextBuilder
    {
        private int _sourceActorId;
        private int _initialTargetActorId;
        private int _projectileConfigId;
        private long _sourceContextId;
        private long _rootContextId;
        private long _ownerContextId;
        private MobaSkillCastRuntimeHandle _skillRuntimeHandle;
        private MobaGameplayOrigin _origin;

        private ProjectileSourceContextBuilder()
        {
            Reset();
        }

        public static ProjectileSourceContextBuilder Create()
        {
            return new ProjectileSourceContextBuilder();
        }

        public ProjectileSourceContextBuilder Reset()
        {
            _sourceActorId = 0;
            _initialTargetActorId = 0;
            _projectileConfigId = 0;
            _sourceContextId = 0L;
            _rootContextId = 0L;
            _ownerContextId = 0L;
            _skillRuntimeHandle = default;
            _origin = default;
            return this;
        }

        public ProjectileSourceContextBuilder FromSourceContext(in ProjectileSourceContext sourceContext)
        {
            _sourceActorId = sourceContext.SourceActorId;
            _initialTargetActorId = sourceContext.InitialTargetActorId;
            _projectileConfigId = sourceContext.ProjectileConfigId;
            _sourceContextId = sourceContext.SourceContextId;
            _rootContextId = sourceContext.RootContextId;
            _ownerContextId = sourceContext.OwnerContextId;
            _skillRuntimeHandle = sourceContext.SkillRuntimeHandle;
            _origin = sourceContext.Origin;
            return this;
        }

        public ProjectileSourceContextBuilder WithActors(int sourceActorId, int initialTargetActorId)
        {
            _sourceActorId = sourceActorId;
            _initialTargetActorId = initialTargetActorId;
            return this;
        }

        public ProjectileSourceContextBuilder WithProjectileConfig(int projectileConfigId)
        {
            _projectileConfigId = projectileConfigId;
            return this;
        }

        public ProjectileSourceContextBuilder WithSourceContext(long sourceContextId)
        {
            _sourceContextId = sourceContextId;
            return this;
        }

        public ProjectileSourceContextBuilder WithRootContext(long rootContextId)
        {
            _rootContextId = rootContextId;
            return this;
        }

        public ProjectileSourceContextBuilder WithOwnerContext(long ownerContextId)
        {
            _ownerContextId = ownerContextId;
            return this;
        }

        public ProjectileSourceContextBuilder WithSkillRuntime(in MobaSkillCastRuntimeHandle skillRuntimeHandle)
        {
            _skillRuntimeHandle = skillRuntimeHandle;
            return this;
        }

        public ProjectileSourceContextBuilder WithOrigin(in MobaGameplayOrigin origin)
        {
            _origin = origin;
            if (!_skillRuntimeHandle.IsValid && origin.SkillRuntimeHandle.IsValid)
            {
                _skillRuntimeHandle = origin.SkillRuntimeHandle;
            }

            return this;
        }

        public ProjectileSourceContextBuilder WithLaunchContext(long sourceContextId)
        {
            _sourceContextId = sourceContextId;
            if (_rootContextId == 0L) _rootContextId = sourceContextId;
            if (_ownerContextId == 0L) _ownerContextId = sourceContextId;

            if (_origin.IsValid)
            {
                _origin = MobaGameplayOriginBuilder.Create()
                    .FromOrigin(in _origin)
                    .WithActors(_sourceActorId, _initialTargetActorId)
                    .WithLifecycleNode(MobaTraceKind.ProjectileLaunch, _projectileConfigId, sourceContextId)
                    .WithRootContext(_rootContextId)
                    .WithOwnerContext(_ownerContextId)
                    .WithSkillRuntimeIfMissing(in _skillRuntimeHandle)
                    .Build();
            }

            return this;
        }

        public ProjectileSourceContext Build()
        {
            if (_rootContextId == 0L) _rootContextId = _sourceContextId;
            if (_ownerContextId == 0L) _ownerContextId = _sourceContextId;

            if (!_origin.IsValid)
            {
                _origin = new MobaGameplayOrigin(
                    _sourceActorId,
                    _initialTargetActorId,
                    MobaTraceKind.ProjectileLaunch,
                    _projectileConfigId,
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

            return new ProjectileSourceContext(
                _sourceActorId,
                _initialTargetActorId,
                _projectileConfigId,
                _sourceContextId,
                _rootContextId,
                _ownerContextId,
                in _skillRuntimeHandle,
                in _origin);
        }
    }
}
