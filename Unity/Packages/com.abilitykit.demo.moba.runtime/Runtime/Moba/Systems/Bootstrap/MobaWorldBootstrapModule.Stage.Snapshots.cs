using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;

using HostWorldStateSnapshotProvider = AbilityKit.Ability.Host.IWorldStateSnapshotProvider;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterSnapshotServices(WorldContainerBuilder builder)
        {
            builder.RegisterService<MobaActorTransformSnapshotService, MobaActorTransformSnapshotService>();
            builder.RegisterService<MobaActorSpawnSnapshotService, MobaActorSpawnSnapshotService>();
            builder.RegisterService<MobaActorDespawnSnapshotService, MobaActorDespawnSnapshotService>();
            builder.RegisterService<MobaStateHashSnapshotService, MobaStateHashSnapshotService>();

            builder.RegisterService<MobaProjectileEventSnapshotService, MobaProjectileEventSnapshotService>();
            builder.RegisterService<MobaAreaEventSnapshotService, MobaAreaEventSnapshotService>();
            builder.RegisterService<MobaDamageEventSnapshotService, MobaDamageEventSnapshotService>();
            builder.RegisterService<MobaEnterGameSnapshotService, MobaEnterGameSnapshotService>();
            builder.RegisterService<MobaSnapshotRouter, MobaSnapshotRouter>();
            builder.RegisterServiceAlias<HostWorldStateSnapshotProvider, MobaSnapshotRouter>();
        }
    }
}
