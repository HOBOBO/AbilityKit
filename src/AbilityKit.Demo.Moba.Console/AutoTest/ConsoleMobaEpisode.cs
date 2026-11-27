using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using SyncMode = AbilityKit.Demo.Moba.Share.SyncMode;

namespace AbilityKit.Demo.Moba.Console.AutoTest;

/// <summary>
/// ???????????????????????? (0, 0) ??????
/// </summary>
public readonly record struct ConsoleMobaAction(ConsoleMobaMove? Move = null, ConsoleMobaSkillInput? Skill = null);

public readonly record struct ConsoleMobaMove(float X, float Z);

/// <summary>
/// ???????????????AimPosition?AimDirection ??????0 ????????
/// </summary>
public readonly record struct ConsoleMobaSkillInput(int Slot, SkillInputPhase Phase, int TargetActorId = 0,
    Vec3 AimPosition = default, Vec3 AimDirection = default);

public readonly record struct ConsoleMobaSkillObservation(int Slot, int SkillId, int Level, long RemainingCooldownMs);

public sealed record ConsoleMobaActorObservation(int ActorId, Vec3 Position, float Hp, float MaxHp, float Mana,
    bool HasBrain, IReadOnlyList<ConsoleMobaSkillObservation> Skills);

/// <summary>
/// ???? 0 ??????????????????????????????????
/// </summary>
public sealed record ConsoleMobaObservation(int Seed, int Frame, ConsoleMobaActorObservation Player,
    ConsoleMobaActorObservation Opponent, bool Terminated, bool Truncated)
{
    public bool Done => Terminated || Truncated;
}

public sealed record ConsoleMobaRuleAiSample(int Episode, ConsoleMobaObservation Before, ConsoleMobaObservation After);

/// <summary>
/// ???? .NET 1v1 ?????????????? AI ? Console ???????? Unity ???
/// ?? Reset ????????????????????
/// </summary>
public sealed class ConsoleMobaEpisode(Func<int, BattleStartConfig> createBattle, int maxSteps) : IDisposable
{
    private readonly Func<int, BattleStartConfig> _createBattle = createBattle ?? throw new ArgumentNullException(nameof(createBattle));
    private readonly int _maxSteps = maxSteps > 0 ? maxSteps : throw new ArgumentOutOfRangeException(nameof(maxSteps));
    private ConsoleBattleBootstrapper? _battle;
    private ConsoleMobaObservation? _current;
    private IMobaInputCoordinator? _input;
    private MobaActorRegistry? _actors;
    private IFrameTime? _clock;
    private PlayerId _player;
    private float _delta;
    private bool _disposed;

    /// <summary>
    /// ???????????????????????????????????????
    /// </summary>
    public ConsoleMobaObservation Reset(int seed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var config = _createBattle(seed) ?? throw new InvalidOperationException("??????????????");
        if (config.TickRate <= 0 || config.SyncMode != SyncMode.Lockstep || config.Network != null ||
            config.EnableInputRecording || config.EnableInputReplay || config.EnableClientPrediction)
            throw new ArgumentException("???????????? Lockstep???????????????????", nameof(config));
        if (config.Players is not { Count: 2 } || config.Players.Any(player => player == null || string.IsNullOrWhiteSpace(player.PlayerId)) ||
            config.Players[0].TeamId == config.Players[1].TeamId || config.Players.Count(player => player.PlayerId == config.PlayerId) != 1)
            throw new ArgumentException("??????????????????????????", nameof(config));

        CloseBattle();
        config.RandomSeed = seed;
        _delta = 1f / config.TickRate;
        _player = new PlayerId(config.PlayerId);
        _battle = new ConsoleBattleBootstrapper(config, enableInteractiveInput: false);
        try
        {
            _battle.Initialize();
            _battle.Start();
            for (var frame = 0; frame < 8 && _battle.Context.EcsWorld == null; frame++) _battle.Tick(_delta);
            _battle.SetupBattle();
            if (_battle.Flow.CurrentPhase != "InMatch" || _battle.RuntimeServices is not { } services)
                throw new InvalidOperationException("Console ????????????");

            _input = services.Resolve<IMobaInputCoordinator>();
            _actors = services.Resolve<MobaActorRegistry>();
            _clock = services.Resolve<IFrameTime>();
            var players = services.Resolve<MobaPlayerActorMapService>();
            var opponent = new PlayerId(config.Players.Single(player => player.PlayerId != config.PlayerId).PlayerId);
            if (!players.TryGetActorId(_player, out var playerId) || !players.TryGetActorId(opponent, out var opponentId) ||
                !_actors.TryGet(playerId, out _) || !_actors.TryGet(opponentId, out _))
                throw new InvalidOperationException("1v1 ?????????????");

            return _current = Observe(seed, 0, playerId, opponentId);
        }
        catch
        {
            CloseBattle();
            throw;
        }
    }

    /// <summary>
    /// ???????????????????????????????????? AI ?????
    /// ????????????????????????????
    /// </summary>
    public ConsoleMobaObservation Step(ConsoleMobaAction action = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_battle == null || _current is not { Done: false } current)
            throw new InvalidOperationException("???????????????????? Reset?");
        if (current.Player.HasBrain && (action.Move.HasValue || action.Skill.HasValue))
            throw new InvalidOperationException("???????? AI ???????????");

        var frame = new FrameIndex(_battle.Context.LastFrame + 1);
        var commands = BuildCommands(action, frame);
        try
        {
            if (commands.Count > 0)
            {
                var result = _input!.TrySubmit(frame, commands.ToArray());
                if (!result.Succeeded || result.HandledCount != commands.Count)
                    throw new InvalidOperationException($"???????????{result}");
            }
            _battle.Tick(_delta);
            if (_battle.Context.LastFrame != frame.Value)
                throw new InvalidOperationException("Console ??????????");

            return _current = Observe(current.Seed, current.Frame + 1, current.Player.ActorId, current.Opponent.ActorId);
        }
        catch
        {
            _current = null;
            throw;
        }
    }

    /// <summary>
    /// ??????????????? AI ????????????????? AI ????????
    /// </summary>
    public static IEnumerable<ConsoleMobaRuleAiSample> SampleRuleAi(Func<int, BattleStartConfig> createBattle,
        IEnumerable<int> seeds, int maxSteps)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        using var episode = new ConsoleMobaEpisode(createBattle, maxSteps);
        var episodeIndex = 0;
        foreach (var seed in seeds)
        {
            var before = episode.Reset(seed);
            if (!before.Player.HasBrain && !before.Opponent.HasBrain)
                throw new InvalidOperationException("?? AI ????????????? Brain?");
            while (!before.Done)
            {
                var after = episode.Step();
                yield return new ConsoleMobaRuleAiSample(episodeIndex, before, after);
                before = after;
            }
            episodeIndex++;
        }
    }

    private List<PlayerInputCommand> BuildCommands(ConsoleMobaAction action, FrameIndex frame)
    {
        var commands = new List<PlayerInputCommand>(2);
        if (action.Move is { } move)
        {
            if (!float.IsFinite(move.X) || !float.IsFinite(move.Z) || MathF.Abs(move.X) > 1 || MathF.Abs(move.Z) > 1)
                throw new ArgumentOutOfRangeException(nameof(action), "?????? [-1, 1] ??");
            commands.Add(new PlayerInputCommand(frame, _player, MobaOpCodes.Input.Move, MobaMoveCodec.Serialize(move.X, move.Z)));
        }
        if (action.Skill is { } skill)
        {
            if (skill.Slot <= 0 || !Enum.IsDefined(skill.Phase) || skill.TargetActorId < 0 ||
                !IsFinite(skill.AimPosition) || !IsFinite(skill.AimDirection))
                throw new ArgumentOutOfRangeException(nameof(action), "????????????????");
            var input = new SkillInputEvent(skill.Slot, skill.Phase, targetActorId: skill.TargetActorId,
                aimPos: skill.AimPosition, aimDir: skill.AimDirection);
            commands.Add(new PlayerInputCommand(frame, _player, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input)));
        }
        return commands;
    }

    private static bool IsFinite(Vec3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private ConsoleMobaObservation Observe(int seed, int frame, int playerId, int opponentId)
    {
        var now = MobaSkillRuntimeAccess.GetCurrentTimeMs(_clock);
        var player = ObserveActor(playerId, _current?.Player);
        var opponent = ObserveActor(opponentId, _current?.Opponent);
        var terminated = player.Hp <= 0 || opponent.Hp <= 0;
        return new ConsoleMobaObservation(seed, frame, player, opponent, terminated, !terminated && frame >= _maxSteps);

        ConsoleMobaActorObservation ObserveActor(int actorId, ConsoleMobaActorObservation? previous)
        {
            if (!_actors!.TryGet(actorId, out var actor))
                return new ConsoleMobaActorObservation(actorId, previous?.Position ?? default, 0, previous?.MaxHp ?? 0,
                    0, false, Array.Empty<ConsoleMobaSkillObservation>());

            var attrs = new MobaAttrs(actor);
            var skills = new List<ConsoleMobaSkillObservation>();
            if (actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills is { } active)
                for (var index = 0; index < active.Length; index++)
                    if (active[index] is { SkillId: > 0 } skill)
                        skills.Add(new ConsoleMobaSkillObservation(index + 1, skill.SkillId, skill.Level,
                            Math.Max(0, skill.CooldownEndTimeMs - now)));
            return new ConsoleMobaActorObservation(actorId, actor.transform.Value.Position, attrs.Hp, attrs.MaxHp,
                attrs.Mana, actor.hasActorBrain, skills.AsReadOnly());
        }
    }

    private void CloseBattle()
    {
        _current = null;
        _input = null;
        _actors = null;
        _clock = null;
        var battle = _battle;
        _battle = null;
        battle?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseBattle();
    }
}
