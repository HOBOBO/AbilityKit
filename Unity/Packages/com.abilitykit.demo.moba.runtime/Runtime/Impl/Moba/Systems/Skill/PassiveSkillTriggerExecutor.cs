using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    internal sealed class PassiveSkillTriggerExecutor
    {
        private readonly TriggerRunner _triggers;
        private readonly EffectSourceRegistry _effectSource;
        private readonly IFrameTime _frameTime;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _planEventBus;

        public PassiveSkillTriggerExecutor(TriggerRunner triggers, EffectSourceRegistry effectSource, IFrameTime frameTime, AbilityKit.Triggering.Eventing.IEventBus planEventBus)
        {
            _triggers = triggers;
            _effectSource = effectSource;
            _frameTime = frameTime;
            _planEventBus = planEventBus;
        }

        public void ExecuteOnce(global::ActorEntity entity, PassiveSkillMO passiveSkill, PassiveSkillTriggerListenerRuntime listener)
        {
            if (entity == null || passiveSkill == null || listener == null) return;
            if (_triggers == null || _frameTime == null) return;
            if (!entity.hasActorId) return;

            try
            {
                var rt = TryGetPassiveSkillRuntime(entity, passiveSkill.Id);
                if (rt == null) return;

                var nowMs = GetNowMs();
                if (rt.CooldownEndTimeMs > 0 && nowMs < rt.CooldownEndTimeMs)
                {
                    return;
                }

                var entries = listener.Entries;
                if (entries == null || entries.Count == 0) return;

                PublishPlanEvent(
                    passiveSkillId: passiveSkill.Id,
                    triggerId: listener.TriggerId,
                    payload: null,
                    listener: listener,
                    entity: entity,
                    isExternalEvent: 0);

                var args = PooledTriggerArgs.Rent();
                try
                {
                    var selfId = entity.actorId.Value;
                    args[EffectSourceKeys.SourceActorId] = selfId;
                    args[EffectSourceKeys.TargetActorId] = selfId;
                    args[EffectTriggering.Args.Source] = selfId;
                    args[EffectTriggering.Args.Target] = selfId;
                    if (listener.SourceContextId != 0)
                    {
                        args[EffectSourceKeys.SourceContextId] = listener.SourceContextId;

                        EffectOriginArgsHelper.FillFromRegistry(args, listener.SourceContextId, _effectSource);
                    }

                    if (!args.ContainsKey(EffectTriggering.Args.OriginSource)) args[EffectTriggering.Args.OriginSource] = selfId;
                    if (!args.ContainsKey(EffectTriggering.Args.OriginTarget)) args[EffectTriggering.Args.OriginTarget] = selfId;

                    var triggered = false;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var def = e?.Def;
                        if (def == null) continue;

                        if (_triggers.RunOnce(def, source: entity, target: entity, payload: null, args: args, initialLocalVars: e.InitialLocalVars))
                        {
                            triggered = true;
                        }
                    }

                    if (triggered && passiveSkill.CooldownMs > 0)
                    {
                        rt.CooldownEndTimeMs = nowMs + passiveSkill.CooldownMs;
                    }
                }
                finally
                {
                    args.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] ExecutePassiveTriggerOnce exception (passiveSkillId={passiveSkill.Id}, triggerId={listener.TriggerId})");
            }
        }

        public void HandleEvent(global::ActorEntity entity, PassiveSkillMO passiveSkill, PassiveSkillTriggerListenerRuntime listener, in SkillCastContext payload)
        {
            if (entity == null || passiveSkill == null || listener == null) return;
            if (_triggers == null || _frameTime == null) return;

            try
            {
                if (string.IsNullOrEmpty(listener.EventId))
                {
                    return;
                }

                var isExternalEvent = false;
                if (entity.hasActorId)
                {
                    var selfId = entity.actorId.Value;
                    var sourceActorId = payload != null ? payload.CasterActorId : 0;
                    if (sourceActorId != 0 && sourceActorId != selfId)
                    {
                        isExternalEvent = true;
                    }
                }

                var rt = TryGetPassiveSkillRuntime(entity, passiveSkill.Id);
                if (rt == null) return;

                var nowMs = GetNowMs();
                if (rt.CooldownEndTimeMs > 0 && nowMs < rt.CooldownEndTimeMs)
                {
                    return;
                }

                var entries = listener.Entries;
                if (entries == null || entries.Count == 0) return;

                PublishPlanEvent(
                    passiveSkillId: passiveSkill.Id,
                    triggerId: listener.TriggerId,
                    payload: payload,
                    listener: listener,
                    entity: entity,
                    isExternalEvent: isExternalEvent ? 1 : 0);

                var args = PooledTriggerArgs.Rent();
                try
                {
                    if (payload != null)
                    {
                        args[EffectTriggering.Args.Source] = payload.CasterActorId;
                        args[EffectTriggering.Args.Target] = payload.TargetActorId;
                        args[EffectTriggering.Args.OriginSource] = payload.CasterActorId;
                        args[EffectTriggering.Args.OriginTarget] = payload.TargetActorId;
                        args[EffectTriggering.Args.OriginKind] = EffectSourceKind.SkillCast;
                        args[EffectTriggering.Args.OriginConfigId] = payload.SkillId;
                        args[EffectTriggering.Args.OriginContextId] = payload.SourceContextId;

                        args[MobaSkillTriggering.Args.SkillId] = payload.SkillId;
                        args[MobaSkillTriggering.Args.SkillSlot] = payload.SkillSlot;
                        args[MobaSkillTriggering.Args.CasterActorId] = payload.CasterActorId;
                        args[MobaSkillTriggering.Args.TargetActorId] = payload.TargetActorId;
                        args[MobaSkillTriggering.Args.AimPos] = payload.AimPos;
                        args[MobaSkillTriggering.Args.AimDir] = payload.AimDir;
                        args[MobaSkillTriggerArgs.SkillLevel] = payload.SkillLevel;
                    }

                    if (listener.SourceContextId != 0)
                    {
                        args[EffectSourceKeys.SourceContextId] = listener.SourceContextId;
                    }

                    args["common.is_external"] = isExternalEvent ? 1 : 0;

                    var triggered = false;
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var def = e?.Def;
                        if (def == null) continue;

                        if (_triggers.RunOnce(def, source: null, target: null, payload: payload, args: args, initialLocalVars: e.InitialLocalVars))
                        {
                            triggered = true;
                        }
                    }

                    if (triggered && passiveSkill.CooldownMs > 0)
                    {
                        rt.CooldownEndTimeMs = nowMs + passiveSkill.CooldownMs;
                    }
                }
                finally
                {
                    args.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] handler exception (passiveSkillId={passiveSkill.Id}, triggerId={listener.TriggerId}, eventId={listener.EventId})");
            }
        }

        private long GetNowMs()
        {
            return (long)(_frameTime.Time * 1000f);
        }

        private void PublishPlanEvent(int passiveSkillId, int triggerId, SkillCastContext payload, PassiveSkillTriggerListenerRuntime listener, global::ActorEntity entity, int isExternalEvent)
        {
            if (_planEventBus == null) return;
            if (listener == null) return;
            if (entity == null || !entity.hasActorId) return;

            try
            {
                var selfId = entity.actorId.Value;

                var sourceActorId = payload != null ? payload.CasterActorId : selfId;
                var targetActorId = payload != null ? payload.TargetActorId : selfId;

                var skillId = payload != null ? payload.SkillId : 0;
                var skillSlot = payload != null ? payload.SkillSlot : 0;
                var skillLevel = payload != null ? payload.SkillLevel : 0;

                var aimPos = payload != null ? payload.AimPos : AbilityKit.Ability.Share.Math.Vec3.Zero;
                var aimDir = payload != null ? payload.AimDir : AbilityKit.Ability.Share.Math.Vec3.Forward;

                var originKind = payload != null ? EffectSourceKind.SkillCast : EffectSourceKind.System;
                var originConfigId = payload != null ? payload.SkillId : passiveSkillId;
                var originContextId = payload != null ? payload.SourceContextId : listener.SourceContextId;
                var originSource = payload != null ? payload.CasterActorId : selfId;
                var originTarget = payload != null ? payload.TargetActorId : selfId;

                var evt = new PassiveSkillTriggerEventArgs(
                    passiveSkillId: passiveSkillId,
                    triggerId: triggerId,
                    sourceContextId: listener.SourceContextId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    skillId: skillId,
                    skillSlot: skillSlot,
                    skillLevel: skillLevel,
                    aimPos: in aimPos,
                    aimDir: in aimDir,
                    isExternalEvent: isExternalEvent,
                    originKind: originKind,
                    originConfigId: originConfigId,
                    originContextId: originContextId,
                    originSourceActorId: originSource,
                    originTargetActorId: originTarget);

                _planEventBus.Publish(PassiveSkillTriggerEventArgs.EventKey, in evt);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaPassiveSkillTriggerRegisterSystem] publish plan event failed (passiveSkillId={passiveSkillId}, triggerId={triggerId})");
            }
        }

        private static PassiveSkillRuntime TryGetPassiveSkillRuntime(global::ActorEntity entity, int passiveSkillId)
        {
            if (entity == null || !entity.hasSkillLoadout) return null;
            var list = entity.skillLoadout.PassiveSkills;
            if (list == null || list.Length == 0) return null;

            for (int i = 0; i < list.Length; i++)
            {
                var rt = list[i];
                if (rt == null) continue;
                if (rt.PassiveSkillId == passiveSkillId) return rt;
            }

            return null;
        }
    }
}
