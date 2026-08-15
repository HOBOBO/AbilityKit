using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Modifiers;
using AbilityKit.Pipeline;
using AbilityKit.Samples.Foundation;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Samples.SkillCore;

/// <summary>接入方派生的具体管线：AbilityPipeline 是抽象基类，按玩法各自收口。</summary>
public sealed class SkillPipeline : AbilityPipeline<SkillContext>
{
    protected override void ReleaseContext(SkillContext context)
    {
        // 上下文由本示例自管理；接入方若用 PipelinePools 池化上下文，应在这里归还。
    }
}

/// <summary>
/// SkillCore 业务模块：把事件总线、技能驱动服务注册进世界容器，
/// 并在 OnInit 时装配一条 RPN 条件触发规则（triggering）。
/// </summary>
public sealed class SkillCoreModule : IWorldModule, IWorldModuleInfo
{
    public string Id => "skillcore";

    public int Order => 10;

    public Type[] DependsOn => Array.Empty<Type>();

    public Type[] ConflictsWith => Array.Empty<Type>();

    public void Configure(WorldContainerBuilder builder)
    {
        builder.RegisterInstance(new EventBus());

        // 同一实例同时以 ISkillCastService（供 Main 调用）与 IFoundationTickLoop（供 FoundationWorld.Tick 驱动）暴露。
        builder.Register<ISkillCastService>(
            WorldLifetime.Singleton,
            r => new SkillCastService(r.Resolve<EventBus>()));
        builder.Register<IFoundationTickLoop>(
            WorldLifetime.Singleton,
            r => (IFoundationTickLoop)r.Resolve<ISkillCastService>());
    }
}

/// <summary>战斗目标：业务层自管修改器列表（modifiers 框架不预设存储）。</summary>
public sealed class CombatTarget
{
    public const int EnemySourceId = 100;

    public string Name { get; }

    public float BaseMoveSpeed { get; }

    public float BaseAttackPower { get; }

    public List<ModifierData> Modifiers { get; } = new(8);

    public CombatTarget(string name, float baseMoveSpeed, float baseAttackPower)
    {
        Name = name;
        BaseMoveSpeed = baseMoveSpeed;
        BaseAttackPower = baseAttackPower;
    }
}

/// <summary>伤害事件：既是技能管线的效果出口，也是触发规则的 payload 来源。</summary>
public readonly struct DamageEvent
{
    public readonly int Amount;

    public DamageEvent(int amount)
    {
        Amount = amount;
    }
}

/// <summary>把 DamageEvent.Amount 映射成 RPN 表达式里的 "payload:amount"。</summary>
public sealed class DamageEventPayloadAccessor : IPayloadIntAccessor<DamageEvent>
{
    public bool TryGet(in DamageEvent args, int fieldId, out int value)
    {
        if (fieldId == StableStringId.Get("payload:amount"))
        {
            value = args.Amount;
            return true;
        }

        value = default;
        return false;
    }
}

public interface ISkillCastService : IService
{
    CombatTarget Target { get; }

    void SetupTriggerRule(float threshold);

    void CastFireball();

    void CastWeaken();

    void PublishDamageProbe(int amount);

    int ActivePipelines { get; }
}

/// <summary>
/// 技能驱动服务：持有活动管线 run 并每帧 Tick；
/// 同时装配 TriggerRunner 演示「伤害事件 + RPN 条件 → 触发动作」。
/// </summary>
public sealed class SkillCastService : ISkillCastService, IFoundationTickLoop, IWorldInitializable, IWorldDeinitializable
{
    private readonly EventBus _bus;
    private readonly List<IAbilityPipelineRun<SkillContext>> _runs = new(8);
    private readonly ModifierCalculator _calculator = new();
    private TriggerRunner<TriggerContext>? _runner;
    private RpnNumericExprRuntime? _thresholdExpr;

    public CombatTarget Target { get; } = new("Goblin", baseMoveSpeed: 100f, baseAttackPower: 10f);

    public int ActivePipelines => _runs.Count;

    public SkillCastService(EventBus bus)
    {
        _bus = bus;
    }

    public void OnInit(IWorldResolver services)
    {
        _runner = new TriggerRunner<TriggerContext>(
            _bus,
            new FunctionRegistry(),
            new ActionRegistry(),
            contextSource: null,
            observer: null,
            blackboards: CreateBlackboards(),
            payloads: CreatePayloads());
        Log.Info($"[SkillCast] OnInit —— 目标 {Target.Name}（基础移速 {Target.BaseMoveSpeed:0}，基础攻击 {Target.BaseAttackPower:0}）");
    }

    /// <summary>装配触发规则：RPN 表达式 (payload:amount + bb:combat:atk) ≥ threshold 时触发。</summary>
    public void SetupTriggerRule(float threshold)
    {
        var expr = new RpnNumericExprRuntime(
            new RpnNumericExprPlan(RpnNumericExprParser.LangRpnV1, "payload:amount bb:combat:atk +"));
        _thresholdExpr = expr;

        var key = new EventKey<DamageEvent>(StableStringId.Get("event:damage"));
        var trigger = new DelegateTrigger<DamageEvent, TriggerContext>(
            predicate: (evt, ctx) => expr.Eval(in evt, in ctx) >= threshold,
            actions: (evt, ctx) =>
            {
                var v = expr.Eval(in evt, in ctx);
                Log.Info($"[Trigger] 触发规则命中：payload.amount + bb.atk = {v:0.#} ≥ {threshold:0.#}，执行反击动作");
            });

        _runner!.Register(key, trigger, phase: 0, priority: 0);
        Log.Info($"[Trigger] 规则已注册：伤害 + 黑板攻击力 ≥ {threshold:0.#} 触发反击");
    }

    /// <summary>技能 1「火球」：前摇 0.3s → 三连发伤害（间隔 0.1s）→ 后摇 0.2s。</summary>
    public void CastFireball()
    {
        var pipeline = new SkillPipeline();
        pipeline.AddPhase(new AbilityDelayPhase<SkillContext>(0.3f));

        var barrage = new AbilityRepeatPhase<SkillContext>(3);
        barrage.RepeatInterval = 0.1f;
        barrage.SetRepeatAction((ctx, index) =>
        {
            int amount = 25 + index * 5;
            Log.Info($"[Skill] 火球弹 {index + 1}/3 命中 {Target.Name}，伤害 {amount}");
            _bus.Publish(new EventKey<DamageEvent>(StableStringId.Get("event:damage")), new DamageEvent(amount));
        });
        pipeline.AddPhase(barrage);

        pipeline.AddPhase(new AbilityDelayPhase<SkillContext>(0.2f));

        StartRun(pipeline, "Fireball");
    }

    /// <summary>
    /// 技能 2「虚弱」：立即给目标挂移动速度 ×0.5 的 Buff（modifiers），
    /// 每 0.5s 造成一次 DOT，5 跳后结束并移除 Buff。
    /// </summary>
    public void CastWeaken()
    {
        var slow = ModifierData.Mul(ModifierKey.MoveSpeed, 0.5f, sourceId: CombatTarget.EnemySourceId);
        Target.Modifiers.Add(slow);
        Log.Info($"[Buff] 虚弱已挂载 —— {Target.Name} 当前移速 {CurrentMoveSpeed():0}（基础 {Target.BaseMoveSpeed:0} × 0.5）");

        var pipeline = new SkillPipeline();

        var dot = new AbilityRepeatPhase<SkillContext>(5);
        dot.RepeatInterval = 0.5f;
        dot.SetRepeatAction((ctx, index) =>
        {
            Log.Info($"[Buff] 虚弱 DOT 第 {index + 1}/5 跳，伤害 8");
            _bus.Publish(new EventKey<DamageEvent>(StableStringId.Get("event:damage")), new DamageEvent(8));
        });
        pipeline.AddPhase(dot);

        pipeline.Events.OnPipelineComplete += ctx =>
        {
            Target.Modifiers.Remove(slow);
            Log.Info($"[Buff] 虚弱结束 —— {Target.Name} 移速恢复 {CurrentMoveSpeed():0}");
        };

        StartRun(pipeline, "Weaken");
    }

    /// <summary>手动派发一次伤害事件，验证触发规则的命中 / 不命中两条路径。</summary>
    public void PublishDamageProbe(int amount)
    {
        Log.Info($"[Probe] 派发伤害事件 amount={amount}（黑板 atk=7，阈值 12：{amount + 7} ≥ 12 ?）");
        _bus.Publish(new EventKey<DamageEvent>(StableStringId.Get("event:damage")), new DamageEvent(amount));
    }

    public void Tick(float deltaTime)
    {
        for (int i = _runs.Count - 1; i >= 0; i--)
        {
            _runs[i].Tick(deltaTime);
            if (_runs[i].State != EAbilityPipelineState.Executing)
            {
                _runs.RemoveAt(i);
            }
        }

        _bus.Flush();
    }

    public void OnDeinit(IWorldResolver services)
    {
        Log.Info($"[SkillCast] OnDeinit —— 剩余活动管线 {_runs.Count} 已清理");
        _runs.Clear();
    }

    public void Dispose()
    {
    }

    private float CurrentMoveSpeed()
        => _calculator.Calculate(Target.Modifiers.ToArray(), Target.BaseMoveSpeed).FinalValue;

    private void StartRun(AbilityPipeline<SkillContext> pipeline, string name)
    {
        var context = new SkillContext();
        context.SetData("skill", name);
        var run = pipeline.Start(new SkillPipelineConfig(), context);
        _runs.Add(run);
        Log.Info($"[Skill] {name} 开始施放");
    }

    private static DictionaryBlackboardResolver CreateBlackboards()
    {
        var resolver = new DictionaryBlackboardResolver();
        var board = new DictionaryBlackboard();
        var boardId = StableStringId.Get("bb:combat");
        resolver.Register(boardId, board);
        board.SetDouble(StableStringId.Get("bb:combat:atk"), 7d);
        return resolver;
    }

    private static PayloadAccessorRegistry CreatePayloads()
    {
        var payloads = new PayloadAccessorRegistry();
        payloads.RegisterIntAccessor(new DamageEventPayloadAccessor());
        return payloads;
    }
}
