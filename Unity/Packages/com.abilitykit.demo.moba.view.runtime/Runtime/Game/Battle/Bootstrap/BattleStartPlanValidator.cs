using System;

namespace AbilityKit.Game.Flow
{
    public static class BattleStartPlanValidator
    {
        public static void Validate(in BattleStartPlanOptions options)
        {
            ValidateWorld(in options.World);
            ValidateGateway(options.HostMode, in options.Gateway);
            if (options.Auto.AutoCreateWorld)
            {
                ValidateCreateWorld(in options.CreateWorld);
            }
            ValidateTimeSync(in options.TimeSync);
            ValidateRunMode(in options.RunMode);
        }

        private static void ValidateWorld(in BattleStartPlanWorldOptions world)
        {
            if (string.IsNullOrEmpty(world.WorldId)) throw new InvalidOperationException("Battle start WorldId is required.");
            if (string.IsNullOrEmpty(world.WorldType)) throw new InvalidOperationException("Battle start WorldType is required.");
            if (string.IsNullOrEmpty(world.ClientId)) throw new InvalidOperationException("Battle start ClientId is required.");
            if (string.IsNullOrEmpty(world.PlayerId)) throw new InvalidOperationException("Battle start PlayerId is required.");
            if (world.TickRate <= 0) throw new InvalidOperationException("Battle start TickRate must be greater than 0.");
            if (world.InputDelayFrames < 0) throw new InvalidOperationException("Battle start InputDelayFrames cannot be negative.");
        }

        private static void ValidateGateway(BattleHostMode hostMode, in BattleStartPlanGatewayOptions gateway)
        {
            if (hostMode != BattleHostMode.GatewayRemote && !gateway.UseGatewayTransport) return;

            if (string.IsNullOrEmpty(gateway.Host)) throw new InvalidOperationException("Gateway Host is required when gateway transport is enabled.");
            if (gateway.Port <= 0) throw new InvalidOperationException("Gateway Port must be greater than 0 when gateway transport is enabled.");
            if (gateway.AutoJoinRoom && !gateway.AutoCreateRoom && gateway.NumericRoomId == 0 && string.IsNullOrEmpty(gateway.JoinRoomId))
            {
                throw new InvalidOperationException("Gateway auto join requires NumericRoomId or JoinRoomId when AutoCreateRoom is disabled.");
            }
        }

        private static void ValidateCreateWorld(in BattleStartPlanCreateWorldOptions createWorld)
        {
            if (createWorld.OpCode <= 0) throw new InvalidOperationException("CreateWorld OpCode must be greater than 0.");
            if (createWorld.Payload == null || createWorld.Payload.Length == 0) throw new InvalidOperationException("CreateWorld Payload is required.");
        }

        private static void ValidateTimeSync(in BattleStartPlanTimeSyncOptions timeSync)
        {
            if (timeSync.OpCode == 0) throw new InvalidOperationException("TimeSync OpCode must be greater than 0.");
            if (timeSync.IntervalMs <= 0) throw new InvalidOperationException("TimeSync IntervalMs must be greater than 0.");
            if (timeSync.TimeoutMs <= 0) throw new InvalidOperationException("TimeSync TimeoutMs must be greater than 0.");
            if (timeSync.Alpha <= 0 || timeSync.Alpha > 1) throw new InvalidOperationException("TimeSync Alpha must be in range (0, 1].");
            if (timeSync.IdealFrameSafetyRttFactor < 0) throw new InvalidOperationException("IdealFrameSafetyRttFactor cannot be negative.");
            if (timeSync.IdealFrameSafetyMinMarginFrames < 0) throw new InvalidOperationException("IdealFrameSafetyMinMarginFrames cannot be negative.");
            if (timeSync.IdealFrameSafetyMaxMarginFrames < timeSync.IdealFrameSafetyMinMarginFrames)
            {
                throw new InvalidOperationException("IdealFrameSafetyMaxMarginFrames must be greater than or equal to IdealFrameSafetyMinMarginFrames.");
            }
        }

        private static void ValidateRunMode(in BattleStartPlanRunModeOptions runMode)
        {
            if (runMode.RunMode == BattleRunMode.Record && string.IsNullOrEmpty(runMode.InputRecordOutputPath))
            {
                throw new InvalidOperationException("InputRecordOutputPath is required when run mode is Record.");
            }

            if (runMode.RunMode == BattleRunMode.Replay && string.IsNullOrEmpty(runMode.InputReplayPath))
            {
                throw new InvalidOperationException("InputReplayPath is required when run mode is Replay.");
            }
        }
    }
}
