using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Attributes.Core;
using AbilityKit.Deterministic;
using AbilityKit.Effect;
using AbilityKit.ECS;
using GameplayTagContainer = AbilityKit.GameplayTags.GameplayTagContainer;
using GameplayTagRequirements = AbilityKit.GameplayTags.GameplayTagRequirements;
using Xunit;

namespace AbilityKit.Ability.Tests;

/// <summary>
/// EffectContainer 定点计时契约：计时字段以 Q32.32 raw long 累加（整数运算无漂移），
/// 到期/周期判定与 dyadic 数值下的 float 语义逐位一致；非 dyadic 步长的累计值与
/// 定点复算一致（raw 累加，而非 float 累加）。
/// </summary>
public sealed class EffectContainerDeterminismTests
{
    private const float Step = 0.125f;

    [Fact]
    public void Duration_and_period_expire_on_exact_dyadic_boundaries()
    {
        var container = new EffectContainer();
        var ticks = new TickCounterComponent();
        var unit = new StubUnitFacade(container);
        var frameTime = new FrameTime();
        var context = new EffectExecutionContext(null, frameTime, null, null, 0L, unit, null);
        var spec = new GameplayEffectSpec(
            EffectDurationPolicy.Duration,
            durationSeconds: 0.5f,
            periodSeconds: 0.25f,
            new GameplayTagRequirements(null, null),
            grantedTags: null,
            components: new List<IEffectComponent> { ticks });

        container.Apply(spec, in context);
        Assert.Single(container.Active);

        for (var i = 1; i <= 4; i++)
        {
            frameTime.StepTo(new FrameIndex(i), Step);
            container.Step(in context);
        }

        // 0.5s 到期移除；周期 0.25s 在第 2、4 步各触发一次（到期帧先 tick 后移除）。
        Assert.Empty(container.Active);
        Assert.Equal(2, ticks.Count);
    }

    [Fact]
    public void Elapsed_matches_fixed_point_accumulation_for_non_dyadic_step()
    {
        var container = new EffectContainer();
        var unit = new StubUnitFacade(container);
        var frameTime = new FrameTime();
        var context = new EffectExecutionContext(null, frameTime, null, null, 0L, unit, null);
        var spec = new GameplayEffectSpec(
            EffectDurationPolicy.Duration,
            durationSeconds: 100f,
            periodSeconds: 0f,
            new GameplayTagRequirements(null, null),
            grantedTags: null,
            components: null);

        var inst = container.Apply(spec, in context);

        const int steps = 10;
        var dt = 1f / 30f;
        for (var i = 1; i <= steps; i++)
        {
            frameTime.StepTo(new FrameIndex(i), dt);
            container.Step(in context);
        }

        // 契约：累计 = N × 单步 raw（整数累加），而不是 N 次 float 加法。
        // 注意只能经 float 视图断言（ElapseRaw 为 internal；float 反解 raw 会丢低位，不做反向断言）。
        var expected = Fixed64.FromSingle(dt) * steps;
        Assert.Equal(expected.ToSingle(), inst!.ElapsedSeconds);
    }

    private sealed class TickCounterComponent : IEffectComponent
    {
        public int Count;

        public void OnApply(in EffectExecutionContext context, EffectInstance instance)
        {
        }

        public void OnTick(in EffectExecutionContext context, EffectInstance instance)
        {
            Count++;
        }

        public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
        {
        }
    }

    private sealed class StubUnitFacade : IUnitFacade
    {
        public StubUnitFacade(EffectContainer effects)
        {
            Effects = effects;
        }

        public EcsEntityId Id => default;
        public GameplayTagContainer Tags => null;
        public AttributeContext Attributes => null;
        public EffectContainer Effects { get; }
    }
}
