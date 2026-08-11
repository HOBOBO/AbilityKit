using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void ApplyAutoPlanActions()
        {
            if (!_autoPlanLogged)
            {
                _autoPlanLogged = true;
            }

            var world = _plan.World;
            var sync = _plan.Sync;
            var gateway = _plan.Gateway;
            var auto = _plan.Auto;

            if (_plan.HostMode == BattleHostMode.GatewayRemote && gateway.UseGatewayTransport)
            {
                Log.Info("[BattleSessionFeature] GatewayRemote transport active. Skipping AutoCreateWorld/AutoJoin/AutoReady (room lifecycle owns these actions). AutoConnect is supported.");
                if (auto.AutoConnect)
                {
                    Log.Info($"[BattleSessionFeature] GatewayRemote AutoConnect -> Connect() to {gateway.Host}:{gateway.Port}");
                    _session?.Connect();
                }

                return;
            }

            var isLocal = sync.SyncMode != BattleSyncMode.SnapshotAuthority && _plan.HostMode == BattleHostMode.Local;
            if (isLocal) _session?.Connect();
            else if (auto.AutoConnect) _session?.Connect();

            if (auto.AutoCreateWorld) CreateWorld();
            if (auto.AutoJoin)
            {
                _session?.Join(new JoinWorldRequest(new WorldId(world.WorldId), new PlayerId(world.PlayerId)));
            }
        }
    }
}

