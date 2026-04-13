using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterBuffAndSkillPipelines(WorldContainerBuilder builder)
        {
            builder.TryRegister<MobaSkillCastInstanceSyncSettings>(WorldLifetime.Singleton, _ => new MobaSkillCastInstanceSyncSettings());
            builder.TryRegisterService<MobaBuffService, MobaBuffService>();
            builder.RegisterService<IMobaSkillPipelineLibrary, TableDrivenMobaSkillPipelineLibrary>();
            builder.RegisterService<SkillExecutor, SkillExecutor>();
        }
    }
}
