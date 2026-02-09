using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillExecutor : IService
    {
        private readonly IWorldResolver _services;
        private readonly IWorldClock _clock;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;
        private readonly MobaSkillLoadoutService _loadout;
        private readonly MobaActorLookupService _actors;
        private readonly IMobaSkillPipelineLibrary _library;

        private readonly Dictionary<int, SkillPipelineRunner> _runners = new Dictionary<int, SkillPipelineRunner>();
        private readonly Dictionary<int, int> _castSequenceByActor = new Dictionary<int, int>();

        public bool AllowParallel { get; set; }
        public bool InterruptRunning { get; set; }

        public SkillExecutor(
            IWorldResolver services,
            IWorldClock clock,
            IFrameTime time,
            IEventBus eventBus,
            IUnitResolver units,
            MobaSkillLoadoutService loadout,
            MobaActorLookupService actors,
            IMobaSkillPipelineLibrary library)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _eventBus = eventBus;
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        private SkillPipelineRunner GetOrCreateRunner(int actorId)
        {
            if (!_runners.TryGetValue(actorId, out var r) || r == null)
            {
                r = new SkillPipelineRunner(actorId);
                _runners[actorId] = r;
            }

            return r;
        }

        public bool CastBySlot(int actorId, int slot)
        {
            return CastBySlot(actorId, slot, out _);
        }

        public bool CastBySlot(int actorId, int slot, out string failReason)
        {
            failReason = null;

            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                failReason = "Skill not found in slot.";
                return false;
            }

            return CastSkill(actorId, skillId, slot, out failReason);
        }

        public bool HandleInput(int actorId, in SkillInputEvent evt)
        {
            if (actorId <= 0) return false;
            if (evt.Slot <= 0) return false;

            switch (evt.Phase)
            {
                case SkillInputPhase.Press:
                    return CastBySlot(actorId, evt.Slot);
                case SkillInputPhase.Release:
                    return CastBySlot(actorId, evt.Slot, in evt.AimPos, in evt.AimDir, out _);
                case SkillInputPhase.Hold:
                case SkillInputPhase.Cancel:
                default:
                    // Not implemented yet: reserved for charge/channel/confirm/cancel.
                    return false;
            }
        }

        public bool CastBySlot(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, out string failReason)
        {
            failReason = null;

            if (!_loadout.TryGetSkillId(actorId, slot, out var skillId))
            {
                failReason = "Skill not found in slot.";
                return false;
            }

            return CastSkill(actorId, skillId, slot, in aimPos, in aimDir, out failReason);
        }

        public bool CastSkill(int actorId, int skillId)
        {
            return CastSkill(actorId, skillId, slot: 0, out _);
        }

        public bool CastSkill(int actorId, int skillId, int slot, out string failReason)
        {
            return CastSkillInternal(actorId, skillId, slot, aimPos: default, aimDir: default, hasAim: false, out failReason);
        }

        public bool CastSkill(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir, out string failReason)
        {
            return CastSkillInternal(actorId, skillId, slot, aimPos, aimDir, hasAim: true, out failReason);
        }

        private bool CastSkillInternal(int actorId, int skillId, int slot, in Vec3 aimPos, in Vec3 aimDir, bool hasAim, out string failReason)
        {
            failReason = null;
            if (actorId <= 0) return false;
            if (skillId <= 0) return false;

            if (!_units.TryResolve(new EcsEntityId(actorId), out var caster) || caster == null)
            {
                failReason = "Caster not found.";
                return false;
            }

            var casterPos = Vec3.Zero;
            var casterForward = Vec3.Forward;
            if (_actors.TryGetActorEntity(actorId, out var actorEntity) && actorEntity != null && actorEntity.hasTransform)
            {
                var t = actorEntity.transform.Value;
                casterPos = t.Position;
                casterForward = t.Rotation.Rotate(Vec3.Forward).Normalized;
            }

            var finalAimPos = hasAim ? aimPos : casterPos;
            var finalAimDir = hasAim ? aimDir : casterForward;
            if (finalAimDir.Equals(Vec3.Zero)) finalAimDir = casterForward;
            if (finalAimPos.Equals(Vec3.Zero)) finalAimPos = casterPos;

            if (!_library.TryGet(skillId, out var preConfig, out var prePhases, out var castConfig, out var castPhases))
            {
                failReason = "Skill pipeline not found.";
                return false;
            }

            var req = new SkillCastRequest(
                skillId: skillId,
                skillSlot: slot,
                casterActorId: actorId,
                targetActorId: actorId,
                aimPos: in finalAimPos,
                aimDir: in finalAimDir,
                worldServices: _services,
                eventBus: _eventBus,
                casterUnit: caster,
                targetUnit: caster
            );

            var skillLevel = 0;
            if (_actors != null && _actors.TryGetActorEntity(actorId, out var ae) && ae != null && ae.hasSkillLoadout)
            {
                var skills = ae.skillLoadout.ActiveSkills;
                var idx = slot - 1;
                if (skills != null && idx >= 0 && idx < skills.Length)
                {
                    var rt = skills[idx];
                    if (rt != null && rt.SkillId == skillId) skillLevel = rt.Level;
                }
            }
            var ctx = SkillCastContext.FromRequest(in req, skillLevel);

            if (_castSequenceByActor.TryGetValue(actorId, out var seq))
            {
                seq++;
            }
            else
            {
                seq = 1;
            }
            _castSequenceByActor[actorId] = seq;
            ctx.Sequence = seq;

            try
            {
                var frame = 0;
                try { frame = _time != null ? _time.Frame.Value : 0; }
                catch { frame = 0; }

                var effectSource = _services != null ? _services.Resolve<EffectSourceRegistry>() : null;
                if (effectSource != null)
                {
                    ctx.SourceContextId = effectSource.CreateRoot(
                        kind: EffectSourceKind.SkillCast,
                        configId: skillId,
                        sourceActorId: actorId,
                        targetActorId: actorId,
                        frame: frame,
                        originSource: actorId,
                        originTarget: actorId);

                    if (ctx.SourceContextId != 0)
                    {
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.EffectSourceKind, (int)EffectSourceKind.SkillCast);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillId, skillId);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillSlot, slot);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillLevel, skillLevel);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillSequence, ctx.Sequence);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillCasterActorId, actorId);
                        effectSource.SetRootInt(ctx.SourceContextId, EffectSourceRegistry.RootIntKeys.SkillTargetActorId, actorId);
                    }
                }
            }
            catch
            {
                ctx.SourceContextId = 0;
            }

            var runner = GetOrCreateRunner(actorId);
            return runner.Start(preConfig, prePhases, castConfig, castPhases, abilityInstance: this, in req, ctx, out failReason, allowParallel: AllowParallel, interruptRunning: InterruptRunning);
        }

        public void CancelAll(int actorId)
        {
            if (actorId <= 0) return;
            if (_runners.TryGetValue(actorId, out var r) && r != null)
            {
                r.CancelAll();
            }
        }

        public void CancelBySkillId(int actorId, int skillId)
        {
            if (actorId <= 0) return;
            if (skillId <= 0) return;
            if (_runners.TryGetValue(actorId, out var r) && r != null)
            {
                r.CancelBySkillId(skillId);
            }
        }

        public void Step(int actorId)
        {
            if (actorId <= 0) return;
            if (!_runners.TryGetValue(actorId, out var r) || r == null) return;

            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            r.Step(dt);
        }

        public void FillRunningSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (!_runners.TryGetValue(actorId, out var r) || r == null)
            {
                buffer.Clear();
                return;
            }

            r.FillRunningSnapshots(buffer);
        }

        public void FillEndedSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (!_runners.TryGetValue(actorId, out var r) || r == null)
            {
                buffer.Clear();
                return;
            }

            r.FillEndedSnapshots(buffer);
        }

        public void Dispose()
        {
            foreach (var kv in _runners)
            {
                kv.Value?.CancelAll();
            }
            _runners.Clear();

            _castSequenceByActor.Clear();
        }
    }
}
