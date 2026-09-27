using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    public sealed class ProjectileHitArgs : MobaTriggerInvocationContextBase, IMobaActorContextProvider, IMobaTriggerSkillRuntimeContext, IMobaTriggerExecutionSnapshotProvider, IMobaContextSourceProvider, IMobaPersistentContextSourceProvider
    {
        public override EffectContextKind Kind => EffectContextKind.Projectile;
        public int SourceConfigId { get; set; }
        public int Frame { get; set; }
        public object Raw { get; set; }
        public ProjectileSourceContext SourceContext { get; set; }

        public int CasterActorId;
        public int ProjectileTemplateId;
        public ProjectileId ProjectileId;
        public Vec3 Point;
        public Vec3 Normal;
        public ColliderId HitCollider;

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = SourceActorId > 0 ? SourceActorId : CasterActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public override bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (SourceContext.IsValid)
            {
                lineageContext = SourceContext.ToHitLineageContext(ProjectileId, TargetActorId);
                return true;
            }

            lineageContext = default;
            return false;
        }

        public override bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (SourceContext.TryGetOrigin(out var sourceOrigin))
            {
                origin = sourceOrigin.WithActors(SourceActorId > 0 ? SourceActorId : CasterActorId, TargetActorId);
                return origin.IsValid;
            }

            if (TryGetLineageContext(out var lineageContext))
            {
                origin = MobaGameplayOrigin.FromLineageContext(in lineageContext);
                return origin.IsValid;
            }

            origin = default;
            return false;
        }

        public bool TryGetSkillRuntimeHandle(out MobaSkillCastRuntimeHandle handle)
        {
            handle = SourceContext.SkillRuntimeHandle;
            return handle.IsValid;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            if (!TryGetLineageContext(out var lineageContext))
            {
                snapshot = default;
                return false;
            }

            snapshot = new MobaTriggerExecutionSnapshot(
                lineageContext.ContextKind,
                lineageContext.SourceActorId,
                lineageContext.TargetActorId,
                lineageContext.SourceContextId,
                lineageContext.RootContextId,
                lineageContext.OwnerContextId,
                TriggerId,
                ProjectileTemplateId != 0 ? ProjectileTemplateId : SourceConfigId,
                Frame,
                SourceContext.SkillRuntimeHandle);
            return snapshot.IsValid;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (TryGetLineageContext(out var lineageContext))
            {
                source = MobaContextSourceView.FromLineage(
                    in lineageContext,
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.Snapshot,
                    SourceContext.SkillRuntimeHandle,
                    false,
                    "ProjectileHit",
                    ProjectileTemplateId);
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

    }
}
