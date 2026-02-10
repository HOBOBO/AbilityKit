using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void InstallWorldInitEnterGameReq(IWorldResolver services)
        {
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

            // CreateWorld stage: store EnterGame request for later StartGame (server adjudication)
            var req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            if (services.TryResolve<MobaLobbyStateService>(out var lobby2) && lobby2 != null)
            {
                lobby2.SetEnterGameReq(req);
                Log.Info("[MobaWorldBootstrapModule] Install: SetEnterGameReq success");

                // Seed deterministic world random as early as possible.
                if (services.TryResolve<IWorldRandom>(out var random) && random is RollbackWorldRandom rr)
                {
                    rr.SetSeed(req.RandomSeed);
                    Log.Info($"[MobaWorldBootstrapModule] Install: Seed world random success (seed={req.RandomSeed})");
                }
            }
            else
            {
                Log.Info("[MobaWorldBootstrapModule] Install: MobaLobbyStateService not found; cannot SetEnterGameReq");
            }
        }
    }
}
