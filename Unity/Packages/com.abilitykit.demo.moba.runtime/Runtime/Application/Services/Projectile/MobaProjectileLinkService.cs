using System;
using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    [WorldService(typeof(MobaProjectileLinkService))]
    public sealed class MobaProjectileLinkService : IService
    {
        private readonly Dictionary<int, ProjectileId> _projectileByActorId = new Dictionary<int, ProjectileId>();
        private readonly Dictionary<ProjectileId, int> _actorIdByProjectile = new Dictionary<ProjectileId, int>();
        private readonly Dictionary<ProjectileId, ProjectileSourceContext> _sourceByProjectile = new Dictionary<ProjectileId, ProjectileSourceContext>();
        private readonly Dictionary<ProjectileId, MobaSkillRuntimeRetainHandle> _retainByProjectile = new Dictionary<ProjectileId, MobaSkillRuntimeRetainHandle>();
        private readonly Dictionary<int, LauncherLink> _launcherByActorId = new Dictionary<int, LauncherLink>();

        [WorldInject(required: false)] private IMobaTemporaryEntityLifecycleService _lifecycle = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        [WorldInject] private MobaSkillCastRuntimeService _skillRuntimes = null;

        public int ActiveCount => _actorIdByProjectile.Count;

        public void Link(ProjectileId projectileId, int actorId)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _projectileByActorId[actorId] = projectileId;
            _actorIdByProjectile[projectileId] = actorId;
            _lifecycle?.RecordSpawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
        }

        public void BindSource(ProjectileId projectileId, in ProjectileSourceContext source)
        {
            if (projectileId.Value == 0) return;
            if (!source.IsValid)
            {
                throw new InvalidOperationException($"Projectile source context is incomplete. projectileId={projectileId.Value} sourceActorId={source.SourceActorId} sourceContextId={source.SourceContextId} projectileConfigId={source.ProjectileConfigId}");
            }

            _sourceByProjectile[projectileId] = source;
        }

        public void BindRetain(ProjectileId projectileId, in MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (projectileId.Value == 0) return;
            if (!retainHandle.IsValid) return;
            if (_skillRuntimes == null) throw new InvalidOperationException("Projectile retain ownership requires MobaSkillCastRuntimeService.");
            if (_retainByProjectile.TryGetValue(projectileId, out var existing))
            {
                if (existing.Equals(retainHandle)) return;
                throw new InvalidOperationException($"Projectile already owns a different skill runtime retain. projectileId={projectileId.Value}");
            }
            _retainByProjectile[projectileId] = retainHandle;
        }

        public void BindLauncherSource(int launcherActorId, in ProjectileSourceContext source)
        {
            if (launcherActorId <= 0) return;
            if (!source.IsValid)
            {
                throw new InvalidOperationException($"Projectile launcher source context is incomplete. launcherActorId={launcherActorId} sourceActorId={source.SourceActorId} sourceContextId={source.SourceContextId} projectileConfigId={source.ProjectileConfigId}");
            }

            if (_launcherByActorId.TryGetValue(launcherActorId, out var link))
            {
                link.Source = source;
                return;
            }

            _launcherByActorId.Add(launcherActorId, new LauncherLink(in source));
        }

        public void BindLauncherRetain(int launcherActorId, in MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (launcherActorId <= 0) return;
            if (!retainHandle.IsValid) return;
            if (_skillRuntimes == null) throw new InvalidOperationException("Projectile launcher retain ownership requires MobaSkillCastRuntimeService.");
            if (!_launcherByActorId.TryGetValue(launcherActorId, out var link))
            {
                throw new InvalidOperationException($"Projectile launcher retain requires a bound source. launcherActorId={launcherActorId}");
            }

            if (link.RetainHandle.IsValid)
            {
                if (link.RetainHandle.Equals(retainHandle)) return;
                throw new InvalidOperationException($"Projectile launcher already owns a different skill runtime retain. launcherActorId={launcherActorId}");
            }
            link.RetainHandle = retainHandle;
        }

        public bool TryGetActorId(ProjectileId projectileId, out int actorId)
        {
            return _actorIdByProjectile.TryGetValue(projectileId, out actorId);
        }

        public bool TryGetSource(ProjectileId projectileId, out ProjectileSourceContext source)
        {
            return _sourceByProjectile.TryGetValue(projectileId, out source) && source.IsValid;
        }

        /// <summary>
        /// Copies active projectile links as value snapshots for diagnostics consumers.
        /// The returned entries do not expose the service's mutable indexes.
        /// </summary>
        public int CopyDiagnosticsTo(List<MobaProjectileLinkDiagnostics> results)
        {
            if (results == null) return 0;

            var start = results.Count;
            foreach (var pair in _actorIdByProjectile)
            {
                var projectileId = pair.Key;
                _sourceByProjectile.TryGetValue(projectileId, out var source);
                results.Add(new MobaProjectileLinkDiagnostics(projectileId, pair.Value, in source));
            }

            return results.Count - start;
        }

        public bool TryGetRetain(ProjectileId projectileId, out MobaSkillRuntimeRetainHandle retainHandle)
        {
            return _retainByProjectile.TryGetValue(projectileId, out retainHandle) && retainHandle.IsValid;
        }

        public bool TryConsumeRetain(ProjectileId projectileId, out MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (!_retainByProjectile.TryGetValue(projectileId, out retainHandle) || !retainHandle.IsValid)
            {
                retainHandle = default;
                return false;
            }

            _retainByProjectile.Remove(projectileId);
            return true;
        }

        public bool TryGetLauncherSource(int launcherActorId, out ProjectileSourceContext source)
        {
            if (_launcherByActorId.TryGetValue(launcherActorId, out var link) && link.Source.IsValid)
            {
                source = link.Source;
                return true;
            }

            source = default;
            return false;
        }

        public bool TryGetLauncherRetain(int launcherActorId, out MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (_launcherByActorId.TryGetValue(launcherActorId, out var link) && link.RetainHandle.IsValid)
            {
                retainHandle = link.RetainHandle;
                return true;
            }

            retainHandle = default;
            return false;
        }

        public bool TryConsumeLauncherRetain(int launcherActorId, out MobaSkillRuntimeRetainHandle retainHandle)
        {
            if (!TryGetLauncherRetain(launcherActorId, out retainHandle)) return false;

            _launcherByActorId[launcherActorId].RetainHandle = default;
            return true;
        }

        public bool TryGetProjectileId(int actorId, out ProjectileId projectileId)
        {
            return _projectileByActorId.TryGetValue(actorId, out projectileId);
        }

        public void UnlinkByActorId(int actorId)
        {
            if (actorId <= 0) return;
            if (_projectileByActorId.TryGetValue(actorId, out var pid))
            {
                _sourceByProjectile.TryGetValue(pid, out var capturedSource);
                _projectileByActorId.Remove(actorId);
                _actorIdByProjectile.Remove(pid);
                _sourceByProjectile.Remove(pid);
                ConsumeAndReleaseRetain(pid);
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
                CollectProjectileEnded(actorId, pid.Value, in capturedSource);
            }
        }

        public void UnlinkByProjectileId(ProjectileId projectileId)
        {
            var removed = false;
            var capturedActorId = 0;
            _sourceByProjectile.TryGetValue(projectileId, out var capturedSource);
            if (_actorIdByProjectile.TryGetValue(projectileId, out var actorId))
            {
                _actorIdByProjectile.Remove(projectileId);
                _projectileByActorId.Remove(actorId);
                capturedActorId = actorId;
                removed = true;
            }

            _sourceByProjectile.Remove(projectileId);
            ConsumeAndReleaseRetain(projectileId);
            if (removed)
            {
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
                CollectProjectileEnded(capturedActorId, projectileId.Value, in capturedSource);
            }
        }

        internal void RollbackLink(ProjectileId projectileId, int expectedActorId)
        {
            var wasActive = false;
            if (_actorIdByProjectile.TryGetValue(projectileId, out var actorId))
            {
                _actorIdByProjectile.Remove(projectileId);
                _projectileByActorId.Remove(actorId);
                wasActive = true;
            }

            if (expectedActorId > 0) _projectileByActorId.Remove(expectedActorId);

            _sourceByProjectile.Remove(projectileId);
            ConsumeAndReleaseRetain(projectileId);
            if (wasActive)
            {
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
            }
        }

        public void UnlinkLauncher(int launcherActorId)
        {
            if (launcherActorId <= 0) return;
            ConsumeAndReleaseLauncherRetain(launcherActorId);
            _launcherByActorId.Remove(launcherActorId);
        }

        public void Clear()
        {
            ReleaseAllRetains();
            _projectileByActorId.Clear();
            _actorIdByProjectile.Clear();
            _sourceByProjectile.Clear();
            _launcherByActorId.Clear();
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Projectile, 0);
        }

        private bool ConsumeAndReleaseRetain(ProjectileId projectileId)
        {
            return TryConsumeRetain(projectileId, out var retainHandle) && ReleaseRetainCapability(in retainHandle, $"projectileId={projectileId.Value}");
        }

        private bool ConsumeAndReleaseLauncherRetain(int launcherActorId)
        {
            return TryConsumeLauncherRetain(launcherActorId, out var retainHandle) && ReleaseRetainCapability(in retainHandle, $"launcherActorId={launcherActorId}");
        }

        private bool ReleaseRetainCapability(in MobaSkillRuntimeRetainHandle retainHandle, string owner)
        {
            if (!retainHandle.IsValid) return false;
            try
            {
                return _skillRuntimes != null && _skillRuntimes.ReleaseChild(in retainHandle);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaProjectileLinkService] Release skill runtime retain failed ({owner})");
                return false;
            }
        }

        private void ReleaseAllRetains()
        {
            foreach (var pair in _retainByProjectile)
            {
                var retainHandle = pair.Value;
                ReleaseRetainCapability(in retainHandle, $"projectileId={pair.Key.Value}");
            }
            _retainByProjectile.Clear();

            foreach (var pair in _launcherByActorId)
            {
                var retainHandle = pair.Value.RetainHandle;
                if (!retainHandle.IsValid) continue;
                ReleaseRetainCapability(in retainHandle, $"launcherActorId={pair.Key}");
                pair.Value.RetainHandle = default;
            }
        }

        internal static MobaBattleDiagnosticEventDraft CreateProjectileEndedDraft(
            int projectileActorId,
            int projectileIdValue,
            in ProjectileSourceContext sourceContext)
        {
            sourceContext.TryGetOrigin(out var resolvedOrigin);
            var handle = sourceContext.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : sourceContext.RootContextId;
            var contextId = sourceContext.SourceContextId != 0L
                ? sourceContext.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = $"projectileId={sourceContext.ProjectileConfigId}, projectileActorId={projectileActorId}, projectileIdValue={projectileIdValue}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.ProjectileEnded,
                BattleDiagnosticEventChannel.TemporaryEntity,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceContext.SourceActorId,
                sourceContext.InitialTargetActorId,
                sourceContext.ProjectileConfigId,
                rootContextId,
                contextId,
                runtime,
                summary: summary);
        }

        private void CollectProjectileEnded(
            int projectileActorId,
            int projectileIdValue,
            in ProjectileSourceContext sourceContext)
        {
            if (_eventCollector == null) return;
            if (!sourceContext.IsValid) return;

            try
            {
                var draft = CreateProjectileEndedDraft(projectileActorId, projectileIdValue, in sourceContext);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响弹丸销毁流程，静默吞掉异常。
            }
        }

        public void Dispose()
        {
            Clear();
        }

        private sealed class LauncherLink
        {
            public LauncherLink(in ProjectileSourceContext source)
            {
                Source = source;
            }

            public ProjectileSourceContext Source;
            public MobaSkillRuntimeRetainHandle RetainHandle;
        }
    }

    /// <summary>
    /// Immutable active-projectile link snapshot for debug tooling.
    /// </summary>
    public readonly struct MobaProjectileLinkDiagnostics
    {
        public MobaProjectileLinkDiagnostics(
            ProjectileId projectileId,
            int actorId,
            in ProjectileSourceContext source)
        {
            ProjectileId = projectileId;
            ActorId = actorId;
            Source = source;
        }

        public ProjectileId ProjectileId { get; }
        public int ActorId { get; }
        public ProjectileSourceContext Source { get; }
        public bool HasSource => Source.IsValid;
    }
}
