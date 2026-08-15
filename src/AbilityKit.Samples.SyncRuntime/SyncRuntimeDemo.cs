using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Recording.FrameRecord;
using StateSnapshot = AbilityKit.Ability.StateSync.Snapshot.WorldStateSnapshot;

namespace AbilityKit.Samples.SyncRuntime;

/// <summary>输入 opCode：1=右移一帧，2=受击扣血。</summary>
public static class SyncInput
{
    public const int Move = 1;

    public const int Hit = 2;
}

/// <summary>
/// 最小确定性逻辑实体：位置 + 血量，按输入推进，无任何随机源。
/// 同时实现 <see cref="IRollbackable"/>（可被 StateManager 快照/恢复）
/// 与 <see cref="IHashableState"/>（自报哈希输入）。
/// </summary>
public sealed class SyncHero : IRollbackable, IHashableState
{
    private const float MoveSpeed = 2f;
    private const float HitDamage = 10f;

    public long EntityId { get; }

    public int SnapshotKey => EntityId.GetHashCode();

    public Vec3 Position { get; private set; }

    public float Health { get; private set; }

    public SyncHero(long entityId, float startX, float startHealth)
    {
        EntityId = entityId;
        Position = new Vec3(startX, 0f, 0f);
        Health = startHealth;
    }

    public void ApplyInputs(IReadOnlyList<PlayerInputCommand> inputs, float deltaTime)
    {
        if (inputs == null)
        {
            return;
        }

        for (int i = 0; i < inputs.Count; i++)
        {
            switch (inputs[i].OpCode)
            {
                case SyncInput.Move:
                    Position = new Vec3(Position.X + MoveSpeed * deltaTime, 0f, 0f);
                    break;
                case SyncInput.Hit:
                    Health -= HitDamage;
                    break;
            }
        }
    }

    public IRollbackState CreateRollbackState()
    {
        return new EntityRollbackState(EntityId)
        {
            position = Position,
            healthPercent = (byte)Math.Clamp(Health, 0, 255),
        };
    }

    public void RestoreFromRollbackState(IRollbackState state)
    {
        if (state is not EntityRollbackState entityState)
        {
            return;
        }

        Position = entityState.position;
        Health = entityState.healthPercent;
    }

    public ulong ComputeHash()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (ulong)EntityId) * 1099511628211UL;
            hash = (hash ^ (ulong)(int)(Position.X * 1000f)) * 1099511628211UL;
            hash = (hash ^ (ulong)(int)(Health * 100f)) * 1099511628211UL;
            return hash;
        }
    }
}

/// <summary>聚合一个或多个 <see cref="IHashableState"/> 成单条业务哈希，喂给 StateHashComputer。</summary>
public sealed class BattleHashProvider : IBusinessHashProvider
{
    private readonly IReadOnlyList<IHashableState> _states;

    public BattleHashProvider(params IHashableState[] states)
    {
        _states = states;
    }

    public ulong GetAllBusinessEntityHashes()
    {
        unchecked
        {
            ulong hash = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < _states.Count; i++)
            {
                hash ^= _states[i].ComputeHash() + 0x9E3779B97F4A7C15UL + (hash << 6) + (hash >> 2);
            }

            return hash;
        }
    }
}

/// <summary>把帧同步驱动器接到一个最小逻辑世界：Tick 时消费当帧输入并推进 hero。</summary>
public sealed class HeroInputWorldManager : IWorldManager
{
    private readonly SyncHero _hero;
    private readonly float _deltaTime;
    private IReadOnlyList<PlayerInputCommand> _pending = Array.Empty<PlayerInputCommand>();

    public HeroInputWorldManager(SyncHero hero, float deltaTime)
    {
        _hero = hero;
        _deltaTime = deltaTime;
    }

    public void QueueInputs(IReadOnlyList<PlayerInputCommand> inputs) => _pending = inputs ?? Array.Empty<PlayerInputCommand>();

    public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

    private readonly Dictionary<WorldId, IWorld> _worlds = new();

    public IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();

    public bool TryGet(WorldId id, out IWorld world)
    {
        world = null!;
        return false;
    }

    public bool Destroy(WorldId id) => false;

    public void Tick(float deltaTime)
    {
        _hero.ApplyInputs(_pending, _deltaTime);
        _pending = Array.Empty<PlayerInputCommand>();
    }

    public void DisposeAll() => _worlds.Clear();
}

internal static class SyncRuntimeDemo
{
    private const int TotalFrames = 30;
    private const float DeltaTime = 1f / 30f;
    private const long HeroEntityId = 1001;
    private const string PlayerId = "player-1";

    private static readonly FrameRecordMeta Meta = new()
    {
        WorldId = "sync-demo",
        WorldType = "syncrettime",
        TickRate = 30,
        RandomSeed = 42,
        PlayerId = PlayerId,
        StartedAtUnixMs = 0L,
    };

    public static void Run()
    {
        Log.Info("=== 录制阶段：确定性跑 30 帧，逐帧采样状态哈希 ===");
        var hero = new SyncHero(HeroEntityId, startX: 0f, startHealth: 100f);
        var hashProvider = new BattleHashProvider(hero);
        var stateManager = new StateManager(new SnapshotBuffer(64));
        stateManager.RegisterRollbackable(hero);
        var manager = new HeroInputWorldManager(hero, DeltaTime);
        var driver = new WorldManagerFrameDriver(manager);

        var recordedInputs = new List<FrameRecordInputFrame>();
        var recordedHashes = new List<FrameRecordStateHashFrame>();

        for (int f = 0; f < TotalFrames; f++)
        {
            var cmds = MakeDeterministicInputs(f);
            manager.QueueInputs(cmds);
            driver.Step(DeltaTime);

            stateManager.CaptureState(f);
            var hash = StateHashComputer.ComputeWithBusinessData(CreateSnapshot(f), hashProvider);

            foreach (var cmd in cmds)
            {
                recordedInputs.Add(new FrameRecordInputFrame
                {
                    Frame = f,
                    PlayerId = PlayerId,
                    OpCode = cmd.OpCode,
                    PayloadBase64 = cmd.Payload.Length == 0 ? string.Empty : Convert.ToBase64String(cmd.Payload),
                });
            }

            recordedHashes.Add(new FrameRecordStateHashFrame { Frame = f, Version = 1, Hash = (uint)hash.Value });
            Log.Info($"[Record] frame={f} hero.x={hero.Position.X:0.00} hp={hero.Health:0} hash={hash}");
        }

        var recordedFile = new FrameRecordFile
        {
            Meta = Meta,
            Inputs = recordedInputs,
            StateHashes = recordedHashes,
            Snapshots = new List<FrameRecordSnapshotFrame>(),
            Index = new List<FrameRecordChunkIndex>(),
        };

        Log.Info("=== 回放阶段：用录制的输入重跑，逐帧比对哈希 ===");
        var replayHero = new SyncHero(HeroEntityId, startX: 0f, startHealth: 100f);
        var replayHashProvider = new BattleHashProvider(replayHero);
        var replayManager = new HeroInputWorldManager(replayHero, DeltaTime);
        var replayDriver = new WorldManagerFrameDriver(replayManager);
        var source = new FrameRecordReplaySource(recordedFile);

        var replayInputs = new List<FrameRecordInputFrame>();
        var replayHashes = new List<FrameRecordStateHashFrame>();
        int matched = 0;

        for (int f = 0; f < TotalFrames; f++)
        {
            if (source.TryGetInputs(new FrameIndex(f), out var inputs))
            {
                replayManager.QueueInputs(inputs);
                foreach (var cmd in inputs)
                {
                    replayInputs.Add(new FrameRecordInputFrame
                    {
                        Frame = f,
                        PlayerId = PlayerId,
                        OpCode = cmd.OpCode,
                        PayloadBase64 = cmd.Payload.Length == 0 ? string.Empty : Convert.ToBase64String(cmd.Payload),
                    });
                }
            }

            replayDriver.Step(DeltaTime);

            var hash = StateHashComputer.ComputeWithBusinessData(CreateSnapshot(f), replayHashProvider);
            replayHashes.Add(new FrameRecordStateHashFrame { Frame = f, Version = 1, Hash = (uint)hash.Value });

            if (source.TryGetStateHash(new FrameIndex(f), out var expected, out _))
            {
                bool ok = (uint)hash.Value == expected.Value;
                matched += ok ? 1 : 0;
            }
        }

        Log.Info($"[Replay] 逐帧哈希匹配 {matched}/{TotalFrames}");

        var replayFile = new FrameRecordFile
        {
            Meta = Meta,
            Inputs = replayInputs,
            StateHashes = replayHashes,
            Snapshots = new List<FrameRecordSnapshotFrame>(),
            Index = new List<FrameRecordChunkIndex>(),
        };

        Log.Info("=== 确定性校验：DiffAnalyzer 比对两份帧记录 ===");
        var report = new FrameRecordDiffAnalyzer().Compare(recordedFile, replayFile, new FrameRecordDiffOptions { ContextFrames = 1 });
        Log.Info($"[DiffAnalyzer] Status={report.Status}");
        if (report.Status == FrameRecordDiffStatus.Identical)
        {
            Log.Info("[结论] 两份帧记录的状态哈希轨道完全一致 —— 逻辑确定性闭环成立（可回放 / 可断线重连恢复 / 可录像复盘）。");
        }
        else if (report.FirstDivergence != null)
        {
            Log.Warning($"[结论] 首次发散于 frame={report.FirstDivergence.Frame}（左={report.FirstDivergence.Left?.Hash} 右={report.FirstDivergence.Right?.Hash}）");
        }
    }

    /// <summary>确定性输入序列：每帧右移，每 5 帧额外受击一次。无随机源。</summary>
    private static List<PlayerInputCommand> MakeDeterministicInputs(int frame)
    {
        var cmds = new List<PlayerInputCommand>(2)
        {
            new(new FrameIndex(frame), new PlayerId(PlayerId), SyncInput.Move, Array.Empty<byte>()),
        };
        if (frame % 5 == 0 && frame > 0)
        {
            cmds.Add(new PlayerInputCommand(new FrameIndex(frame), new PlayerId(PlayerId), SyncInput.Hit, Array.Empty<byte>()));
        }

        return cmds;
    }

    private static StateSnapshot CreateSnapshot(int frame)
    {
        return new StateSnapshot
        {
            WorldId = 7,
            Frame = frame,
            Timestamp = frame,
            WorldFlags = 1,
            IsFullSnapshot = true,
        };
    }
}
