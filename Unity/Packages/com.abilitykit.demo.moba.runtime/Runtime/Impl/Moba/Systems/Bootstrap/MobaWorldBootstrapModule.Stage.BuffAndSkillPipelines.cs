using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterBuffAndSkillPipelines(WorldContainerBuilder builder)
        {
            builder.TryRegisterService<MobaBuffService, MobaBuffService>();
            builder.RegisterService<IMobaSkillPipelineLibrary, TableDrivenMobaSkillPipelineLibrary>();
            builder.RegisterService<SkillExecutor, SkillExecutor>();
        }
    }
}
