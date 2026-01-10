using System;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillCastPipeline : AbilityPipeline
    {
        protected override IAbilityPipelineContext CreateContext(object abilityInstance, params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is SkillCastRequest req))
            {
                throw new ArgumentException("SkillCastPipeline requires SkillCastRequest as first arg.");
            }

            var ctx = new SkillPipelineContext();
            ctx.Initialize(abilityInstance, in req);
            return ctx;
        }

        protected override void ReleaseContext(IAbilityPipelineContext context)
        {
            // no-op for now
        }

        public override void OnUpdate(IAbilityPipelineContext context, float deltaTime)
        {
            if (context is SkillPipelineContext c)
            {
                c.AdvanceTime(deltaTime);
            }

            base.OnUpdate(context, deltaTime);
        }
    }
}
