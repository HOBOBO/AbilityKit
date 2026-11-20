using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Projectile;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 校验目标 Launcher 的跨 Provider 关系，并准备其拥有的发射运行时静默恢复。
    /// </summary>
    public sealed partial class MobaSkillLifecycleRollbackPreparation
    {
        /*
         * 只重建由目标 Launcher 与技能 retain 共同拥有的发射 Continuous。
         * Launcher 清理当帧遗留的 Continuous 仍沿用原地恢复合同，缺失时不得独立重建。
         */
        private List<IContinuous> PrepareProjectileLaunches(FrameIndex frame, Dictionary<string, byte[]> sections,
            MobaContinuousLocalCheckpoint[] order, int[] actorIds)
        {
            var actors = new HashSet<int>(actorIds);
            var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(sections["skill-runtime"]);
            var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
            var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(sections["execution-contexts"]);
            var nodes = new Dictionary<long, MobaExecutionContextNode>();
            foreach (var node in MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(execution.Nodes))
                nodes.Add(node.ContextId, node.ToNode());
            var live = new Dictionary<(string, long, string, int), MobaProjectileLaunchContinuous>();
            foreach (var instance in _continuous.GetAllContinuous())
                if (instance is MobaProjectileLaunchContinuous launch)
                    live.Add(MobaContinuousLocalCheckpoint.Capture(launch).Key, launch);
            var result = new List<IContinuous>();
            var launchers = new HashSet<int>();
            foreach (var entry in order)
            {
                if (entry == null || entry.Type != typeof(MobaProjectileLaunchContinuous).FullName) continue;
                var state = entry.ProjectileLaunch;
                if (state == null || state.StartFrame > frame.Value || !launchers.Add(state.LauncherActorId) ||
                    !actors.Contains(state.CasterActorId) || !actors.Contains(state.SourceActorId))
                    throw new InvalidOperationException("Invalid projectile launch Continuous checkpoint identity.");
                if (live.TryGetValue(entry.Key, out var existing))
                {
                    var current = MobaProjectileLaunchContinuousRollbackState.Capture(existing);
                    if (!MobaSkillLifecycleCheckpointCodec.Encode(current).AsSpan().SequenceEqual(
                        MobaSkillLifecycleCheckpointCodec.Encode(state)))
                        throw new InvalidOperationException("Surviving launch Continuous changed its immutable request or result identity.");
                    result.Add(existing);
                }
                else if (!actors.Contains(state.LauncherActorId))
                    throw new InvalidOperationException("Local Continuous membership is outside declared recovery ownership.");
                else
                {
                    var restored = _projectileService?.PrepareRollbackLaunch(state) ??
                        throw new InvalidOperationException("Launcher recovery requires the world projectile service.");
                    var source = restored.Request.SourceContext;
                    source.TryGetOrigin(out var origin);
                    var parentMatches = false;
                    foreach (var parent in skills.Runtimes)
                        if (parent.RuntimeId == state.SkillRuntimeId && parent.Generation == state.SkillRuntimeGeneration &&
                            parent.RootContextId == state.SkillRuntimeRootContextId && parent.CasterActorId == state.CasterActorId)
                            parentMatches = true;
                    var owned = false;
                    foreach (var retain in skills.Retains)
                        if (retain.ChildKind == (int)MobaSkillRuntimeChildKind.ProjectileLauncher &&
                            retain.ChildId == state.LauncherActorId && retain.ChildConfigId == state.LauncherId &&
                            retain.RuntimeId == state.SkillRuntimeId && retain.Generation == state.SkillRuntimeGeneration &&
                            retain.RootContextId == state.SkillRuntimeRootContextId &&
                            nodes.TryGetValue(retain.ChildContextId, out var node) && !node.IsEnded &&
                            node.Kind == MobaExecutionKind.ProjectileLaunch && node.ConfigId == state.ProjectileId &&
                            node.SourceActorId == state.CasterActorId && node.TargetActorId == source.InitialTargetActorId &&
                            node.ParentContextId == origin.EffectiveParentContextId && node.RootContextId == source.RootContextId &&
                            node.OwnerContextId == source.OwnerContextId && node.CreatedFrame == state.StartFrame)
                            owned = true;
                    if (!owned || !parentMatches || !source.SkillRuntimeHandle.IsValid || state.SourceActorId != state.CasterActorId ||
                        !origin.SkillRuntimeHandle.Equals(source.SkillRuntimeHandle) ||
                        source.RootContextId != source.SkillRuntimeHandle.RootContextId ||
                        origin.EffectiveRootContextId != source.RootContextId || origin.OwnerContextId != source.OwnerContextId ||
                        !nodes.ContainsKey(source.SourceContextId) || !nodes.ContainsKey(origin.EffectiveParentContextId) ||
                        source.InitialTargetActorId > 0 && !actors.Contains(source.InitialTargetActorId) ||
                        entry.State != (int)ContinuousState.Active && entry.State != (int)ContinuousState.Paused)
                        throw new InvalidOperationException("Projectile launch Continuous has no exact target Launcher retain.");
                    if (MobaContinuousLocalCheckpoint.Capture(restored).Key != entry.Key)
                        throw new InvalidOperationException("Projectile launch Continuous identity differs from its request.");
                    result.Add(restored);
                }
                live.Remove(entry.Key);
            }
            /*
             * 未被目标声明的现存 Continuous 继续参与严格名单校验，不能借此静默丢弃。
             */
            foreach (var launch in live.Values) result.Add(launch);
            return result;
        }

        private void ValidateLauncherSnapshot(RollbackSnapshotPreflightContext context,
            MobaSkillLifecycleCheckpoint checkpoint, MobaEntitasComponentSnapshotSummary[] components,
            HashSet<int> launcherIds)
        {
            var sections = MobaSkillLifecycleCheckpointCodec.ReadSections(checkpoint);
            var order = MobaSkillLifecycleCheckpointCodec.Decode<MobaContinuousLocalCheckpoint[]>(sections["continuous-local"]);
            if (launcherIds.Count == 0) return;
            var links = MobaProjectileLinkRollbackProvider.ReadLauncherSnapshot(
                context.RequirePayload(MobaProjectileLinkRollbackProvider.DefaultKey).ToArray());
            var lifecycle = MobaSkillLifecycleSnapshotIndex.Read(context.Frame,
                context.RequirePayload(MobaSkillLifecycleRollbackProvider.DefaultKey).ToArray());
            var physicsPayload = context.RequirePayload(ProjectileRollbackProvider.DefaultKey).ToArray();
            var physics = ProjectileRollbackProvider.ReadProjectileSnapshot(physicsPayload, context.Frame);
            var projectileCounts = new Dictionary<int, int>();
            foreach (var projectile in physics.Items)
                if (launcherIds.Contains(projectile.LauncherActorId))
                {
                    projectileCounts.TryGetValue(projectile.LauncherActorId, out var count);
                    projectileCounts[projectile.LauncherActorId] = count + 1;
                    var matches = false;
                    foreach (var component in components)
                        if (component.ActorId == projectile.LauncherActorId && component.HasProjectileLauncher &&
                            component.LauncherProjectileId == projectile.TemplateId &&
                            component.LauncherRootActorId == projectile.RootActorId) matches = true;
                    if (!matches) throw new InvalidOperationException("Physical projectile disagrees with its target Launcher.");
                }
            foreach (var component in components)
                if (component.HasProjectileLauncher)
                {
                    projectileCounts.TryGetValue(component.ActorId, out var count);
                    if (component.LauncherActiveBullets != count)
                        throw new InvalidOperationException("Launcher active bullet count disagrees with target physical projectiles.");
                }
            var launches = new Dictionary<int, MobaContinuousLocalCheckpoint>();
            foreach (var entry in order)
                if (entry?.Type == typeof(MobaProjectileLaunchContinuous).FullName)
                {
                    if (entry.ProjectileLaunch == null || !launches.TryAdd(entry.ProjectileLaunch.LauncherActorId, entry))
                        throw new InvalidOperationException("Launcher Continuous checkpoint is missing or duplicated.");
                }
            var linked = new HashSet<int>();
            foreach (var link in links)
            {
                if (!launcherIds.Contains(link.ActorId) || !linked.Add(link.ActorId))
                    throw new InvalidOperationException("Launcher link is outside the target component roster.");
                if (!launches.TryGetValue(link.ActorId, out var entry))
                {
                    if (!_actors.TryGet(link.ActorId, out _))
                        throw new InvalidOperationException("Missing target Launcher has no owned Continuous checkpoint.");
                    continue;
                }
                var state = entry.ProjectileLaunch;
                /*
                 * 未关联技能 runtime 的存活对象仍只允许原地恢复，不获得重建资格。
                 */
                if (!link.Retain.IsValid && _actors.TryGet(link.ActorId, out _))
                {
                    var survives = false;
                    foreach (var instance in _continuous.GetAllContinuous())
                        if (instance is MobaProjectileLaunchContinuous current &&
                            MobaContinuousLocalCheckpoint.Capture(current).Key == entry.Key) survives = true;
                    if (survives) continue;
                }
                if (_projectileService == null)
                    throw new InvalidOperationException("Launcher recovery requires the world projectile service.");
                var detached = _projectileService.PrepareRollbackLaunch(state);
                var request = detached.Request;
                var original = request.SourceContext;
                original.TryGetOrigin(out var origin);
                var source = link.Source;
                var retain = link.Retain;
                if (!retain.IsValid || !retain.Runtime.Equals(original.SkillRuntimeHandle) ||
                    !source.SkillRuntimeHandle.Equals(original.SkillRuntimeHandle) ||
                    !origin.SkillRuntimeHandle.Equals(original.SkillRuntimeHandle) ||
                    original.RootContextId != original.SkillRuntimeHandle.RootContextId ||
                    source.RootContextId != original.RootContextId || source.OwnerContextId != original.OwnerContextId ||
                    source.SourceActorId != state.CasterActorId || state.SourceActorId != state.CasterActorId ||
                    source.InitialTargetActorId != original.InitialTargetActorId || source.ProjectileConfigId != state.ProjectileId ||
                    retain.Child.Kind != MobaSkillRuntimeChildKind.ProjectileLauncher ||
                    retain.Child.ChildId != link.ActorId || retain.Child.ConfigId != state.LauncherId ||
                    retain.Child.ContextId != source.SourceContextId ||
                    !lifecycle.Contexts.TryGetValue(source.SourceContextId, out var node) || node.IsEnded ||
                    node.Kind != MobaExecutionKind.ProjectileLaunch || node.ConfigId != state.ProjectileId ||
                    node.SourceActorId != state.CasterActorId || node.TargetActorId != original.InitialTargetActorId ||
                    node.ParentContextId != origin.EffectiveParentContextId || node.RootContextId != source.RootContextId ||
                    node.OwnerContextId != source.OwnerContextId || node.CreatedFrame != state.StartFrame ||
                    !lifecycle.Contexts.ContainsKey(original.SourceContextId) ||
                    !lifecycle.Contexts.ContainsKey(origin.EffectiveParentContextId) ||
                    origin.EffectiveRootContextId != original.RootContextId || origin.OwnerContextId != original.OwnerContextId)
                    throw new InvalidOperationException("Launcher source, request, parent runtime or retain relationships disagree.");
                lifecycle.RequireRetain(in retain);
                var matched = false;
                foreach (var component in components)
                    if (component.ActorId == link.ActorId && component.HasProjectileLauncher)
                    {
                        if (component.LauncherId != state.LauncherId || component.LauncherProjectileId != state.ProjectileId ||
                            component.LauncherRootActorId != state.CasterActorId || component.ScheduleId != state.ScheduleId ||
                            component.LauncherIntervalFrames != state.IntervalFrames ||
                            component.LauncherTotalCount != state.TotalCount || component.LauncherEndTimeMs != state.EndTimeMs)
                            throw new InvalidOperationException("Launcher component and Continuous result fields disagree.");
                        matched = true;
                    }
                if (!matched) throw new InvalidOperationException("Launcher has no target component snapshot.");
            }
            if (!linked.SetEquals(launcherIds))
                throw new InvalidOperationException("Target Launcher component has no source link.");
            foreach (var launch in launches)
                if (lifecycle.ActorIds.Contains(launch.Key) && !linked.Contains(launch.Key))
                    throw new InvalidOperationException("Target launch Continuous has no Launcher relationship.");
        }
    }
}
