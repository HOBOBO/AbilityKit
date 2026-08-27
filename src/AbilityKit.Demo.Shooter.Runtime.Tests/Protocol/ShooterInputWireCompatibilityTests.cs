using AbilityKit.Protocol.Shooter;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Protocol;

public sealed class ShooterInputWireCompatibilityTests
{
    [Fact]
    public void GeneratedSequentialInput_IsByteCompatibleWithLegacyHandWrittenDto()
    {
        var legacy = new LegacyShooterInputPayload
        {
            Commands = new[]
            {
                new LegacyShooterPlayerCommand
                {
                    PlayerId = 7,
                    MoveX = 0.25f,
                    MoveY = -0.5f,
                    AimX = 1f,
                    AimY = 0f,
                    Fire = true,
                    AttackSlot = ShooterPlayerAttackSlots.Spread
                },
                new LegacyShooterPlayerCommand
                {
                    PlayerId = 9,
                    MoveX = -1f,
                    MoveY = 0.75f,
                    AimX = 0f,
                    AimY = -1f,
                    Fire = false,
                    AttackSlot = ShooterPlayerAttackSlots.Twin
                }
            }
        };
        var generated = new ShooterInputPayload(new[]
        {
            new ShooterPlayerCommand(7, 0.25f, -0.5f, 1f, 0f, true, ShooterPlayerAttackSlots.Spread),
            new ShooterPlayerCommand(9, -1f, 0.75f, 0f, -1f, false, ShooterPlayerAttackSlots.Twin)
        });

        var legacyBytes = MemoryPackSerializer.Serialize(legacy);
        var generatedBytes = MemoryPackSerializer.Serialize(generated);

        Assert.Equal(legacyBytes, generatedBytes);
        Assert.Equal(
            "AQIAAAAHAAAAAACAPgAAAL8AAIA/AAAAAAEAAAABAAAACQAAAAAAgL8AAEA/AAAAAAAAgL8AAAAAAgAAAA==",
            Convert.ToBase64String(generatedBytes));

        var generatedFromLegacy = MemoryPackSerializer.Deserialize<ShooterInputPayload>(legacyBytes);
        var legacyFromGenerated = MemoryPackSerializer.Deserialize<LegacyShooterInputPayload>(generatedBytes);
        Assert.Equal(2, generatedFromLegacy.Commands.Length);
        Assert.Equal(ShooterPlayerAttackSlots.Twin, generatedFromLegacy.Commands[1].AttackSlot);
        Assert.Equal(2, legacyFromGenerated.Commands.Length);
        Assert.Equal(-0.5f, legacyFromGenerated.Commands[0].MoveY);
    }

    [Fact]
    public void GeneratedSequentialStartGame_IsByteCompatibleWithLegacyHandWrittenDto()
    {
        var legacy = new LegacyShooterStartGamePayload
        {
            MatchId = "match-2026-08-24",
            TickRate = 60,
            RandomSeed = -123456789,
            Players = new[]
            {
                new LegacyShooterStartPlayer
                {
                    PlayerId = 7,
                    Name = "Alpha",
                    SpawnX = 1.25f,
                    SpawnY = -2.5f
                },
                new LegacyShooterStartPlayer
                {
                    PlayerId = 9,
                    Name = "Bravo",
                    SpawnX = -8f,
                    SpawnY = 16.5f
                }
            },
            WorldId = 0x0123_4567_89AB_CDEFul,
            StartServerTicks = 638_600_000_000_000_000L,
            ServerTickFrequency = 10_000_000L,
            StartFrame = 42,
            FixedDeltaSeconds = 1d / 60d
        };
        var generated = new ShooterStartGamePayload(
            legacy.MatchId,
            legacy.TickRate,
            legacy.RandomSeed,
            new[]
            {
                new ShooterStartPlayer(7, "Alpha", 1.25f, -2.5f),
                new ShooterStartPlayer(9, "Bravo", -8f, 16.5f)
            },
            legacy.WorldId,
            legacy.StartServerTicks,
            legacy.ServerTickFrequency,
            legacy.StartFrame,
            legacy.FixedDeltaSeconds);

        var legacyBytes = MemoryPackSerializer.Serialize(legacy);
        var generatedBytes = MemoryPackSerializer.Serialize(generated);

        Assert.Equal(legacyBytes, generatedBytes);
        Assert.Equal(
            "Cu////8QAAAAbWF0Y2gtMjAyNi0wOC0yNDwAAADrMqT4AgAAAAQHAAAA+v///wUAAABBbHBoYQAAoD8AACDABAkAAAD6////BQAAAEJyYXZvAAAAwQAAhEHvzauJZ0UjAQCAkAlRw9wIgJaYAAAAAAAqAAAAERERERERkT8B",
            Convert.ToBase64String(generatedBytes));

        var generatedFromLegacy = MemoryPackSerializer.Deserialize<ShooterStartGamePayload>(legacyBytes);
        var legacyFromGenerated = MemoryPackSerializer.Deserialize<LegacyShooterStartGamePayload>(generatedBytes);
        Assert.NotNull(generatedFromLegacy.Players);
        Assert.NotNull(legacyFromGenerated.Players);
        Assert.Equal("match-2026-08-24", generatedFromLegacy.MatchId);
        Assert.Equal("Bravo", generatedFromLegacy.Players[1].Name);
        Assert.Equal(0x0123_4567_89AB_CDEFul, generatedFromLegacy.WorldId);
        Assert.Equal(42, legacyFromGenerated.StartFrame);
        Assert.Equal(-2.5f, legacyFromGenerated.Players[0].SpawnY);
        Assert.Equal(1d / 60d, legacyFromGenerated.FixedDeltaSeconds);
    }

    [Fact]
    public void GeneratedSequentialEventArray_IsByteCompatibleWithLegacyHandWrittenDto()
    {
        var legacy = new[]
        {
            new LegacyShooterEventSnapshot
            {
                EventType = (int)ShooterEventType.Fire,
                SourcePlayerId = 7,
                TargetPlayerId = 0,
                BulletId = 101,
                X = 12.5f,
                Y = -3.25f,
                Value = 2
            },
            new LegacyShooterEventSnapshot
            {
                EventType = (int)ShooterEventType.Hit,
                SourcePlayerId = 7,
                TargetPlayerId = 9,
                BulletId = 101,
                X = -0.5f,
                Y = 4.75f,
                Value = 35
            }
        };
        var generated = new[]
        {
            new ShooterEventSnapshot(ShooterEventType.Fire, 7, 0, 101, 12.5f, -3.25f, 2),
            new ShooterEventSnapshot(ShooterEventType.Hit, 7, 9, 101, -0.5f, 4.75f, 35)
        };

        var legacyBytes = MemoryPackSerializer.Serialize(legacy);
        var generatedBytes = MemoryPackSerializer.Serialize(generated);

        Assert.Equal(legacyBytes, generatedBytes);
        Assert.Equal(
            "AgAAAAIAAAAHAAAAAAAAAGUAAAAAAEhBAABQwAIAAAABAAAABwAAAAkAAABlAAAAAAAAvwAAmEAjAAAA",
            Convert.ToBase64String(generatedBytes));

        var generatedFromLegacy = MemoryPackSerializer.Deserialize<ShooterEventSnapshot[]>(legacyBytes);
        var legacyFromGenerated = MemoryPackSerializer.Deserialize<LegacyShooterEventSnapshot[]>(generatedBytes);
        Assert.NotNull(generatedFromLegacy);
        Assert.NotNull(legacyFromGenerated);
        Assert.Equal(2, generatedFromLegacy.Length);
        Assert.Equal(ShooterEventType.Hit, (ShooterEventType)generatedFromLegacy[1].EventType);
        Assert.Equal(35, generatedFromLegacy[1].Value);
        Assert.Equal(2, legacyFromGenerated.Length);
        Assert.Equal(101, legacyFromGenerated[0].BulletId);
        Assert.Equal(4.75f, legacyFromGenerated[1].Y);
    }

    [Fact]
    public void GeneratedSequentialStateSnapshot_IsByteCompatibleWithLegacyHandWrittenDto()
    {
        var legacy = new LegacyShooterStateSnapshotPayload
        {
            Frame = 12345,
            Players = new[]
            {
                new LegacyShooterPlayerSnapshot
                {
                    PlayerId = 7,
                    X = 1.25f,
                    Y = -2.5f,
                    AimX = 0.6f,
                    AimY = 0.8f,
                    Hp = 85,
                    Score = 3,
                    Alive = true
                },
                new LegacyShooterPlayerSnapshot
                {
                    PlayerId = 9,
                    X = -12f,
                    Y = 7.75f,
                    AimX = -1f,
                    AimY = 0f,
                    Hp = 0,
                    Score = 11,
                    Alive = false
                }
            },
            Bullets = new[]
            {
                new LegacyShooterBulletSnapshot
                {
                    BulletId = 101,
                    OwnerPlayerId = 7,
                    X = 12.5f,
                    Y = -3.25f,
                    VelocityX = 20f,
                    VelocityY = -4f,
                    RemainingFrames = 18
                },
                new LegacyShooterBulletSnapshot
                {
                    BulletId = 202,
                    OwnerPlayerId = 9,
                    X = -8.5f,
                    Y = 6f,
                    VelocityX = -10f,
                    VelocityY = 2.5f,
                    RemainingFrames = 7
                }
            },
            Events = new[]
            {
                new LegacyShooterEventSnapshot
                {
                    EventType = (int)ShooterEventType.Hit,
                    SourcePlayerId = 7,
                    TargetPlayerId = 9,
                    BulletId = 101,
                    X = -0.5f,
                    Y = 4.75f,
                    Value = 35
                }
            },
            MatchState = 2,
            TimeLimitFrames = 108000,
            RemainingTimeFrames = 54321,
            Enemies = new[]
            {
                new LegacyShooterEnemySnapshot
                {
                    EnemyId = 301,
                    X = 30f,
                    Y = -15f,
                    FacingX = -0.8f,
                    FacingY = 0.6f,
                    Hp = 120,
                    MaxHp = 150,
                    Alive = true
                },
                new LegacyShooterEnemySnapshot
                {
                    EnemyId = 302,
                    X = -22f,
                    Y = 11f,
                    FacingX = 1f,
                    FacingY = 0f,
                    Hp = 0,
                    MaxHp = 80,
                    Alive = false
                }
            }
        };
        var generated = new ShooterStateSnapshotPayload(
            12345,
            new[]
            {
                new ShooterPlayerSnapshot(7, 1.25f, -2.5f, 0.6f, 0.8f, 85, 3, true),
                new ShooterPlayerSnapshot(9, -12f, 7.75f, -1f, 0f, 0, 11, false)
            },
            new[]
            {
                new ShooterBulletSnapshot(101, 7, 12.5f, -3.25f, 20f, -4f, 18),
                new ShooterBulletSnapshot(202, 9, -8.5f, 6f, -10f, 2.5f, 7)
            },
            new[]
            {
                new ShooterEventSnapshot(ShooterEventType.Hit, 7, 9, 101, -0.5f, 4.75f, 35)
            },
            2,
            108000,
            54321,
            new[]
            {
                new ShooterEnemySnapshot(301, 30f, -15f, -0.8f, 0.6f, 120, 150, true),
                new ShooterEnemySnapshot(302, -22f, 11f, 1f, 0f, 0, 80, false)
            });

        var legacyBytes = MemoryPackSerializer.Serialize(legacy);
        var generatedBytes = MemoryPackSerializer.Serialize(generated);

        Assert.Equal(legacyBytes, generatedBytes);
        Assert.Equal(
            "CDkwAAACAAAABwAAAAAAoD8AACDAmpkZP83MTD9VAAAAAwAAAAEAAAAJAAAAAABAwQAA+EAAAIC/AAAAAAAAAAALAAAAAAAAAAIAAABlAAAABwAAAAAASEEAAFDAAACgQQAAgMASAAAAygAAAAkAAAAAAAjBAADAQAAAIMEAACBABwAAAAEAAAABAAAABwAAAAkAAABlAAAAAAAAvwAAmEAjAAAAAgAAAOClAQAx1AAAAgAAAC0BAAAAAPBBAABwwc3MTL+amRk/eAAAAJYAAAABAAAALgEAAAAAsMEAADBBAACAPwAAAAAAAAAAUAAAAAAAAAA=",
            Convert.ToBase64String(generatedBytes));

        var generatedFromLegacy = MemoryPackSerializer.Deserialize<ShooterStateSnapshotPayload>(legacyBytes);
        var legacyFromGenerated = MemoryPackSerializer.Deserialize<LegacyShooterStateSnapshotPayload>(generatedBytes);
        Assert.NotNull(generatedFromLegacy.Players);
        Assert.NotNull(generatedFromLegacy.Bullets);
        Assert.NotNull(generatedFromLegacy.Events);
        Assert.NotNull(generatedFromLegacy.Enemies);
        Assert.NotNull(legacyFromGenerated.Players);
        Assert.NotNull(legacyFromGenerated.Bullets);
        Assert.NotNull(legacyFromGenerated.Events);
        Assert.NotNull(legacyFromGenerated.Enemies);
        Assert.Equal(12345, generatedFromLegacy.Frame);
        Assert.Equal(-12f, generatedFromLegacy.Players[1].X);
        Assert.Equal(202, generatedFromLegacy.Bullets[1].BulletId);
        Assert.Equal(35, generatedFromLegacy.Events[0].Value);
        Assert.Equal(302, generatedFromLegacy.Enemies[1].EnemyId);
        Assert.Equal(54321, legacyFromGenerated.RemainingTimeFrames);
        Assert.Equal(150, legacyFromGenerated.Enemies[0].MaxHp);
    }

    [Fact]
    public void StateHashPrimitive_HasStableMemoryPackGolden()
    {
        const ulong stateHash = 0x0123_4567_89AB_CDEFul;

        var bytes = MemoryPackSerializer.Serialize(stateHash);

        Assert.Equal("782riWdFIwE=", Convert.ToBase64String(bytes));
        Assert.Equal(stateHash, MemoryPackSerializer.Deserialize<ulong>(bytes));
    }
}

[MemoryPackable]
internal partial struct LegacyShooterInputPayload
{
    [MemoryPackOrder(0)]
    public LegacyShooterPlayerCommand[] Commands;
}

[MemoryPackable]
internal partial struct LegacyShooterPlayerCommand
{
    [MemoryPackOrder(0)] public int PlayerId;
    [MemoryPackOrder(1)] public float MoveX;
    [MemoryPackOrder(2)] public float MoveY;
    [MemoryPackOrder(3)] public float AimX;
    [MemoryPackOrder(4)] public float AimY;
    [MemoryPackOrder(5)] public bool Fire;
    [MemoryPackOrder(6)] public int AttackSlot;
}

[MemoryPackable]
internal partial struct LegacyShooterStartPlayer
{
    [MemoryPackOrder(0)] public int PlayerId;
    [MemoryPackOrder(1)] public string Name;
    [MemoryPackOrder(2)] public float SpawnX;
    [MemoryPackOrder(3)] public float SpawnY;
}

[MemoryPackable]
internal partial struct LegacyShooterStartGamePayload
{
    [MemoryPackOrder(0)] public string MatchId;
    [MemoryPackOrder(1)] public int TickRate;
    [MemoryPackOrder(2)] public int RandomSeed;
    [MemoryPackOrder(3)] public LegacyShooterStartPlayer[] Players;
    [MemoryPackOrder(4)] public ulong WorldId;
    [MemoryPackOrder(5)] public long StartServerTicks;
    [MemoryPackOrder(6)] public long ServerTickFrequency;
    [MemoryPackOrder(7)] public int StartFrame;
    [MemoryPackOrder(8)] public double FixedDeltaSeconds;

    [MemoryPackConstructor]
    public LegacyShooterStartGamePayload(
        string matchId,
        int tickRate,
        int randomSeed,
        LegacyShooterStartPlayer[] players,
        ulong worldId,
        long startServerTicks,
        long serverTickFrequency,
        int startFrame,
        double fixedDeltaSeconds)
    {
        MatchId = matchId;
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
}

[MemoryPackable]
internal partial struct LegacyShooterEventSnapshot
{
    [MemoryPackOrder(0)] public int EventType;
    [MemoryPackOrder(1)] public int SourcePlayerId;
    [MemoryPackOrder(2)] public int TargetPlayerId;
    [MemoryPackOrder(3)] public int BulletId;
    [MemoryPackOrder(4)] public float X;
    [MemoryPackOrder(5)] public float Y;
    [MemoryPackOrder(6)] public int Value;
}

[MemoryPackable]
internal partial struct LegacyShooterPlayerSnapshot
{
    [MemoryPackOrder(0)] public int PlayerId;
    [MemoryPackOrder(1)] public float X;
    [MemoryPackOrder(2)] public float Y;
    [MemoryPackOrder(3)] public float AimX;
    [MemoryPackOrder(4)] public float AimY;
    [MemoryPackOrder(5)] public int Hp;
    [MemoryPackOrder(6)] public int Score;
    [MemoryPackOrder(7)] public bool Alive;
}

[MemoryPackable]
internal partial struct LegacyShooterBulletSnapshot
{
    [MemoryPackOrder(0)] public int BulletId;
    [MemoryPackOrder(1)] public int OwnerPlayerId;
    [MemoryPackOrder(2)] public float X;
    [MemoryPackOrder(3)] public float Y;
    [MemoryPackOrder(4)] public float VelocityX;
    [MemoryPackOrder(5)] public float VelocityY;
    [MemoryPackOrder(6)] public int RemainingFrames;
}

[MemoryPackable]
internal partial struct LegacyShooterEnemySnapshot
{
    [MemoryPackOrder(0)] public int EnemyId;
    [MemoryPackOrder(1)] public float X;
    [MemoryPackOrder(2)] public float Y;
    [MemoryPackOrder(3)] public float FacingX;
    [MemoryPackOrder(4)] public float FacingY;
    [MemoryPackOrder(5)] public int Hp;
    [MemoryPackOrder(6)] public int MaxHp;
    [MemoryPackOrder(7)] public bool Alive;
}

[MemoryPackable]
internal partial struct LegacyShooterStateSnapshotPayload
{
    [MemoryPackOrder(0)] public int Frame;
    [MemoryPackOrder(1)] public LegacyShooterPlayerSnapshot[] Players;
    [MemoryPackOrder(2)] public LegacyShooterBulletSnapshot[] Bullets;
    [MemoryPackOrder(3)] public LegacyShooterEventSnapshot[] Events;
    [MemoryPackOrder(4)] public int MatchState;
    [MemoryPackOrder(5)] public int TimeLimitFrames;
    [MemoryPackOrder(6)] public int RemainingTimeFrames;
    [MemoryPackOrder(7)] public LegacyShooterEnemySnapshot[] Enemies;
}
