using System;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillCastPipeline : AbilityPipeline<SkillPipelineContext>
    {
        protected override void ReleaseContext(SkillPipelineContext context)
        {
            // no-op for now
        }
    }
}
