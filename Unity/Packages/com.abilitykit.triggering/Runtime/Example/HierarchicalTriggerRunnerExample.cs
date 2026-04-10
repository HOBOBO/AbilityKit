using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Example
{
    /// <summary>
    /// 分层触发器运行器使用示例
    /// 演示如何在战斗系统中使用分层 TriggerRunner
    /// </summary>
    public static class HierarchicalTriggerRunnerExample
    {
        /// <summary>
        /// 战斗上下文（实际项目中替换为真实实现）
        /// </summary>
        public readonly struct BattleCtx
        {
            public readonly int ActorId;
            public readonly int SkillId;
            public readonly BattleServices Services;

            public BattleCtx(int actorId, int skillId, BattleServices services)
            {
                ActorId = actorId;
                SkillId = skillId;
                Services = services;
            }
        }

        /// <summary>
        /// 战斗服务（示例占位）
        /// </summary>
        public class BattleServices { }

        /// <summary>
        /// 伤害事件（示例）
        /// </summary>
        public readonly struct DamageEvent
        {
            public readonly int TargetId;
            public readonly double Damage;
            public readonly bool IsCritical;

            public DamageEvent(int targetId, double damage, bool isCritical)
            {
                TargetId = targetId;
                Damage = damage;
                IsCritical = isCritical;
            }
        }

        public static void Run()
        {
            // ========== 1. 创建共享的事件总线和注册表 ==========
            var eventBus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // ========== 2. 创建全局触发器运行器 ==========
            var globalRunner = new HierarchicalTriggerRunner<BattleCtx>(
                eventBus,
                functions,
                actions,
                contextSource: null,  // 全局上下文可能为空
                options: new HierarchicalOptions(scopeName: "Global")
            );

            // ========== 3. 在全局层级注册触发器 ==========
            var damageKey = new EventKey<DamageEvent>("damage");
            globalRunner.Register(damageKey, new DelegateTrigger<DamageEvent, BattleCtx>(
                predicate: (args, ctx) => args.Damage > 0,
                actions: (args, ctx) => UnityEngine.Debug.Log($"[Global] Damage dealt: {args.Damage}")
            ), phase: 0, priority: 0);

            // ========== 4. 创建技能层级触发器 ==========
            var skillRunner = globalRunner.CreateChild(HierarchicalOptions.SkillScope);

            // 技能层级的触发器 - 子级先执行，可以覆盖全局逻辑
            skillRunner.Register(damageKey, new DelegateTrigger<DamageEvent, BattleCtx>(
                predicate: (args, ctx) => args.IsCritical,
                actions: (args, ctx) => UnityEngine.Debug.Log($"[Skill] Critical damage: {args.Damage}")
            ), phase: 0, priority: 100);

            // ========== 5. 创建 Buff 层级触发器 ==========
            var buffRunner = globalRunner.CreateChild(HierarchicalOptions.BuffScope);

            // Buff 层级的触发器 - 父级先执行
            buffRunner.Register(damageKey, new DelegateTrigger<DamageEvent, BattleCtx>(
                predicate: (args, ctx) => args.TargetId > 0,
                actions: (args, ctx) => UnityEngine.Debug.Log($"[Buff] Damage reduced by shield")
            ), phase: 0, priority: 50);

            // ========== 6. 触发事件测试 ==========
            var ctx = new BattleCtx(1, 100, new BattleServices());
            var damageEvent = new DamageEvent(1001, 500.0, true);

            UnityEngine.Debug.Log("=== 触发伤害事件 ===");
            eventBus.Publish(damageKey, damageEvent);

            // 预期输出顺序（SkillScope: 子级先执行）:
            // [Skill] Critical damage: 500
            // [Global] Damage dealt: 500

            // 如果切换到 BuffScope（父级先执行）:
            // [Global] Damage dealt: 500
            // [Buff] Damage reduced by shield

            UnityEngine.Debug.Log($"=== 层级路径 ===");
            UnityEngine.Debug.Log($"Global:   {globalRunner.GetScopePath()}");
            UnityEngine.Debug.Log($"Skill:    {skillRunner.GetScopePath()}");
            UnityEngine.Debug.Log($"Buff:     {buffRunner.GetScopePath()}");
        }

        /// <summary>
        /// 典型战斗系统初始化示例
        /// 展示如何在 DI 容器中配置分层触发器
        /// </summary>
        public static void BattleSystemSetup()
        {
            // 这是典型的依赖注入配置示例

            // 1. 创建共享服务
            // var eventBus = new EventBus();
            // var functions = CreateBattleFunctions();
            // var actions = CreateBattleActions();

            // 2. 创建全局触发器（在 World 层级）
            // var globalRunner = new HierarchicalTriggerRunner<BattleCtx>(
            //     eventBus, functions, actions,
            //     options: new HierarchicalOptions(scopeName: "World")
            // );

            // 3. 创建子系统触发器（可选地共享全局注册表）
            // var skillRunner = globalRunner.CreateChild(HierarchicalOptions.SkillScope);
            // var buffRunner = globalRunner.CreateChild(HierarchicalOptions.BuffScope);
            // var entityRunner = globalRunner.CreateChild(new HierarchicalOptions(scopeName: "Entity"));

            // 4. 注册全局触发器
            // RegisterGlobalTriggers(globalRunner);

            // 5. 注册子系统触发器
            // RegisterSkillTriggers(skillRunner);
            // RegisterBuffTriggers(buffRunner);

            // 6. 返回全局 Runner（子系统 Runner 作为属性暴露）
            // return globalRunner;
        }

        /// <summary>
        /// 技能触发器范围（实现示例）
        /// 展示如何封装临时触发器的生命周期管理
        /// </summary>
        public class SkillTriggerScope : IDisposable
        {
            private readonly HierarchicalTriggerRunner<BattleCtx> _runner;
            private readonly System.Collections.Generic.List<IDisposable> _registrations = new();

            public SkillTriggerScope(HierarchicalTriggerRunner<BattleCtx> runner)
            {
                _runner = runner;
            }

            public void Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, BattleCtx> trigger, int phase = 0, int priority = 0)
            {
                var reg = _runner.Register(key, trigger, phase, priority);
                _registrations.Add(reg);
            }

            public void Dispose()
            {
                foreach (var reg in _registrations)
                {
                    reg.Dispose();
                }
                _registrations.Clear();
            }
        }
    }
}
