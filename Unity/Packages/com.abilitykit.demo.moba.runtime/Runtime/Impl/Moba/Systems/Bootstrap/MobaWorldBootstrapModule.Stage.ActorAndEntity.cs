using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterActorAndEntityServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<ActorIdAllocator, ActorIdAllocator>();
            builder.RegisterService<MobaActorRegistry, MobaActorRegistry>();
            builder.RegisterService<ActorIdIndex, ActorIdIndex>();
            builder.RegisterService<MobaActorLookupService, MobaActorLookupService>();

            builder.RegisterService<MobaEntityManager, MobaEntityManager>();
            builder.RegisterService<MobaPlayerActorMapService, MobaPlayerActorMapService>();

            builder.RegisterService<MobaEnterGameFlowService, MobaEnterGameFlowService>();
            builder.RegisterService<IMobaGameStartOrchestrator, MobaGameStartOrchestrator>();
            builder.RegisterService<IWorldInputSink, MobaLobbyInputSink>();
            builder.RegisterService<ActorEntityInitPipeline, ActorEntityInitPipeline>();
        }
    }
}
