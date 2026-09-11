#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Action;
using AbilityKit.Samples.Logic.Ability.Samples.Action;

namespace AbilityKit.Samples.Logic.Ability.Tests
{
    /// <summary>
    /// 动作框架单元测试。
    /// </summary>
    public static class ActionTests
    {
        public static void RunAll()
        {
            TestActionRegistry();
            TestDamageAction();
            TestActionContext();
            TestActionSpec();
            Console.WriteLine("[ActionTests] All tests passed!");
        }

        private static void TestActionRegistry()
        {
            Console.WriteLine("[Test] ActionRegistry");

            var registry = new ActionRegistry();
            registry.Register(new DamageActionFactory());

            // Test GetFactoryFor
            var factory = registry.GetFactoryFor("damage");
            Assert(factory != null, "Factory should be found");

            // Test Create
            var action = registry.Create("damage", new Dictionary<string, object> { { "damage", 100 } });
            Assert(action != null, "Action should be created");
            Assert(action is DamageAction, "Action should be DamageAction");

            Console.WriteLine("  - ActionRegistry: OK");
        }

        private static void TestDamageAction()
        {
            Console.WriteLine("[Test] DamageAction");

            var executor = new SimpleActionExecutor();
            var target = new TestTarget();
            var source = new TestSource();

            var action = new DamageAction(50, "physical");
            var context = new ActionContext(executor, source, target);

            var result = action.Execute(context);
            Assert(result.Success, "Action should succeed");
            Assert(target.ReceivedDamage == 50, $"Target should receive 50 damage, got {target.ReceivedDamage}");

            Console.WriteLine("  - DamageAction: OK");
        }

        private static void TestActionContext()
        {
            Console.WriteLine("[Test] ActionContext");

            var executor = new SimpleActionExecutor();
            var context = new ActionContext(executor, new object(), new object(),
                new Dictionary<string, object> { { "key", "value" } });

            Assert(context.GetArg<string>("key") == "value", "Args should be accessible");
            Assert(context.GetArg<string>("missing", "default") == "default", "Missing arg should return default");

            context.SetData("custom", 123);
            Assert(context.GetData<int>("custom") == 123, "Custom data should be accessible");

            Console.WriteLine("  - ActionContext: OK");
        }

        private static void TestActionSpec()
        {
            Console.WriteLine("[Test] ActionSpec");

            var registry = new ActionRegistry();
            registry.Register(new DamageActionFactory());

            // Test creating action from args directly
            var action = registry.Create("damage", new Dictionary<string, object>
            {
                { "damage", 100 },
                { "damage_type", "fire" }
            });

            Assert(action is DamageAction, "Action should be DamageAction");

            Console.WriteLine("  - ActionSpec: OK");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        /// <summary>
        /// 测试用的目标实现。
        /// </summary>
        private class TestTarget : ITarget
        {
            public int ReceivedDamage { get; private set; }
            public int Hp { get; set; } = 1000;

            public int GetStat(string statName) => 0;

            public void ReceiveDamage(int damage, string damageType)
            {
                ReceivedDamage = damage;
                Hp -= damage;
            }
        }

        /// <summary>
        /// 测试用的来源实现。
        /// </summary>
        private class TestSource : ITarget
        {
            public int GetStat(string statName)
            {
                return statName == "attack" ? 10 : 0;
            }

            public void ReceiveDamage(int damage, string damageType) { }
        }
    }
}
