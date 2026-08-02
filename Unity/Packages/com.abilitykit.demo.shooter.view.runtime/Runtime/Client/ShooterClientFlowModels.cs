using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public static class ShooterRoomLaunchTagKeys
    {
        public const string MinPlayers = "minPlayers";
        public const string SyncTemplateId = "syncTemplateId";
        public const string SyncModel = "syncModel";
        public const string NetworkEnvironmentId = "networkEnvironmentId";
        public const string CarrierName = "carrierName";
        public const string EnableAuthoritativeWorld = "enableAuthoritativeWorld";
        public const string InterpolationEnabled = "interpolationEnabled";
        public const string InputDelayFrames = "inputDelayFrames";
        public const string RandomSeed = "randomSeed";
        public const string DurationFrames = "durationFrames";
        public const string VictoryTargetDefeats = "victoryTargetDefeats";
        public const string ContinueAfterAllPlayersDefeated = "continueAfterAllPlayersDefeated";
    }

    public readonly struct ShooterRoomLaunchSpec
    {
        public const int DefaultOfflineTimeoutSeconds = 30 * 60;
        public const string DefaultSyncTemplateId = ShooterSyncTemplateIds.PredictRollbackAuthority;
        public const int DefaultSyncModel = (int)NetworkSyncModel.PredictRollback;
        public const string DefaultNetworkEnvironmentId = "ideal";
        public const string DefaultCarrierName = "server";

        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomTitle;
        public readonly int MaxPlayers;
        public readonly int GameplayId;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly IReadOnlyDictionary<string, string> Tags;
        public readonly string SyncTemplateId;
        public readonly int SyncModel;
        public readonly string NetworkEnvironmentId;
        public readonly string CarrierName;
        public readonly bool EnableAuthoritativeWorld;
        public readonly bool InterpolationEnabled;
        public readonly int InputDelayFrames;

        public ShooterRoomLaunchSpec(
            string region,
            string serverId,
            string roomTitle,
            int maxPlayers,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            IReadOnlyDictionary<string, string> tags,
            string syncTemplateId = "",
            int syncModel = 0,
            string networkEnvironmentId = "",
            string carrierName = "",
            bool enableAuthoritativeWorld = true,
            bool interpolationEnabled = false,
            int inputDelayFrames = 0)
        {
            Region = string.IsNullOrWhiteSpace(region) ? "local" : region;
            ServerId = string.IsNullOrWhiteSpace(serverId) ? "dev" : serverId;
            RoomTitle = string.IsNullOrWhiteSpace(roomTitle) ? "Shooter Room" : roomTitle;
            MaxPlayers = maxPlayers <= 0 ? ShooterGameplay.DefaultMaxPlayers : maxPlayers;
            GameplayId = gameplayId <= 0 ? ShooterGameplay.GameplayId : gameplayId;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            WorldType = string.IsNullOrWhiteSpace(worldType) ? ShooterGameplay.WorldType : worldType;
            ClientId = clientId ?? string.Empty;
            Tags = tags ?? EmptyTags.Value;
            SyncTemplateId = syncTemplateId ?? string.Empty;
            SyncModel = syncModel;
            NetworkEnvironmentId = networkEnvironmentId ?? string.Empty;
            CarrierName = carrierName ?? string.Empty;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            InterpolationEnabled = interpolationEnabled;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }

        public static ShooterRoomLaunchSpec CreateDefault(string clientId)
        {
            return new ShooterRoomLaunchSpec(
                "local",
                "dev",
                "Shooter Room",
                ShooterGameplay.DefaultMaxPlayers,
                ShooterGameplay.GameplayId,
                ruleSetId: 1,
                configVersion: 1,
                protocolVersion: 1,
                ShooterGameplay.WorldType,
                clientId,
                CreateDefaultTags(),
                syncTemplateId: DefaultSyncTemplateId,
                syncModel: DefaultSyncModel,
                networkEnvironmentId: DefaultNetworkEnvironmentId,
                carrierName: DefaultCarrierName,
                enableAuthoritativeWorld: true,
                interpolationEnabled: false,
                inputDelayFrames: 0);
        }

        public static Dictionary<string, string> CreateDefaultTags()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RoomTagKeys.Gameplay] = ShooterGameplay.RoomType,
                [RoomTagKeys.WorldType] = ShooterGameplay.WorldType,
                [ShooterRoomLaunchTagKeys.MinPlayers] = "1",
                [RoomTagKeys.TickRate] = ShooterGameplay.DefaultTickRate.ToString(),
                [RoomTagKeys.OfflineTimeoutSeconds] = DefaultOfflineTimeoutSeconds.ToString()
            };
        }

        private static class EmptyTags
        {
            internal static readonly IReadOnlyDictionary<string, string> Value = new Dictionary<string, string>(0);
        }
    }

    public readonly struct ShooterInputPacket
    {
        public readonly int OpCode;
        public readonly byte[] Payload;
        public readonly ShooterPlayerCommand Command;

        public ShooterInputPacket(int opCode, byte[] payload, in ShooterPlayerCommand command)
        {
            OpCode = opCode;
            Payload = payload ?? Array.Empty<byte>();
            Command = command;
        }
    }

    public static class ShooterClientInputBuilder
    {
        public static ShooterInputPacket CreatePacket(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return CreatePacket(playerId, moveX, moveY, aimX, aimY, fire, ShooterPlayerAttackSlots.Primary);
        }

        public static ShooterInputPacket CreatePacket(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire, int attackSlot)
        {
            var command = CreateCommand(playerId, moveX, moveY, aimX, aimY, fire, attackSlot);
            var payload = ShooterInputCodec.Serialize(new[] { command });
            return new ShooterInputPacket(ShooterOpCodes.Input.PlayerCommand, payload, in command);
        }

        public static ShooterPlayerCommand CreateCommand(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return CreateCommand(playerId, moveX, moveY, aimX, aimY, fire, ShooterPlayerAttackSlots.Primary);
        }

        public static ShooterPlayerCommand CreateCommand(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire, int attackSlot)
        {
            Normalize(ref moveX, ref moveY);
            Normalize(ref aimX, ref aimY);
            return new ShooterPlayerCommand(playerId, moveX, moveY, aimX, aimY, fire, attackSlot);
        }

        private static void Normalize(ref float x, ref float y)
        {
            var lengthSquared = x * x + y * y;
            if (lengthSquared <= 0.0001f)
            {
                return;
            }

            if (lengthSquared <= 1f)
            {
                return;
            }

            var invLength = 1f / (float)Math.Sqrt(lengthSquared);
            x *= invLength;
            y *= invLength;
        }
    }
}
