using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    [MemoryPackable]
    public partial struct ShooterStartPlayer
    {
        [MemoryPackOrder(0)] public int PlayerId;
        [MemoryPackOrder(1)] public string Name;
        [MemoryPackOrder(2)] public float SpawnX;
        [MemoryPackOrder(3)] public float SpawnY;

        public ShooterStartPlayer(int playerId, string name, float spawnX, float spawnY)
        {
            PlayerId = playerId;
            Name = name ?? string.Empty;
            SpawnX = spawnX;
            SpawnY = spawnY;
        }
    }

    [MemoryPackable]
    public partial struct ShooterStartGamePayload
    {
        [MemoryPackOrder(0)] public string MatchId;
        [MemoryPackOrder(1)] public int TickRate;
        [MemoryPackOrder(2)] public int RandomSeed;
        [MemoryPackOrder(3)] public ShooterStartPlayer[] Players;
        [MemoryPackOrder(4)] public ulong WorldId;
        [MemoryPackOrder(5)] public long StartServerTicks;
        [MemoryPackOrder(6)] public long ServerTickFrequency;
        [MemoryPackOrder(7)] public int StartFrame;
        [MemoryPackOrder(8)] public double FixedDeltaSeconds;

        public ShooterStartGamePayload(string matchId, int tickRate, int randomSeed, ShooterStartPlayer[] players)
            : this(matchId, tickRate, randomSeed, players, 0ul, 0L, 0L, 0, 0d)
        {
        }

        [MemoryPackConstructor]
        public ShooterStartGamePayload(
            string matchId,
            int tickRate,
            int randomSeed,
            ShooterStartPlayer[] players,
            ulong worldId,
            long startServerTicks,
            long serverTickFrequency,
            int startFrame,
            double fixedDeltaSeconds)
        {
            MatchId = matchId ?? string.Empty;
            TickRate = tickRate;
            RandomSeed = randomSeed;
            Players = players;
            WorldId = worldId;
            StartServerTicks = startServerTicks;
            ServerTickFrequency = serverTickFrequency;
            StartFrame = startFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }

        public readonly bool HasWorldStartAnchor => StartServerTicks > 0L && ServerTickFrequency > 0L && FixedDeltaSeconds > 0d;

        public readonly ShooterStartGamePayload WithWorldStartAnchor(
            ulong worldId,
            long startServerTicks,
            long serverTickFrequency,
            int startFrame,
            double fixedDeltaSeconds)
        {
            return new ShooterStartGamePayload(
                MatchId,
                TickRate,
                RandomSeed,
                Players ?? Array.Empty<ShooterStartPlayer>(),
                worldId,
                startServerTicks,
                serverTickFrequency,
                startFrame,
                fixedDeltaSeconds);
        }
    }

    public static class ShooterStartGameCodec
    {
        public static byte[] Serialize(in ShooterStartGamePayload payload)
        {
            return MemoryPackSerializer.Serialize(payload);
        }

        public static ShooterStartGamePayload Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return new ShooterStartGamePayload(string.Empty, 30, 0, Array.Empty<ShooterStartPlayer>());
            }

            var value = MemoryPackSerializer.Deserialize<ShooterStartGamePayload>(payload);
            return new ShooterStartGamePayload(
                value.MatchId,
                value.TickRate,
                value.RandomSeed,
                value.Players ?? Array.Empty<ShooterStartPlayer>(),
                value.WorldId,
                value.StartServerTicks,
                value.ServerTickFrequency,
                value.StartFrame,
                value.FixedDeltaSeconds);
        }
    }
}
