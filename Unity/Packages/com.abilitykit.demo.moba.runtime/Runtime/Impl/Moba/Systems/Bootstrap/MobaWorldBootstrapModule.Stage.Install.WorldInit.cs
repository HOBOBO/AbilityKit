using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Impl.Moba.Serialization;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    using AbilityKit.Ability.Share.Impl.Moba.CreateWorld;
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void InstallWorldInitEnterGameReq(IWorldResolver services)
        {
            DemoWireSerializerBootstrap.TryInstallMemoryPack();

            if (!services.TryResolve<WorldInitData>(out var init))
            {
                Log.Info("[MobaWorldBootstrapModule] Install: WorldInitData not found; skip SetEnterGameReq");
                return;
            }

            var payloadLen = init.Payload != null ? init.Payload.Length : 0;
            Log.Info($"[MobaWorldBootstrapModule] Install: WorldInitData found. opCode={init.OpCode}, payloadLen={payloadLen}");

            if (payloadLen == 0)
            {
                Log.Info("[MobaWorldBootstrapModule] Install: WorldInitData payload is empty; skip SetEnterGameReq");
                return;
            }

            // CreateWorld stage: store game start spec for later StartGame (server adjudication)
            EnterMobaGameReq req;
            if (MobaCreateWorldInitCodec.TryDeserializeReq(init.Payload, out var initReq))
            {
                req = initReq;
            }
            else
            {
                req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            }

            // Seed deterministic world random as early as possible.
            if (services.TryResolve<IWorldRandom>(out var random) && random is RollbackWorldRandom rr)
            {
                rr.SetSeed(req.RandomSeed);
                Log.Info($"[MobaWorldBootstrapModule] Install: Seed world random success (seed={req.RandomSeed})");
            }

            var spec = new MobaGameStartSpec(in req);
            if (services.TryResolve<MobaEnterGameFlowService>(out var flow) && flow != null)
            {
                try
                {
                    var contexts = services.Resolve<global::Entitas.IContexts>();
                    var actorContext = ((global::Contexts)contexts).actor;
                    flow.ApplyGameStartSpec(actorContext, in spec);
                    Log.Info("[MobaWorldBootstrapModule] Install: ApplyGameStartSpec success");

                    if (services.TryResolve<MobaGamePhaseService>(out var phase) && phase != null)
                    {
                        phase.SetInGame();
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[MobaWorldBootstrapModule] Install: ApplyGameStartSpec failed");
                }
            }
            else
            {
                Log.Info("[MobaWorldBootstrapModule] Install: MobaEnterGameFlowService not found; cannot ApplyGameStartSpec");
            }
        }
    }
}
