using AbilityKit.Combat.Projectile;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public readonly struct ProjectileSourceContext : IMobaOriginContextProvider, IMobaTriggerLineageContextProvider, IMobaContextSourceProvider, IMobaPersistentContextSourceProvider
    {
        public readonly int SourceActorId;
        public readonly int InitialTargetActorId;
        public readonly int ProjectileConfigId;
        public readonly long SourceContextId;
        public readonly long RootContextId;
        public readonly long OwnerContextId;
        public readonly MobaSkillCastRuntimeHandle SkillRuntimeHandle;
        public readonly MobaGameplayOrigin Origin;

        public ProjectileSourceContext(
            int sourceActorId,
            int initialTargetActorId,
            int projectileConfigId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle,
            in MobaGameplayOrigin origin)
        {
            SourceActorId = sourceActorId;
            InitialTargetActorId = initialTargetActorId;
            ProjectileConfigId = projectileConfigId;
            SourceContextId = sourceContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            SkillRuntimeHandle = skillRuntimeHandle;
            Origin = origin.IsValid
                ? origin
                : new MobaGameplayOrigin(
                    sourceActorId,
                    initialTargetActorId,
                    MobaExecutionKind.ProjectileLaunch,
                    projectileConfigId,
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
                    InitialTargetActorId,
                    MobaExecutionKind.ProjectileLaunch,
                    ProjectileConfigId,
                    SourceContextId,
                    SourceContextId,
                    RootContextId != 0 ? RootContextId : SourceContextId,
                    OwnerContextId != 0 ? OwnerContextId : SourceContextId,
                    SkillRuntimeHandle);
            return origin.IsValid;
        }

        public MobaTriggerLineageContext ToHitLineageContext(ProjectileId projectileId, int targetActorId)
        {
            var sourceActorId = SourceActorId;
            var target = targetActorId > 0 ? targetActorId : InitialTargetActorId;
            var rootContextId = RootContextId != 0 ? RootContextId : SourceContextId;
            var ownerContextId = OwnerContextId != 0 ? OwnerContextId : SourceContextId;
            return new MobaTriggerLineageContext(
                EffectContextKind.Projectile,
                MobaExecutionKind.ProjectileHit,
                sourceActorId,
                target,
                SourceContextId,
                rootContextId,
                ownerContextId,
                ProjectileConfigId);
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = new MobaTriggerLineageContext(
                EffectContextKind.Projectile,
                MobaExecutionKind.ProjectileLaunch,
                SourceActorId,
                InitialTargetActorId,
                SourceContextId,
                RootContextId != 0 ? RootContextId : SourceContextId,
                OwnerContextId != 0 ? OwnerContextId : SourceContextId,
                ProjectileConfigId);
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
                    "Projectile",
                    ProjectileConfigId);
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

        public ProjectileSourceContext WithLaunchContext(long sourceContextId)
        {
            return ProjectileSourceContextBuilder.Create()
                .FromSourceContext(in this)
                .WithLaunchContext(sourceContextId)
                .Build();
        }
    }
}
