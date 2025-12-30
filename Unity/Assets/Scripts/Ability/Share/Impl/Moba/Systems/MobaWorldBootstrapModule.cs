using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.RegisterType<MobaLobbyStateService, MobaLobbyStateService>(WorldLifetime.Scoped);

            builder.RegisterType<ActorIdAllocator, ActorIdAllocator>(WorldLifetime.Scoped);
            builder.RegisterType<MobaActorRegistry, MobaActorRegistry>(WorldLifetime.Scoped);
            builder.RegisterType<ActorIdIndex, ActorIdIndex>(WorldLifetime.Scoped);
            builder.RegisterType<MobaActorLookupService, MobaActorLookupService>(WorldLifetime.Scoped);

            builder.RegisterType<MobaPlayerActorMapService, MobaPlayerActorMapService>(WorldLifetime.Scoped);
            builder.RegisterType<MobaActorTransformSnapshotService, MobaActorTransformSnapshotService>(WorldLifetime.Scoped);
            builder.RegisterType<MobaStateHashSnapshotService, MobaStateHashSnapshotService>(WorldLifetime.Scoped);

            builder.RegisterType<MobaEnterGameSnapshotService, MobaEnterGameSnapshotService>(WorldLifetime.Scoped);

            builder.RegisterType<MobaLobbySnapshotService, MobaLobbySnapshotService>(WorldLifetime.Scoped);
            builder.RegisterType<MobaSnapshotRouter, MobaSnapshotRouter>(WorldLifetime.Scoped);
            builder.Register<IWorldStateSnapshotProvider>(WorldLifetime.Scoped, r => r.Resolve<MobaSnapshotRouter>());

            builder.RegisterType<MobaEnterGameFlowService, MobaEnterGameFlowService>(WorldLifetime.Scoped);
            builder.RegisterType<IWorldInputSink, MobaLobbyInputSink>(WorldLifetime.Scoped);

            builder.RegisterType<MobaMoveService, MobaMoveService>(WorldLifetime.Scoped);
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (services.TryGet<MobaLobbyStateService>(out var lobby) && lobby != null
                && services.TryGet<MobaMoveService>(out var moves) && moves != null
                && services.TryGet<IWorldClock>(out var clock) && clock != null)
            {
                systems.Add(new MobaMoveSystem(contexts, lobby, moves, clock));
            }

            if (!services.TryGet<WorldInitData>(out var init) || init.Payload == null || init.Payload.Length == 0)
            {
                return;
            }

            // CreateWorld stage: store EnterGame request for later StartGame (server adjudication)
            var req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            if (services.TryGet<MobaLobbyStateService>(out var lobby2) && lobby2 != null)
            {
                lobby2.SetEnterGameReq(req);
            }
        }
    }
}
