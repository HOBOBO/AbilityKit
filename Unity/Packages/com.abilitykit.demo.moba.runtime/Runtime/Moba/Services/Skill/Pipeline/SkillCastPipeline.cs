using System;
using AbilityKit.Core.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillCastPipeline : AbilityPipeline<SkillPipelineContext>
    {
        protected override void ReleaseContext(SkillPipelineContext context)
        {
            // no-op for now
        }
    }
}
