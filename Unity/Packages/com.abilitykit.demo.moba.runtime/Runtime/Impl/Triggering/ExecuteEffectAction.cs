using System;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    using AbilityKit.Ability;
    public sealed class ExecuteEffectAction : ITriggerAction
    {
        private readonly int _effectId;
        private readonly EffectExecuteMode _mode;

        public ExecuteEffectAction(int effectId, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            _effectId = effectId;
            _mode = mode;
        }

        public static ExecuteEffectAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new ExecuteEffectAction(0);

            var mode = EffectExecuteMode.InternalOnly;
            if (args.TryGetValue("executeMode", out var modeObj) && modeObj != null)
            {
                if (modeObj is EffectExecuteMode em) mode = em;
                else if (modeObj is int mi) mode = (EffectExecuteMode)mi;
                else if (modeObj is long ml) mode = (EffectExecuteMode)(int)ml;
                else if (modeObj is string ms && int.TryParse(ms, out var parsedMode)) mode = (EffectExecuteMode)parsedMode;
            }

            if (args.TryGetValue("effectId", out var idObj))
            {
                if (idObj is int i) return new ExecuteEffectAction(i, mode);
                if (idObj is long l) return new ExecuteEffectAction((int)l, mode);
                if (idObj is string s && int.TryParse(s, out var parsed)) return new ExecuteEffectAction(parsed, mode);
            }

            return new ExecuteEffectAction(0, mode);
        }

        public void Execute(TriggerContext context)
        {
            if (_effectId <= 0) return;

            IAbilityPipelineContext ctx = null;
            if (context?.Event.Payload is IAbilityPipelineContext pipelineCtx)
            {
                ctx = pipelineCtx;
            }
            else
            {
                SkillCastRequest req;
                if (context?.Event.Payload is SkillCastContext sc)
                {
                    req = new SkillCastRequest(
                        skillId: sc.SkillId,
                        skillSlot: sc.SkillSlot,
                        casterActorId: sc.CasterActorId,
                        targetActorId: sc.TargetActorId,
                        aimPos: in sc.AimPos,
                        aimDir: in sc.AimDir,
                        worldServices: sc.WorldServices,
                        eventBus: sc.EventBus,
                        casterUnit: sc.CasterUnit,
                        targetUnit: sc.TargetUnit);
                }
                else
                {
                    req = default;
                }

                if (req.SkillId != 0)
                {
                    var skillCtx = new SkillPipelineContext();
                    skillCtx.Initialize(abilityInstance: null, in req);
                    ctx = skillCtx;
                }
            }

            if (ctx == null)
            {
                Log.Warning($"[Trigger] effect_execute requires payload IAbilityPipelineContext / SkillCastContext, got: {context?.Event.Payload?.GetType().FullName ?? "null"}");
                return;
            }

            var svc = context?.Services?.GetService(typeof(MobaEffectExecutionService)) as MobaEffectExecutionService;
            if (svc == null)
            {
                Log.Warning("[Trigger] effect_execute cannot resolve MobaEffectExecutionService from DI");
                return;
            }

            svc.Execute(_effectId, ctx, _mode);
        }
    }
}
