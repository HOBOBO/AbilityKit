using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 重构后的行为系统使用示例
    /// </summary>
    public static class RefactoredExecutableExamples
    {
        // ====================================================================
        // 1. 基础原子行为
        // ====================================================================

        public static void BasicAtomExamples()
        {
            // Action 调用 (使用 int 作为 ActionId)
            var action = ExecutableDsl.Action(new ActionId(1), 10.0);

            // 延迟 (瞬时返回，延迟信息由调度器控制)
            var delay = ExecutableDsl.Delay(1000f);

            // 打印日志
            var log = ExecutableDsl.Log("Hello, World!");

            // 发送事件
            var evt = ExecutableDsl.Event("OnDamageDealt");

            // 空行为
            var noop = ExecutableDsl.NoOp();

            // 显式失败/成功
            var fail = ExecutableDsl.Fail("Insufficient mana");
            var success = ExecutableDsl.Success();
        }

        // ====================================================================
        // 2. 复合行为
        // ====================================================================

        public static void CompositeExamples()
        {
            // Sequence - 顺序执行
            var sequence = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Log("Step 1"))
                .Add(ExecutableDsl.Log("Step 2"))
                .Add(ExecutableDsl.Delay(500f))
                .Add(ExecutableDsl.Event("OnComplete"))
                .Build();

            // Selector - 选择第一个成功的
            var selector = ExecutableDsl.Selector()
                .Add(CreateMeleeAttack())
                .Add(CreateRangedAttack())
                .Add(CreateDefaultAttack())
                .Build();

            // Parallel - 并行执行
            var parallel = ExecutableDsl.Parallel()
                .Add(ExecutableDsl.Event("PlayVFX"))
                .Add(ExecutableDsl.Event("PlaySFX"))
                .Add(ExecutableDsl.Log("Both started"))
                .Build();

            // RandomSelector - 随机选择
            var random = ExecutableDsl.RandomSelector()
                .Add(ExecutableDsl.Log("Option A"), 0.3f)  // 30%
                .Add(ExecutableDsl.Log("Option B"), 0.5f)  // 50%
                .Add(ExecutableDsl.Log("Option C"), 0.2f) // 20%
                .Build();

            // Repeat - 重复执行
            var repeat = ExecutableDsl.Repeat()
                .SetChild(ExecutableDsl.Log("Repeated"))
                .SetCount(3)
                .StopOnFailure(true)
                .Build();

            // Until - 直到成功/失败
            var untilSuccess = ExecutableDsl.Until()
                .SetChild(ExecutableDsl.Action(new ActionId(100)))
                .UntilSuccess()
                .SetMaxIterations(5)
                .Build();
        }

        // ====================================================================
        // 3. 条件分支
        // ====================================================================

        public static void ConditionalExamples()
        {
            // If - 单条件
            var ifExec = ExecutableDsl.If(
                ConditionBuilderExtensions.Gt(NumericValueRef.Var("domain", "health"), NumericValueRef.Const(50)),
                ExecutableDsl.Sequence()
                    .Add(ExecutableDsl.Log("Health > 50"))
                    .Add(ExecutableDsl.Action(new ActionId(200)))
                    .Build()
            );

            // If-ElseIf-Else - 多条件
            var ifElse = ExecutableDsl.IfElse()
                .If(
                    ConditionBuilderExtensions.Gt(NumericValueRef.Var("domain", "health"), NumericValueRef.Const(80)),
                    ExecutableDsl.Log("Health is high")
                )
                .ElseIf(
                    ConditionBuilderExtensions.Gt(NumericValueRef.Var("domain", "health"), NumericValueRef.Const(30)),
                    ExecutableDsl.Log("Health is medium")
                )
                .Else(
                    ExecutableDsl.Log("Health is low")
                )
                .Build();

            // Switch - 值匹配
            var switchExec = ExecutableDsl.Switch()
                .Selector(ctx => GetSkillLevel(ctx))
                .Case(1, ExecutableDsl.Log("Skill level 1"))
                .Case(2, ExecutableDsl.Log("Skill level 2"))
                .Case(3, ExecutableDsl.Log("Skill level 3"))
                .Default(ExecutableDsl.Log("Unknown skill level"))
                .Build();
        }

        // ====================================================================
        // 4. 调度行为 (原持续行为)
        // ====================================================================

        public static void ScheduledExamples(object ctx)
        {
            // Timed - 定时行为
            IScheduledExecutable timedExec = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Log("Starting"))
                .Build()
                .Timed(5000f); // 持续 5 秒

            // Periodic - 周期行为
            IScheduledExecutable periodicExec = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Action(new ActionId(300)))
                .Build()
                .Periodic(1000f, 10); // 每秒一次，最多 10 次

            // External - 外部控制
            IScheduledExecutable externalExec = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Log("Channeling..."))
                .Build()
                .External();

            // 创建调度执行器
            var executor = new ScheduledExecutor();

            // 启动调度行为
            long handle = executor.Start(timedExec, ctx,
                onCompleted: h => Console.WriteLine("Timed ability completed"),
                onInterrupted: (h, reason) => Console.WriteLine($"Interrupted: {reason}"));

            // 每帧更新
            // executor.Update(deltaTimeMs);

            // 中断
            // executor.Interrupt(handle, "Player cancelled");
        }

        // ====================================================================
        // 5. 组合使用示例
        // ====================================================================

        public static ISimpleExecutable CreateFireballAbility()
        {
            return ExecutableDsl.Sequence()
                // 前摇阶段
                .Add(ExecutableDsl.If(
                    ConditionBuilderExtensions.HasTarget(),
                    ExecutableDsl.Sequence()
                        .Add(ExecutableDsl.Action(new ActionId(400)))
                        .Add(ExecutableDsl.Log("Targeting enemy"))
                        .Build()
                ))
                // 施法阶段
                .Add(ExecutableDsl.Action(new ActionId(401)))
                .Add(ExecutableDsl.Delay(500f))
                // 效果阶段
                .Add(ExecutableDsl.If(
                    ConditionBuilderExtensions.HasTarget(),
                    ExecutableDsl.Sequence()
                        .Add(ExecutableDsl.Action(new ActionId(402), 100.0))
                        .Add(ExecutableDsl.Action(new ActionId(403)))
                        .Add(ExecutableDsl.Event("OnDamageDealt"))
                        .Build()
                ))
                // 冷却提示
                .Add(ExecutableDsl.Event("OnAbilityOnCooldown"))
                .Build();
        }

        public static ISimpleExecutable CreateDOTAbility()
        {
            // DOT = 持续伤害效果
            var dotBody = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Action(new ActionId(500)))
                .Add(ExecutableDsl.Event("OnDOTTick"))
                .Build();

            // 注意：实际使用中需要将 dotBody.Timed() 包装为可添加到 Sequence 的形式
            // 这里作为示例展示概念
            var dotExec = dotBody.Timed(5000f);

            return ExecutableDsl.Sequence()
                .Add(ExecutableDsl.If(
                    ConditionBuilderExtensions.HasTarget(),
                    ExecutableDsl.Log("DOT applied") // 实际应使用 dotExec
                ))
                .Add(ExecutableDsl.Log("DOT applied"))
                .Build();
        }

        public static ISimpleExecutable CreateUltimateAbility()
        {
            var aoeSequence = ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Action(new ActionId(601)))
                .Add(ExecutableDsl.Event("OnAOEHit"))
                .Add(ExecutableDsl.Delay(200f))
                .Build();

            // 周期执行 AOE
            var periodicAOE = aoeSequence.Periodic(200f, 10);

            return ExecutableDsl.Sequence()
                .Add(ExecutableDsl.Log("Ultimate charging..."))
                .Add(ExecutableDsl.Action(new ActionId(600)))
                // 注意：实际使用中 periodicAOE 需要转换为 ISimpleExecutable 或直接用于 ScheduledExecutor
                .Add(ExecutableDsl.Log("Ultimate released!"))
                .Build();
        }

        // ====================================================================
        // 辅助方法
        // ====================================================================

        private static ISimpleExecutable CreateMeleeAttack()
        {
            return ExecutableDsl.Sequence()
                .Add(ExecutableDsl.If(
                    ConditionBuilderExtensions.Lt(NumericValueRef.Var("domain", "distance"), NumericValueRef.Const(5)),
                    ExecutableDsl.Action(new ActionId(700))
                ))
                .Build();
        }

        private static ISimpleExecutable CreateRangedAttack()
        {
            return ExecutableDsl.Sequence()
                .Add(ExecutableDsl.If(
                    ConditionBuilderExtensions.Gt(NumericValueRef.Var("domain", "distance"), NumericValueRef.Const(5)),
                    ExecutableDsl.Action(new ActionId(701))
                ))
                .Build();
        }

        private static ISimpleExecutable CreateDefaultAttack()
        {
            return ExecutableDsl.Action(new ActionId(702));
        }

        private static int GetSkillLevel(object ctx)
        {
            // 从上下文获取技能等级
            return 2;
        }
    }

    // ========================================================================
    // 示例：如何创建自定义调度行为工厂
    // ========================================================================

    /// <summary>
    /// 示例：自定义调度行为工厂
    /// </summary>
    [ScheduledExecutableFactory("conditional", priority: 10)]
    public class ConditionalScheduledFactory : IScheduledExecutableFactory
    {
        public string Name => "conditional";
        public int Priority => 10;

        public IScheduledExecutable Create(ScheduleFactoryConfig config, ActionRegistry actions, object context)
        {
            // 返回条件调度行为
            return new ConditionalScheduledExecutable
            {
                DurationMs = config.DurationMs
            };
        }
    }

    /// <summary>
    /// 条件满足时执行的行为
    /// </summary>
    public sealed class ConditionalScheduledExecutable : IScheduledExecutable
    {
        public string Name => "Conditional";
        public ExecutableMetadata Metadata => new(2000, "Conditional", isScheduled: true);
        public EScheduleMode ScheduleMode => EScheduleMode.Conditional;
        public bool IsPeriodic => false;
        public float PeriodMs => 0;
        public float DurationMs { get; set; } = -1;

        public ICondition Condition { get; set; }
        public ISimpleExecutable Inner { get; set; }
        public bool CanBeInterrupted { get; set; } = true;

        public ExecutionResult Execute(object ctx)
        {
            if (Condition?.Evaluate(ctx).Passed == true)
            {
                Inner?.Execute(ctx);
            }
            return ExecutionResult.Success();
        }
    }
}
