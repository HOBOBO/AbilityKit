using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
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

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                Log.Info("[BattleSessionFeature] GatewayRemote transport active. Skipping AutoCreateWorld/AutoJoin (not applicable). Use GatewayAutoCreateRoom/GatewayAutoJoinRoom for room lifecycle. AutoConnect/AutoReady are supported.");
                if (_plan.AutoConnect)
                {
                    Log.Info($"[BattleSessionFeature] GatewayRemote AutoConnect -> Connect() to {_plan.GatewayHost}:{_plan.GatewayPort}");
                    _session?.Connect();
                }

                if (_plan.AutoReady)
                {
                    Log.Info($"[BattleSessionFeature] GatewayRemote AutoReady -> SubmitInput(Ready). worldId='{_plan.WorldId}' playerId={_plan.PlayerId} frame={_lastFrame + 1}");
                    var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                    _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
                }
                return;
            }

            var isLocal = _plan.SyncMode != BattleSyncMode.SnapshotAuthority && _plan.HostMode == BattleStartConfig.BattleHostMode.Local;
            if (isLocal) _session?.Connect();
            else if (_plan.AutoConnect) _session?.Connect();

            if (_plan.AutoCreateWorld) CreateWorld();
            if (_plan.AutoJoin)
            {
                _session?.Join(new JoinWorldRequest(new WorldId(_plan.WorldId), new PlayerId(_plan.PlayerId)));
            }
            if (_plan.AutoReady)
            {
                var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
            }
        }
    }
}
