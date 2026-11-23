using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(order: MobaSystemOrder.SkillPipelines + 1, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaSkillCastInstanceSyncSystem : WorldSystemBase
    {
        private int _retainCompletedFrames = 30;
        private int _destroyConfirmGateFrames = 10;

        private SkillCastCoordinator _skills;
        private IFrameTime _time;
        private MobaAuthorityFrameService _authority;

        private global::Entitas.IGroup<global::ActorEntity> _actors;
        private global::ActorContext _actorContext;

        private readonly List<SkillPipelineRunner.RunningSnapshot> _buffer = new List<SkillPipelineRunner.RunningSnapshot>(8);
        private readonly List<SkillPipelineRunner.RunningSnapshot> _endedBuffer = new List<SkillPipelineRunner.RunningSnapshot>(4);
        private readonly HashSet<long> _seenThisFrame = new HashSet<long>();
        private readonly Dictionary<long, global::ActorEntity> _byInstanceId = new Dictionary<long, global::ActorEntity>();

        public MobaSkillCastInstanceSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _skills);
            Services.TryResolve(out _time);
            Services.TryResolve(out _authority);

            if (Services.TryResolve<MobaSkillCastInstanceSyncSettings>(out var settings) && settings != null)
            {
                _retainCompletedFrames = settings.RetainCompletedFrames;
                _destroyConfirmGateFrames = settings.DestroyConfirmGateFrames;
            }

            _actorContext = Contexts.Actor();
            _actors = _actorContext.GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId));
        }

        protected override void OnExecute()
        {
            MobaWorldSystemExecution.Require(
                _skills != null && _actors != null && _actorContext != null,
                Services,
                nameof(MobaSkillCastInstanceSyncSystem),
                "skill.cast.instance.sync",
                "SkillCastCoordinator, actor group and actor context",
                $"hasSkills={_skills != null}, hasActors={_actors != null}, hasActorContext={_actorContext != null}");

            var frame = ResolveFrame("skill.cast.instance.resolveFrame");

            var entities = _actors.GetEntities();
            _seenThisFrame.Clear();
            // Derived index only: rollback and Entitas pooling can invalidate every entry.
            _byInstanceId.Clear();
            foreach (var cast in _actorContext.GetGroup(ActorMatcher.SkillCastInstanceId).GetEntities())
            {
                if (_byInstanceId.ContainsKey(cast.skillCastInstanceId.Value))
                    throw new InvalidOperationException("Duplicate Cast instance identity.");
                _byInstanceId.Add(cast.skillCastInstanceId.Value, cast);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor == null || !actor.hasActorId) continue;

                _skills.FillRunningSnapshots(actor.actorId.Value, _buffer);
                UpsertSkillCastInstances(_buffer, frame, isRunning: true);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor == null || !actor.hasActorId) continue;

                _skills.FillEndedSnapshots(actor.actorId.Value, _endedBuffer);
                UpsertSkillCastInstances(_endedBuffer, frame, isRunning: false);
            }

            CleanupNotSeenRunningInstances(frame);
        }

        private void UpsertSkillCastInstances(List<SkillPipelineRunner.RunningSnapshot> snapshots, int frame, bool isRunning)
        {
            if (snapshots == null || snapshots.Count == 0) return;

            for (int i = 0; i < snapshots.Count; i++)
            {
                UpsertSkillCastInstance(snapshots[i], frame, isRunning);
            }
        }

        private void UpsertSkillCastInstance(in SkillPipelineRunner.RunningSnapshot snapshot, int frame, bool isRunning)
        {
            if (snapshot.InstanceId == 0) return;

            _seenThisFrame.Add(snapshot.InstanceId);
            if (!_byInstanceId.TryGetValue(snapshot.InstanceId, out var inst))
            {
                inst = _actorContext.CreateEntity();
                inst.AddSkillCastInstanceId(snapshot.InstanceId);
                _byInstanceId[snapshot.InstanceId] = inst;
            }
            inst.ReplaceSkillCastRetention(frame);

            if (!inst.hasSkillCastOwnerActorId) inst.AddSkillCastOwnerActorId(snapshot.OwnerActorId);
            else inst.ReplaceSkillCastOwnerActorId(snapshot.OwnerActorId);

            if (!inst.hasSkillCastSkillId) inst.AddSkillCastSkillId(snapshot.SkillId);
            else inst.ReplaceSkillCastSkillId(snapshot.SkillId);

            if (!inst.hasSkillCastSlot) inst.AddSkillCastSlot(snapshot.SkillSlot);
            else inst.ReplaceSkillCastSlot(snapshot.SkillSlot);

            if (!inst.hasSkillCastSkillLevel) inst.AddSkillCastSkillLevel(snapshot.SkillLevel);
            else inst.ReplaceSkillCastSkillLevel(snapshot.SkillLevel);

            if (!inst.hasSkillCastSequence) inst.AddSkillCastSequence(snapshot.Sequence);
            else inst.ReplaceSkillCastSequence(snapshot.Sequence);

            if (!inst.hasSkillCastStartFrame) inst.AddSkillCastStartFrame(snapshot.StartFrame);
            else if (inst.skillCastStartFrame.Value == 0 && snapshot.StartFrame != 0) inst.ReplaceSkillCastStartFrame(snapshot.StartFrame);

            if (!inst.hasSkillCastStage) inst.AddSkillCastStage(snapshot.Stage);
            else inst.ReplaceSkillCastStage(snapshot.Stage);

            if (!inst.hasSkillCastTargetActorId) inst.AddSkillCastTargetActorId(snapshot.TargetActorId);
            else inst.ReplaceSkillCastTargetActorId(snapshot.TargetActorId);

            if (!inst.hasSkillCastAim) inst.AddSkillCastAim(snapshot.AimPos, snapshot.AimDir);
            else inst.ReplaceSkillCastAim(snapshot.AimPos, snapshot.AimDir);

            if (!inst.hasSkillCastTimelineRuntime)
            {
                inst.AddSkillCastTimelineRuntime(snapshot.ElapsedMs, snapshot.NextEventIndex);
            }
            else
            {
                inst.ReplaceSkillCastTimelineRuntime(snapshot.ElapsedMs, snapshot.NextEventIndex);
            }

            inst.isSkillCastRunningTag = isRunning;

            if (isRunning && inst.hasSkillCastCancelRequest)
            {
                inst.RemoveSkillCastCancelRequest();
            }
            if (isRunning && inst.hasSkillCastDestroyRequest) inst.RemoveSkillCastDestroyRequest();
        }

        private void CleanupNotSeenRunningInstances(int frame)
        {
            if (_byInstanceId.Count == 0) return;

            foreach (var kv in _byInstanceId)
            {
                var id = kv.Key;
                var e = kv.Value;
                if (_seenThisFrame.Contains(id)) continue;

                if (e.isSkillCastRunningTag)
                {
                    e.isSkillCastRunningTag = false;
                    if (!e.hasSkillCastStage)
                    {
                        e.AddSkillCastStage(SkillCastStage.Completed);
                    }
                    else
                    {
                        var stage = e.skillCastStage.Value;
                        if (stage == SkillCastStage.PreCast || stage == SkillCastStage.Cast || stage == SkillCastStage.Channeling)
                        {
                            e.ReplaceSkillCastStage(SkillCastStage.Completed);
                        }
                    }
                }

                var destroyReason = SkillDestroyReason.Unknown;
                if (e.hasSkillCastStage)
                {
                    switch (e.skillCastStage.Value)
                    {
                        case SkillCastStage.Failed:
                            destroyReason = SkillDestroyReason.FailedRetainExpired;
                            break;
                        case SkillCastStage.Cancelled:
                            destroyReason = SkillDestroyReason.CancelledRetainExpired;
                            break;
                        case SkillCastStage.Completed:
                        default:
                            destroyReason = SkillDestroyReason.CompletedRetainExpired;
                            break;
                    }
                }
                if (e.hasSkillCastCancelRequest && destroyReason != SkillDestroyReason.FailedRetainExpired)
                {
                    destroyReason = SkillDestroyReason.CancelledRetainExpired;
                }

                if (e.hasSkillCastCancelRequest) e.RemoveSkillCastCancelRequest();

                if (_retainCompletedFrames > 0 && !e.hasSkillCastDestroyRequest)
                {
                    if (!e.hasSkillCastRetention || e.skillCastRetention.LastObservedFrame < 0 ||
                        e.skillCastRetention.LastObservedFrame > frame)
                        throw new InvalidOperationException($"Cast {id} has an invalid retention anchor.");
                    var requestFrame = checked(e.skillCastRetention.LastObservedFrame + _retainCompletedFrames + 1);
                    var minConfirmed = checked(requestFrame + _destroyConfirmGateFrames);
                    if (frame >= requestFrame)
                        e.AddSkillCastDestroyRequest(requestFrame, minConfirmed, destroyReason);
                }
            }

            // Note: actual entity destruction is handled by MobaSkillCastDestroyCleanupSystem,
            // gated by authoritative confirmed frames.
        }

        private int ResolveFrame(string operation)
        {
            MobaWorldSystemExecution.Require(
                _authority != null || _time != null,
                Services,
                nameof(MobaSkillCastInstanceSyncSystem),
                operation,
                "MobaAuthorityFrameService or IFrameTime",
                $"hasAuthority={_authority != null}, hasFrameTime={_time != null}");

            if (_authority != null) return _authority.PredictedFrame.Value;
            return _time.Frame.Value;
        }

    }
}

