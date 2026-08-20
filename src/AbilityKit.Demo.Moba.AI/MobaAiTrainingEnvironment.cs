using AbilityKit.AI.Abstractions;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services.Behavior.AI;

namespace AbilityKit.Demo.Moba.AI;

/// <summary>
/// MOBA 无头 AI 训练环境选项。
/// </summary>
public readonly struct MobaAiEnvironmentOptions
{
    /// <summary>
    /// 创建 MOBA AI 环境选项。
    /// </summary>
    public MobaAiEnvironmentOptions(
        int maxObservedEntities = 8,
        float positionExtent = 64f,
        float hitPointScale = 1000f,
        int tickRate = 30)
    {
        MaxObservedEntities = maxObservedEntities < 1 ? 1 : maxObservedEntities;
        PositionExtent = positionExtent > 0f ? positionExtent : 64f;
        HitPointScale = hitPointScale > 0f ? hitPointScale : 1000f;
        TickRate = tickRate < 1 ? 30 : tickRate;
    }

    /// <summary>
    /// 每次观测最多编码的实体状态数量。
    /// </summary>
    public int MaxObservedEntities { get; }

    /// <summary>
    /// 用于归一化位置和偏移的世界空间范围。
    /// </summary>
    public float PositionExtent { get; }

    /// <summary>
    /// 最大生命值缺失时用于归一化生命字段的生命值尺度。
    /// </summary>
    public float HitPointScale { get; }

    /// <summary>
    /// 默认固定 step 时间使用的逻辑 tick 率。
    /// </summary>
    public int TickRate { get; }

    /// <summary>
    /// 默认 MOBA AI 环境选项。
    /// </summary>
    public static MobaAiEnvironmentOptions Default => new();
}

/// <summary>
/// 基于 Console 战斗启动器和运行时战斗端口的纯 C# MOBA 训练环境。
/// </summary>
public sealed class MobaAiTrainingEnvironment : IAiEnvironment, IDisposable
{
    private readonly MobaAiEnvironmentOptions _options;
    private readonly AiObservationBuffer _observation;
    private readonly MobaAiObservationBuilder _observationBuilder;
    private readonly MobaAiActionMapper _actionMapper;
    private readonly MobaAiRewardEvaluator _rewardEvaluator;
    private readonly List<LogicWorldEntityState> _states;
    private ConsoleBattleBootstrapper? _bootstrapper;
    private IMobaBattleRuntimePort? _runtime;
    private AiEpisodeOptions _episodeOptions;
    private int _stepIndex;
    private int _localActorId;

    /// <summary>
    /// 使用默认选项创建环境。
    /// </summary>
    public MobaAiTrainingEnvironment()
        : this(MobaAiEnvironmentOptions.Default)
    {
    }

    /// <summary>
    /// 使用显式选项创建环境。
    /// </summary>
    public MobaAiTrainingEnvironment(MobaAiEnvironmentOptions options)
    {
        _options = options;
        ObservationSpec = MobaAiObservationBuilder.CreateSpec(options);
        ActionSpec = MobaAiActionMapper.ActionSpec;
        _observation = new AiObservationBuffer(ObservationSpec);
        _observationBuilder = new MobaAiObservationBuilder(options);
        _actionMapper = new MobaAiActionMapper();
        _rewardEvaluator = new MobaAiRewardEvaluator();
        _states = new List<LogicWorldEntityState>(options.MaxObservedEntities + 4);
        _episodeOptions = new AiEpisodeOptions(1, 1, 1f / options.TickRate);
    }

    /// <inheritdoc />
    public AiObservationSpec ObservationSpec { get; }

    /// <inheritdoc />
    public AiActionSpec ActionSpec { get; }

    /// <summary>
    /// 当前用于诊断的 Console 启动器实例。
    /// </summary>
    public ConsoleBattleBootstrapper? Bootstrapper => _bootstrapper;

    /// <summary>
    /// 当前从启动器服务容器解析出的运行时战斗端口。
    /// </summary>
    public IMobaBattleRuntimePort? Runtime => _runtime;

    /// <summary>
    /// 当前解析出的运行时角色 ID，用作本地 AI 控制角色。
    /// </summary>
    public int LocalActorId => _localActorId;

    /// <summary>
    /// 当前规范化运行时实体状态，按实体 ID 排序。
    /// </summary>
    public IReadOnlyList<LogicWorldEntityState> CurrentEntityStates => _states;

    /// <inheritdoc />
    public AiStepResult Reset(in AiEpisodeOptions options)
    {
        DisposeBootstrapper();
        _episodeOptions = options;
        _stepIndex = 0;

        _bootstrapper = CreateBootstrapper(options.Seed);
        _bootstrapper.Initialize();
        _bootstrapper.Start();
        TickUntilPrepared(_bootstrapper);
        _bootstrapper.SetupBattle();
        _runtime = ResolveRuntime(_bootstrapper);

        FillStates();
        _localActorId = ResolveLocalActorId(_states, _bootstrapper.Context.LocalActorId);
        _bootstrapper.Context.LocalActorId = _localActorId;
        _rewardEvaluator.Reset(_states, _localActorId);
        _observationBuilder.Write(_states, _bootstrapper, _localActorId, _observation);
        return new AiStepResult(_observation, 0f, done: false, truncated: false, _stepIndex, ComputeStateHash(_states, _bootstrapper.Context.LastFrame, _localActorId));
    }

    /// <inheritdoc />
    public AiStepResult Step(in AiActionBuffer action)
    {
        if (_bootstrapper == null || _runtime == null)
        {
            throw new InvalidOperationException("MOBA AI environment must be reset before stepping.");
        }

        _actionMapper.Apply(action, _bootstrapper);
        _bootstrapper.Tick(_episodeOptions.FixedDeltaSeconds);
        _stepIndex++;

        FillStates();
        _localActorId = ResolveLocalActorId(_states, _localActorId);
        _bootstrapper.Context.LocalActorId = _localActorId;
        var reward = _rewardEvaluator.Evaluate(_states, _localActorId);
        _observationBuilder.Write(_states, _bootstrapper, _localActorId, _observation);

        var done = IsLocalActorDead(_states, _localActorId);
        var truncated = !done && _stepIndex >= _episodeOptions.MaxSteps;
        return new AiStepResult(_observation, reward, done, truncated, _stepIndex, ComputeStateHash(_states, _bootstrapper.Context.LastFrame, _localActorId));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeBootstrapper();
    }

    private static ConsoleBattleBootstrapper CreateBootstrapper(int seed)
    {
        var config = BattleStartConfig.CreateDefault();
        config.RandomSeed = seed;
        return new ConsoleBattleBootstrapper(config);
    }

    private static void TickUntilPrepared(ConsoleBattleBootstrapper bootstrapper)
    {
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
        {
            bootstrapper.Tick();
        }
    }

    private static IMobaBattleRuntimePort ResolveRuntime(ConsoleBattleBootstrapper bootstrapper)
    {
        var services = bootstrapper.RuntimeServices;
        if (services == null || !services.TryResolve<IMobaBattleRuntimePort>(out var runtime) || runtime == null)
        {
            throw new InvalidOperationException("MOBA AI environment failed to resolve IMobaBattleRuntimePort.");
        }

        return runtime;
    }

    private void FillStates()
    {
        _states.Clear();
        _runtime?.FillAllEntityStates(_states);
        _states.Sort(static (left, right) => left.EntityId.CompareTo(right.EntityId));
    }

    private void DisposeBootstrapper()
    {
        if (_bootstrapper == null) return;
        _bootstrapper.Stop();
        _bootstrapper.Dispose();
        _bootstrapper = null;
        _runtime = null;
    }

    private static int ResolveLocalActorId(IReadOnlyList<LogicWorldEntityState> states, int preferredActorId)
    {
        if (preferredActorId > 0)
        {
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].EntityId == preferredActorId)
                {
                    return preferredActorId;
                }
            }
        }

        return states.Count > 0 ? states[0].EntityId : preferredActorId;
    }

    private static bool IsLocalActorDead(IReadOnlyList<LogicWorldEntityState> states, int localActorId)
    {
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.EntityId == localActorId)
            {
                return state.IsDead;
            }
        }

        return false;
    }

    private static uint ComputeStateHash(IReadOnlyList<LogicWorldEntityState> states, int frame, int localActorId)
    {
        unchecked
        {
            var hash = 2166136261u;
            Mix(ref hash, frame);
            Mix(ref hash, states.Count);
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                Mix(ref hash, state.EntityId == localActorId ? 1 : 0);
                Mix(ref hash, state.TeamId);
                Mix(ref hash, Quantize(state.X));
                Mix(ref hash, Quantize(state.Y));
                Mix(ref hash, Quantize(state.Z));
                Mix(ref hash, Quantize(state.Hp));
                Mix(ref hash, Quantize(state.HpMax));
                Mix(ref hash, state.IsDead ? 1 : 0);
            }

            return hash == 0 ? 1u : hash;
        }
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }

    private static int Quantize(float value) => (int)MathF.Round(value * 1000f);
}

/// <summary>
/// 根据运行时实体状态缓冲构建固定长度 MOBA 观测。
/// </summary>
public sealed class MobaAiObservationBuilder
{
    private readonly MobaBrainObservationEncoder _encoder;

    /// <summary>
    /// 创建观测构建器。
    /// </summary>
    public MobaAiObservationBuilder(MobaAiEnvironmentOptions options)
    {
        _encoder = new MobaBrainObservationEncoder(ToRuntimeOptions(options));
    }

    /// <summary>
    /// 根据给定选项创建观测规格。
    /// </summary>
    public static AiObservationSpec CreateSpec(MobaAiEnvironmentOptions options)
    {
        var runtimeOptions = ToRuntimeOptions(options);
        return MobaBrainObservationEncoder.CreateSpec(in runtimeOptions);
    }

    /// <summary>
    /// 将当前运行时状态写入给定观测缓冲。
    /// </summary>
    public void Write(IReadOnlyList<LogicWorldEntityState> states, ConsoleBattleBootstrapper bootstrapper, int localActorId, AiObservationBuffer buffer)
    {
        if (states == null) throw new ArgumentNullException(nameof(states));
        if (bootstrapper == null) throw new ArgumentNullException(nameof(bootstrapper));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        _encoder.Write(
            states,
            localActorId,
            bootstrapper.Context.LastFrame,
            bootstrapper.RuntimeInputPortReady,
            bootstrapper.Flow.CurrentPhase == "InMatch",
            buffer);
    }

    private static MobaBrainObservationOptions ToRuntimeOptions(MobaAiEnvironmentOptions options)
    {
        return new MobaBrainObservationOptions(
            options.MaxObservedEntities,
            options.PositionExtent,
            options.HitPointScale);
    }
}

/// <summary>
/// 将通用 AI 动作映射到现有 MOBA Console 输入表面。
/// </summary>
public sealed class MobaAiActionMapper
{
    /// <summary>
    /// 连续分支表示 X/Z 移动；离散分支表示技能槽位，0 表示不释放技能。
    /// </summary>
    public static AiActionSpec ActionSpec => MobaBrainActionCodec.ActionSpec;

    /// <summary>
    /// 将动作应用到 Console 战斗上下文，供下一帧 tick 使用。
    /// </summary>
    public void Apply(in AiActionBuffer action, ConsoleBattleBootstrapper bootstrapper)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (bootstrapper == null) throw new ArgumentNullException(nameof(bootstrapper));

        var intent = MobaBrainActionCodec.Decode(action);
        bootstrapper.InputFeature.ApplyIntent(in intent);
    }
}

/// <summary>
/// 面向 MOBA smoke 级训练 episode 的最小奖励评估器。
/// </summary>
public sealed class MobaAiRewardEvaluator
{
    private float _lastLocalHp;
    private int _lastAliveEnemyCount;
    private bool _hasBaseline;

    /// <summary>
    /// 捕获初始奖励基线。
    /// </summary>
    public void Reset(IReadOnlyList<LogicWorldEntityState> states, int localActorId)
    {
        var local = FindLocal(states, localActorId);
        _lastLocalHp = local.Hp;
        _lastAliveEnemyCount = CountAliveEnemies(states, local.TeamId);
        _hasBaseline = true;
    }

    /// <summary>
    /// 根据生命保持、敌方击败和少量 step 成本评估奖励。
    /// </summary>
    public float Evaluate(IReadOnlyList<LogicWorldEntityState> states, int localActorId)
    {
        var local = FindLocal(states, localActorId);
        if (!_hasBaseline)
        {
            Reset(states, localActorId);
            return 0f;
        }

        var aliveEnemies = CountAliveEnemies(states, local.TeamId);
        var reward = -0.001f;
        reward += (_lastAliveEnemyCount - aliveEnemies) * 1.0f;
        reward += (local.Hp - _lastLocalHp) * 0.01f;
        if (local.IsDead)
        {
            reward -= 2f;
        }

        _lastLocalHp = local.Hp;
        _lastAliveEnemyCount = aliveEnemies;
        return reward;
    }

    private static LogicWorldEntityState FindLocal(IReadOnlyList<LogicWorldEntityState> states, int localActorId)
    {
        for (var i = 0; i < states.Count; i++)
        {
            if (states[i].EntityId == localActorId)
            {
                return states[i];
            }
        }

        return states.Count > 0 ? states[0] : LogicWorldEntityState.Empty(localActorId);
    }

    private static int CountAliveEnemies(IReadOnlyList<LogicWorldEntityState> states, int localTeamId)
    {
        var count = 0;
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (!state.IsDead && state.TeamId != 0 && state.TeamId != localTeamId)
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// MOBA AI 环境使用的确定性 smoke 策略。
/// </summary>
public sealed class MobaAiForwardSkillPolicy : IAiPolicy
{
    /// <inheritdoc />
    public AiActionSpec ActionSpec => MobaAiActionMapper.ActionSpec;

    /// <inheritdoc />
    public void Decide(in AiObservationBuffer observation, AiActionBuffer action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        action.Clear();
        if (action.Continuous.Length >= 2)
        {
            action.Continuous[0] = 1f;
            action.Continuous[1] = 0f;
        }

        if (action.Discrete.Length > 0)
        {
            action.Discrete[0] = 1;
        }
    }
}
