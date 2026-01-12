using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class ExecuteEffectAction : ITriggerAction
    {
        private readonly int _effectId;

        public ExecuteEffectAction(int effectId)
        {
            _effectId = effectId;
        }

        public static ExecuteEffectAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new ExecuteEffectAction(0);

            if (args.TryGetValue("effectId", out var idObj))
            {
                if (idObj is int i) return new ExecuteEffectAction(i);
                if (idObj is long l) return new ExecuteEffectAction((int)l);
                if (idObj is string s && int.TryParse(s, out var parsed)) return new ExecuteEffectAction(parsed);
            }

            return new ExecuteEffectAction(0);
        }

        public void Execute(TriggerContext context)
        {
            if (_effectId <= 0) return;

            SkillPipelineContext ctx = null;
            if (context?.Event.Payload is SkillPipelineContext pipelineCtx)
            {
                ctx = pipelineCtx;
            }
            else if (context?.Event.Payload is SkillCastRequest req)
            {
                ctx = new SkillPipelineContext();
                ctx.Initialize(abilityInstance: null, in req);
            }

            if (ctx == null)
            {
                Log.Warning($"[Trigger] effect_execute requires payload SkillPipelineContext or SkillCastRequest, got: {context?.Event.Payload?.GetType().FullName ?? "null"}");
                return;
            }

            var svc = context?.Services?.GetService(typeof(MobaEffectExecutionService)) as MobaEffectExecutionService;
            if (svc == null)
            {
                Log.Warning("[Trigger] effect_execute cannot resolve MobaEffectExecutionService from DI");
                return;
            }

            svc.Execute(_effectId, ctx);
        }
    }
}
