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

        private SkillExecutor _skills;
        private IFrameTime _time;
        private MobaAuthorityFrameService _authority;

        private global::Entitas.IGroup<global::ActorEntity> _actors;
        private global::ActorContext _actorContext;

        private readonly List<SkillPipelineRunner.RunningSnapshot> _buffer = new List<SkillPipelineRunner.RunningSnapshot>(8);
        private readonly List<SkillPipelineRunner.RunningSnapshot> _endedBuffer = new List<SkillPipelineRunner.RunningSnapshot>(4);
        private readonly HashSet<long> _seenThisFrame = new HashSet<long>();
        private readonly Dictionary<long, global::ActorEntity> _byInstanceId = new Dictionary<long, global::ActorEntity>();
        private readonly Dictionary<long, int> _lastSeenFrame = new Dictionary<long, int>();

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
            if (_skills == null) return;
            if (_actors == null) return;

            var frame = 0;
            try { frame = _authority != null ? _authority.PredictedFrame.Value : (_time != null ? _time.Frame.Value : 0); }
            catch { frame = 0; }

            var entities = _actors.GetEntities();
            if (entities == null || entities.Length == 0) return;

            _seenThisFrame.Clear();

            for (int i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor == null || !actor.hasActorId) continue;

                _skills.FillRunningSnapshots(actor.actorId.Value, _buffer);
                if (_buffer.Count == 0) continue;

                for (int j = 0; j < _buffer.Count; j++)
                {
                    var s = _buffer[j];
                    if (s.InstanceId == 0) continue;

                    _seenThisFrame.Add(s.InstanceId);
                    _lastSeenFrame[s.InstanceId] = frame;

                    if (!_byInstanceId.TryGetValue(s.InstanceId, out var inst) || inst == null)
                    {
                        inst = _actorContext.CreateEntity();
                        inst.AddSkillCastInstanceId(s.InstanceId);
                        _byInstanceId[s.InstanceId] = inst;
                    }

                    if (!inst.hasSkillCastOwnerActorId) inst.AddSkillCastOwnerActorId(s.OwnerActorId);
                    else inst.ReplaceSkillCastOwnerActorId(s.OwnerActorId);

                    if (!inst.hasSkillCastSkillId) inst.AddSkillCastSkillId(s.SkillId);
                    else inst.ReplaceSkillCastSkillId(s.SkillId);

                    if (!inst.hasSkillCastSlot) inst.AddSkillCastSlot(s.SkillSlot);
                    else inst.ReplaceSkillCastSlot(s.SkillSlot);

                    if (!inst.hasSkillCastSkillLevel) inst.AddSkillCastSkillLevel(s.SkillLevel);
                    else inst.ReplaceSkillCastSkillLevel(s.SkillLevel);

                    if (!inst.hasSkillCastSequence) inst.AddSkillCastSequence(s.Sequence);
                    else inst.ReplaceSkillCastSequence(s.Sequence);

                    if (!inst.hasSkillCastStartFrame) inst.AddSkillCastStartFrame(s.StartFrame);
                    else if (inst.skillCastStartFrame.Value == 0 && s.StartFrame != 0) inst.ReplaceSkillCastStartFrame(s.StartFrame);

                    if (!inst.hasSkillCastStage) inst.AddSkillCastStage(s.Stage);
                    else inst.ReplaceSkillCastStage(s.Stage);

                    if (!inst.hasSkillCastTargetActorId) inst.AddSkillCastTargetActorId(s.TargetActorId);
                    else inst.ReplaceSkillCastTargetActorId(s.TargetActorId);

                    if (!inst.hasSkillCastAim) inst.AddSkillCastAim(s.AimPos, s.AimDir);
                    else inst.ReplaceSkillCastAim(s.AimPos, s.AimDir);

                    if (!inst.hasSkillCastTimelineRuntime)
                    {
                        inst.AddSkillCastTimelineRuntime(s.ElapsedMs, s.NextEventIndex);
                    }
                    else
                    {
                        inst.ReplaceSkillCastTimelineRuntime(s.ElapsedMs, s.NextEventIndex);
                    }

                    if (!inst.isSkillCastRunningTag) inst.isSkillCastRunningTag = true;

                    if (inst.hasSkillCastCancelRequest) inst.RemoveSkillCastCancelRequest();
                }
            }

            for (int i = 0; i < entities.Length; i++)
            {
                var actor = entities[i];
                if (actor == null || !actor.hasActorId) continue;

                _skills.FillEndedSnapshots(actor.actorId.Value, _endedBuffer);
                if (_endedBuffer.Count == 0) continue;

                for (int j = 0; j < _endedBuffer.Count; j++)
                {
                    var s = _endedBuffer[j];
                    if (s.InstanceId == 0) continue;

                    _seenThisFrame.Add(s.InstanceId);
                    _lastSeenFrame[s.InstanceId] = frame;

                    if (!_byInstanceId.TryGetValue(s.InstanceId, out var inst) || inst == null)
                    {
                        inst = _actorContext.CreateEntity();
                        inst.AddSkillCastInstanceId(s.InstanceId);
                        _byInstanceId[s.InstanceId] = inst;
                    }

                    if (!inst.hasSkillCastOwnerActorId) inst.AddSkillCastOwnerActorId(s.OwnerActorId);
                    else inst.ReplaceSkillCastOwnerActorId(s.OwnerActorId);

                    if (!inst.hasSkillCastSkillId) inst.AddSkillCastSkillId(s.SkillId);
                    else inst.ReplaceSkillCastSkillId(s.SkillId);

                    if (!inst.hasSkillCastSlot) inst.AddSkillCastSlot(s.SkillSlot);
                    else inst.ReplaceSkillCastSlot(s.SkillSlot);

                    if (!inst.hasSkillCastSkillLevel) inst.AddSkillCastSkillLevel(s.SkillLevel);
                    else inst.ReplaceSkillCastSkillLevel(s.SkillLevel);

                    if (!inst.hasSkillCastSequence) inst.AddSkillCastSequence(s.Sequence);
                    else inst.ReplaceSkillCastSequence(s.Sequence);

                    if (!inst.hasSkillCastStartFrame) inst.AddSkillCastStartFrame(s.StartFrame);
                    else if (inst.skillCastStartFrame.Value == 0 && s.StartFrame != 0) inst.ReplaceSkillCastStartFrame(s.StartFrame);

                    if (!inst.hasSkillCastStage) inst.AddSkillCastStage(s.Stage);
                    else inst.ReplaceSkillCastStage(s.Stage);

                    if (!inst.hasSkillCastTargetActorId) inst.AddSkillCastTargetActorId(s.TargetActorId);
                    else inst.ReplaceSkillCastTargetActorId(s.TargetActorId);

                    if (!inst.hasSkillCastAim) inst.AddSkillCastAim(s.AimPos, s.AimDir);
                    else inst.ReplaceSkillCastAim(s.AimPos, s.AimDir);

                    if (!inst.hasSkillCastTimelineRuntime)
                    {
                        inst.AddSkillCastTimelineRuntime(s.ElapsedMs, s.NextEventIndex);
                    }
                    else
                    {
                        inst.ReplaceSkillCastTimelineRuntime(s.ElapsedMs, s.NextEventIndex);
                    }

                    if (inst.isSkillCastRunningTag) inst.isSkillCastRunningTag = false;
                }
            }

            CleanupNotSeenRunningInstances();
        }

        private void CleanupNotSeenRunningInstances()
        {
            if (_byInstanceId.Count == 0) return;

            var frame = 0;
            try { frame = _authority != null ? _authority.PredictedFrame.Value : (_time != null ? _time.Frame.Value : 0); }
            catch { frame = 0; }

            List<long> stale = null;

            foreach (var kv in _byInstanceId)
            {
                var id = kv.Key;
                var e = kv.Value;
                if (e == null || !e.isEnabled)
                {
                    stale ??= new List<long>(4);
                    stale.Add(id);
                    continue;
                }

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

                if (_retainCompletedFrames > 0 && frame > 0 && _lastSeenFrame.TryGetValue(id, out var last))
                {
                    if (frame - last > _retainCompletedFrames)
                    {
                        var requestFrame = frame;
                        var minConfirmed = requestFrame + _destroyConfirmGateFrames;
                        if (!e.hasSkillCastDestroyRequest)
                        {
                            e.AddSkillCastDestroyRequest(requestFrame, minConfirmed, destroyReason);
                        }
                        else
                        {
                            e.ReplaceSkillCastDestroyRequest(requestFrame, minConfirmed, destroyReason);
                        }
                    }
                }
            }

            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    var id = stale[i];
                    _lastSeenFrame.Remove(id);
                    _seenThisFrame.Remove(id);
                    _byInstanceId.Remove(id);
                }
            }

            // Note: actual entity destruction is handled by MobaSkillCastDestroyCleanupSystem,
            // gated by authoritative confirmed frames.
        }
    }
}
