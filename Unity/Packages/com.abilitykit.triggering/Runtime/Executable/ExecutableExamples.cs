using System;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 使用示例
    /// </summary>
    public static class ExecutableExamples
    {
        public static class MyActionIds
        {
            public static readonly ActionId PlayVfx = new(1);
            public static readonly ActionId ApplyDamage = new(2);
            public static readonly ActionId Heal = new(3);
            public static readonly ActionId SpawnProjectile = new(4);
        }

        public static class MyPayloadFields
        {
            public const int Damage = 1;
            public const int TargetType = 2;
            public const int Level = 3;
        }

        /// <summary>
        /// 简单 Sequence
        /// </summary>
        public static ISimpleExecutable SimpleSequence()
        {
            return ExecutableDsl.Sequence()
                .AddAction(MyActionIds.PlayVfx)
                .AddAction(MyActionIds.ApplyDamage, 100.0)
                .AddAction(MyActionIds.Heal, 50.0)
                .Build();
        }

        /// <summary>
        /// 带条件的 If
        /// </summary>
        public static ISimpleExecutable ConditionalIf()
        {
            var condition = ConditionBuilderExtensions.Lt(
                NumericValueRef.Const(30),
                NumericValueRef.Const(100));

            return ExecutableDsl.If(condition, ExecutableDsl.Action(MyActionIds.ApplyDamage, 200.0));
        }

        /// <summary>
        /// If-ElseIf-Else
        /// </summary>
        public static ISimpleExecutable IfElseIfElse()
        {
            var bossCondition = ConditionBuilderExtensions.Eq(
                NumericValueRef.Const(1),
                NumericValueRef.Const(1));

            var eliteCondition = ConditionBuilderExtensions.Eq(
                NumericValueRef.Const(2),
                NumericValueRef.Const(2));

            return ExecutableDsl.IfElse()
                .If(bossCondition,
                    ExecutableDsl.Action(MyActionIds.ApplyDamage, 500.0))
                .ElseIf(eliteCondition,
                    ExecutableDsl.Action(MyActionIds.ApplyDamage, 250.0))
                .Else(
                    ExecutableDsl.Action(MyActionIds.ApplyDamage, 100.0))
                .Build();
        }

        /// <summary>
        /// Switch
        /// </summary>
        public static ISimpleExecutable SwitchExample()
        {
            return ExecutableDsl.Switch()
                .Selector(ctx => 1)
                .Case(1, ExecutableDsl.Action(MyActionIds.ApplyDamage, 500.0))
                .Case(2, ExecutableDsl.Action(MyActionIds.ApplyDamage, 250.0))
                .Case(3, ExecutableDsl.Action(MyActionIds.ApplyDamage, 100.0))
                .Default(ExecutableDsl.Action(MyActionIds.ApplyDamage, 50.0))
                .Build();
        }

        /// <summary>
        /// 复杂条件组合
        /// </summary>
        public static ISimpleExecutable ComplexCondition()
        {
            var levelCondition = ConditionBuilderExtensions.Ge(
                NumericValueRef.Const(5),
                NumericValueRef.Const(5));

            var hpCondition = ConditionBuilderExtensions.Lt(
                NumericValueRef.Const(50),
                NumericValueRef.Const(100));

            var multiCondition = ConditionBuilderExtensions.AllOf(levelCondition, hpCondition);

            return ExecutableDsl.If(multiCondition,
                    ExecutableDsl.Sequence()
                        .AddAction(MyActionIds.PlayVfx)
                        .AddAction(MyActionIds.ApplyDamage, 300.0)
                        .AddAction(MyActionIds.Heal, 100.0)
                        .Build());
        }

        /// <summary>
        /// 嵌套 Sequence
        /// </summary>
        public static ISimpleExecutable NestedSequence()
        {
            return ExecutableDsl.Sequence()
                .AddAction(MyActionIds.PlayVfx)
                .If(
                    ConditionBuilderExtensions.HasTarget(),
                    ExecutableDsl.Sequence()
                        .AddAction(MyActionIds.SpawnProjectile)
                        .AddAction(MyActionIds.ApplyDamage, 150.0)
                        .Build())
                .AddAction(MyActionIds.Heal, 25.0)
                .Build();
        }

        /// <summary>
        /// 复杂 Fluent API
        /// </summary>
        public static ISimpleExecutable ComplexFluent()
        {
            return ExecutableDsl.Sequence()
                .AddAction(MyActionIds.PlayVfx)
                .IfElse(
                    ConditionBuilderExtensions.Gt(
                        NumericValueRef.Const(10),
                        NumericValueRef.Const(5)),
                    ExecutableDsl.Sequence()
                        .AddAction(MyActionIds.ApplyDamage, 500.0)
                        .AddAction(MyActionIds.SpawnProjectile)
                        .Build(),
                    ExecutableDsl.Action(MyActionIds.ApplyDamage, 200.0))
                .AddAction(MyActionIds.Heal, 50.0)
                .Build();
        }

        /// <summary>
        /// 执行示例
        /// </summary>
        public static void ExecuteExample(object ctx)
        {
            var executable = SimpleSequence();
            var result = ExecutableExecutor.Execute(executable, ctx);

            if (result.IsSuccess)
            {
                Console.WriteLine($"Executed {result.ExecutedCount} actions");
            }
            else if (result.IsSkipped)
            {
                Console.WriteLine($"Skipped: {result.FailureReason}");
            }
            else if (result.IsFailed)
            {
                Console.WriteLine($"Failed: {result.FailureReason}");
            }
        }
    }
}
