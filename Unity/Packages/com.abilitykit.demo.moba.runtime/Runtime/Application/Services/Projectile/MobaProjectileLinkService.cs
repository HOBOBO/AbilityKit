using System;
using System.Collections.Generic;
using AbilityKit.Combat.Projectile;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services.Observability;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    internal sealed class MobaProjectileLinkRollbackState
    {
        public MobaProjectileLinkRollbackState(
            MobaProjectileLinkRollbackEntry[] projectiles,
            MobaProjectileRetiredLinkRollbackEntry[] retired,
            MobaProjectileLauncherLinkRollbackEntry[] launchers)
        { Projectiles = projectiles; Retired = retired; Launchers = launchers; }
        public MobaProjectileLinkRollbackEntry[] Projectiles { get; }
        public MobaProjectileRetiredLinkRollbackEntry[] Retired { get; }
        public MobaProjectileLauncherLinkRollbackEntry[] Launchers { get; }
    }

    internal readonly struct MobaProjectileLinkRollbackEntry
    {
        public MobaProjectileLinkRollbackEntry(int projectileId, int actorId,
            in ProjectileSourceContext source, in MobaSkillRuntimeRetainHandle retain,
            int confirmedKills = 0, int lastKillingHit = 0)
        {
            ProjectileId = projectileId; ActorId = actorId; Source = source; Retain = retain;
            ConfirmedKills = confirmedKills; LastKillingHit = lastKillingHit;
        }
        public int ConfirmedKills { get; }
        public int LastKillingHit { get; }
        public int ProjectileId { get; }
        public int ActorId { get; }
        public ProjectileSourceContext Source { get; }
        public MobaSkillRuntimeRetainHandle Retain { get; }
    }

    internal readonly struct MobaProjectileRetiredLinkRollbackEntry
    {
        public MobaProjectileRetiredLinkRollbackEntry(int projectileId, int actorId)
        { ProjectileId = projectileId; ActorId = actorId; }
        public int ProjectileId { get; }
        public int ActorId { get; }
    }

    internal readonly struct MobaProjectileLauncherLinkRollbackEntry
    {
        public MobaProjectileLauncherLinkRollbackEntry(int actorId, in ProjectileSourceContext source,
            in MobaSkillRuntimeRetainHandle retain)
        { ActorId = actorId; Source = source; Retain = retain; }
        public int ActorId { get; }
        public ProjectileSourceContext Source { get; }
        public MobaSkillRuntimeRetainHandle Retain { get; }
    }

    [WorldService(typeof(MobaProjectileLinkService))]
    public sealed class MobaProjectileLinkService :
        IMobaRuntimeObjectBootstrapContributor,
        IService
    {
        private readonly Dictionary<int, ProjectileId> _projectileByActorId = new Dictionary<int, ProjectileId>();
        private readonly Dictionary<ProjectileId, int> _actorIdByProjectile = new Dictionary<ProjectileId, int>();
        private readonly Dictionary<ProjectileId, int> _retiredActorIdByProjectile = new Dictionary<ProjectileId, int>();
        private readonly Dictionary<ProjectileId, ProjectileSourceContext> _sourceByProjectile = new Dictionary<ProjectileId, ProjectileSourceContext>();
        private readonly Dictionary<ProjectileId, MobaSkillRuntimeRetainHandle> _retainByProjectile = new Dictionary<ProjectileId, MobaSkillRuntimeRetainHandle>();
        private readonly Dictionary<ProjectileId, (int Count, int LastHit)> _impactKills =
            new Dictionary<ProjectileId, (int Count, int LastHit)>();
        private readonly Dictionary<int, LauncherLink> _launcherByActorId = new Dictionary<int, LauncherLink>();

        [WorldInject(required: false)] private IMobaTemporaryEntityLifecycleService _lifecycle = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        [WorldInject] private MobaSkillCastRuntimeService _skillRuntimes = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectLifecycleHook _objectLifecycle = null;
        [WorldInject(required: false)] private IMobaRuntimeObjectBootstrapRegistry _objectBootstrap = null;

        private bool _objectBootstrapRegistered;

        public int ActiveCount => _actorIdByProjectile.Count;

        internal MobaProjectileLinkRollbackState CaptureRollbackState()
        {
            var projectiles = new List<MobaProjectileLinkRollbackEntry>(_actorIdByProjectile.Count);
            foreach (var pair in _actorIdByProjectile)
            {
                _sourceByProjectile.TryGetValue(pair.Key, out var source);
                _retainByProjectile.TryGetValue(pair.Key, out var retain);
                _impactKills.TryGetValue(pair.Key, out var impactKills);
                projectiles.Add(new MobaProjectileLinkRollbackEntry(pair.Key.Value, pair.Value, in source, in retain,
                    impactKills.Count, impactKills.LastHit));
            }
            projectiles.Sort((left, right) => left.ProjectileId.CompareTo(right.ProjectileId));

            var retired = new List<MobaProjectileRetiredLinkRollbackEntry>(_retiredActorIdByProjectile.Count);
            foreach (var pair in _retiredActorIdByProjectile)
                retired.Add(new MobaProjectileRetiredLinkRollbackEntry(pair.Key.Value, pair.Value));
            retired.Sort((left, right) => left.ProjectileId.CompareTo(right.ProjectileId));

            var launchers = new List<MobaProjectileLauncherLinkRollbackEntry>(_launcherByActorId.Count);
            foreach (var pair in _launcherByActorId)
                launchers.Add(new MobaProjectileLauncherLinkRollbackEntry(
                    pair.Key, in pair.Value.Source, in pair.Value.RetainHandle));
            launchers.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
            return new MobaProjectileLinkRollbackState(projectiles.ToArray(), retired.ToArray(), launchers.ToArray());
        }

        internal void RestoreRollbackState(MobaProjectileLinkRollbackState state)
        {
            _projectileByActorId.Clear();
            _actorIdByProjectile.Clear();
            _retiredActorIdByProjectile.Clear();
            _sourceByProjectile.Clear();
            _impactKills.Clear();
            _retainByProjectile.Clear();
            _launcherByActorId.Clear();
            foreach (var entry in state.Projectiles)
            {
                var id = new ProjectileId(entry.ProjectileId);
                _projectileByActorId.Add(entry.ActorId, id);
                _actorIdByProjectile.Add(id, entry.ActorId);
                if (entry.Source.IsValid) _sourceByProjectile.Add(id, entry.Source);
                if (entry.Retain.IsValid) _retainByProjectile.Add(id, entry.Retain);
                if (entry.ConfirmedKills > 0) _impactKills.Add(id, (entry.ConfirmedKills, entry.LastKillingHit));
            }
            foreach (var entry in state.Retired)
                _retiredActorIdByProjectile.Add(new ProjectileId(entry.ProjectileId), entry.ActorId);
            foreach (var entry in state.Launchers)
            {
                var source = entry.Source;
                var link = new LauncherLink(in source) { RetainHandle = entry.Retain };
                _launcherByActorId.Add(entry.ActorId, link);
            }
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Projectile, ActiveCount);
        }

        public void Link(ProjectileId projectileId, int actorId)
        {
            if (projectileId.Value <= 0) throw new ArgumentOutOfRangeException(nameof(projectileId));
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            // Link is a retryable boundary: the same pair may be submitted more than
            // once, but a conflicting pair must fail before either index is mutated.
            var hasExistingActorLink = _projectileByActorId.TryGetValue(actorId, out var existingProjectile);
            if (hasExistingActorLink &&
                existingProjectile.Value != projectileId.Value)
            {
                throw new InvalidOperationException(
                    $"Projectile actor {actorId} is already linked to projectile {existingProjectile.Value}.");
            }

            var hasExistingProjectileLink = _actorIdByProjectile.TryGetValue(projectileId, out var existingActor);
            if (hasExistingProjectileLink &&
                existingActor != actorId)
            {
                throw new InvalidOperationException(
                    $"Projectile {projectileId.Value} is already linked to actor {existingActor}.");
            }

            EnsureObjectBootstrapRegistered();
            if (hasExistingActorLink && hasExistingProjectileLink &&
                existingProjectile.Value == projectileId.Value && existingActor == actorId)
            {
                _retiredActorIdByProjectile.Remove(projectileId);
                return;
            }

            _projectileByActorId[actorId] = projectileId;
            _actorIdByProjectile[projectileId] = actorId;
            _retiredActorIdByProjectile.Remove(projectileId);
            _lifecycle?.RecordSpawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
            PublishProjectileLifecycle(
                MobaRuntimeObjectLifecycleStage.Created,
                projectileId,
                actorId,
                default);
        }

        public void BindSource(ProjectileId projectileId, in ProjectileSourceContext source)
        {
            if (projectileId.Value == 0) return;
            if (!source.IsValid)
            {
                throw new InvalidOperationException($"Projectile source context is incomplete. projectileId={projectileId.Value} sourceActorId={source.SourceActorId} sourceContextId={source.SourceContextId} projectileConfigId={source.ProjectileConfigId}");
            }

            _sourceByProjectile[projectileId] = source;
            _actorIdByProjectile.TryGetValue(projectileId, out var actorId);
            PublishProjectileLifecycle(
                MobaRuntimeObjectLifecycleStage.Created,
                projectileId,
                actorId,
                in source);
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

        public bool TryGetRetiredActorId(ProjectileId projectileId, out int actorId)
        {
            return _retiredActorIdByProjectile.TryGetValue(projectileId, out actorId);
        }

        internal bool TryReadPriorImpactKills(ProjectileId projectileId, long sourceContextId, int hitIndex, out int count)
        {
            count = 0;
            if (hitIndex <= 0 || !_actorIdByProjectile.ContainsKey(projectileId)
                || !TryGetSource(projectileId, out var source) || source.SourceContextId != sourceContextId)
                return false;

            if (!_impactKills.TryGetValue(projectileId, out var kills)) return true;
            if (hitIndex < kills.LastHit) return false;
            count = hitIndex == kills.LastHit ? kills.Count - 1 : kills.Count;
            return true;
        }

        internal void RecordImpactKill(ProjectileId projectileId, long sourceContextId, int hitIndex)
        {
            if (!TryReadPriorImpactKills(projectileId, sourceContextId, hitIndex, out var count)) return;
            _impactKills[projectileId] = (count + 1, hitIndex);
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
                _retiredActorIdByProjectile[pid] = actorId;
                _sourceByProjectile.Remove(pid);
                _impactKills.Remove(pid);
                ConsumeAndReleaseRetain(pid);
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
                PublishProjectileLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    pid,
                    actorId,
                    in capturedSource);
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
                _retiredActorIdByProjectile[projectileId] = capturedActorId;
                removed = true;
            }

            _sourceByProjectile.Remove(projectileId);
            _impactKills.Remove(projectileId);
            ConsumeAndReleaseRetain(projectileId);
            if (removed)
            {
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
                PublishProjectileLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    projectileId,
                    capturedActorId,
                    in capturedSource);
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
            _impactKills.Remove(projectileId);
            ConsumeAndReleaseRetain(projectileId);
            if (wasActive)
            {
                _retiredActorIdByProjectile[projectileId] = actorId;
                _lifecycle?.RecordDespawn(MobaTemporaryEntityKind.Projectile, ActiveCount);
                PublishProjectileLifecycle(
                    MobaRuntimeObjectLifecycleStage.Destroyed,
                    projectileId,
                    actorId,
                    default,
                    (int)MobaExecutionEndReason.Failed);
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
            if (_objectLifecycle != null && _objectLifecycle.IsEnabled)
            {
                foreach (var pair in _actorIdByProjectile)
                {
                    _sourceByProjectile.TryGetValue(pair.Key, out var source);
                    PublishProjectileLifecycle(
                        MobaRuntimeObjectLifecycleStage.Destroyed,
                        pair.Key,
                        pair.Value,
                        in source,
                        (int)MobaExecutionEndReason.Cancelled);
                }
            }
            ReleaseAllRetains();
            _projectileByActorId.Clear();
            _actorIdByProjectile.Clear();
            _retiredActorIdByProjectile.Clear();
            _sourceByProjectile.Clear();
            _impactKills.Clear();
            _launcherByActorId.Clear();
            _lifecycle?.SetActive(MobaTemporaryEntityKind.Projectile, 0);
        }

        private void PublishProjectileLifecycle(
            MobaRuntimeObjectLifecycleStage stage,
            ProjectileId projectileId,
            int actorId,
            in ProjectileSourceContext source,
            int endReason = 0)
        {
            EnsureObjectBootstrapRegistered();
            var hook = _objectLifecycle;
            if (hook == null || !hook.IsEnabled || projectileId.Value == 0) return;
            PublishProjectileLifecycleTo(
                hook,
                stage,
                projectileId,
                actorId,
                in source,
                _frameTime != null ? _frameTime.Frame.Value : -1,
                endReason);
        }

        void IMobaRuntimeObjectBootstrapContributor.CaptureActiveRuntimeObjects(
            IMobaRuntimeObjectLifecycleHook hook,
            int frame)
        {
            foreach (var pair in _actorIdByProjectile)
            {
                _sourceByProjectile.TryGetValue(pair.Key, out var source);
                PublishProjectileLifecycleTo(
                    hook,
                    MobaRuntimeObjectLifecycleStage.Created,
                    pair.Key,
                    pair.Value,
                    in source,
                    frame);
            }
        }

        private static void PublishProjectileLifecycleTo(
            IMobaRuntimeObjectLifecycleHook hook,
            MobaRuntimeObjectLifecycleStage stage,
            ProjectileId projectileId,
            int actorId,
            in ProjectileSourceContext source,
            int frame,
            int endReason = 0)
        {
            if (hook == null || !hook.IsEnabled || projectileId.Value == 0) return;
            var observation = new MobaRuntimeObjectLifecycleObservation(
                stage,
                MobaRuntimeObjectKind.Projectile,
                projectileId.Value,
                frame,
                MobaRuntimeObjectDefinitionKind.Projectile,
                source.ProjectileConfigId,
                relatedActorId: actorId,
                ownerActorId: source.SourceActorId,
                sourceActorId: source.SourceActorId,
                targetActorId: source.InitialTargetActorId,
                rootContextId: source.RootContextId,
                contextId: source.SourceContextId,
                endReason: endReason);
            hook.TryObserve(in observation);
        }

        private void EnsureObjectBootstrapRegistered()
        {
            if (_objectBootstrapRegistered) return;
            var registry = _objectBootstrap;
            if (registry != null && registry.Register(this))
                _objectBootstrapRegistered = true;
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
                summary: summary,
                subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                    BattleDiagnosticRuntimeObjectKind.Projectile,
                    projectileIdValue));
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
            if (_objectBootstrapRegistered)
            {
                _objectBootstrap?.Unregister(this);
                _objectBootstrapRegistered = false;
            }
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
