using System;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillCastPipeline : AbilityPipeline<SkillPipelineContext>
    {
        protected override void ReleaseContext(SkillPipelineContext context)
        {
            // 当前无需额外释放逻辑。
        }
    }
}
