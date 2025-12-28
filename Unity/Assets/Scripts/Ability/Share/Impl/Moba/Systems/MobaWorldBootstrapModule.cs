using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.RegisterType<MobaEnterGameSnapshotService, MobaEnterGameSnapshotService>(WorldLifetime.Scoped);
            builder.Register<IWorldStateSnapshotProvider>(WorldLifetime.Scoped, r => r.Resolve<MobaEnterGameSnapshotService>());
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (!services.TryGet<WorldInitData>(out var init) || init.Payload == null || init.Payload.Length == 0)
            {
                return;
            }

            var req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            var built = EntityBuilder.BuildEnterGameActors(contexts.actor, req);

            var payload = new byte[12];
            var p = built.LocalActorTransform.Position;
            Buffer.BlockCopy(BitConverter.GetBytes(p.X), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(p.Y), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(p.Z), 0, payload, 8, 4);

            var res = new EnterMobaGameRes(
                worldId: services.Resolve<IWorldContext>().Id,
                playerId: req.PlayerId,
                localActorId: built.LocalActorId,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                players: built.Players,
                opCode: 0,
                payload: payload
            );

            if (services.TryGet<MobaEnterGameSnapshotService>(out var snap) && snap != null)
            {
                snap.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(res));
            }
        }
    }
}
