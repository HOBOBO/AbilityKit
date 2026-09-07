using System;
using System.Collections.Generic;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Extension;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMBehaviorTree - 完整行为树示例
    /// 演示如何使用行为树构建复杂的状态逻辑，类似于 Unity Node Canvas
    /// </summary>
    [Sample]
    public sealed class HFSMBehaviorTree : SampleBase
    {
        public override string Title => "HFSM Behavior Tree";
        public override string Description => "完整行为树模式：角色 AI 行为、巡逻、追逐、攻击";
        public override SampleCategory Category => SampleCategory.StateMachine;

        private int _globalTick;

        protected override void OnRun()
        {
            Log("=== HFSM 行为树示例 (Node Canvas 风格) ===");
            Output.Divider();
            _globalTick = 0;

            // 1. 行为树节点类型
            Log("【1】行为树节点类型");
            Output.Bullet("Action (原子行为) - 叶子节点，执行具体操作");
            Output.Bullet("Sequence - 顺序执行所有子节点");
            Output.Bullet("Selector - 选择第一个成功的子节点");
            Output.Bullet("Parallel - 并行执行所有子节点");
            Output.Bullet("Decorator - 装饰器，修改子节点行为");
            Log("");

            // 2. 角色 AI 状态机
            Log("【2】角色 AI 状态机示例");
            Log("场景: 敌人 AI 具有 Idle、Patrol、Chase、Attack 状态");
            Log("");

            var aiFsm = CreateEnemyAI();
            Log("AI 状态机已创建");
            Log("");

            // 模拟运行
            Log("--- 模拟 AI 运行 ---");

            // 初始状态
            Log($"\n[帧 {_globalTick}] 当前状态: Idle");
            aiFsm.OnLogic();
            _globalTick++;

            // 模拟发现目标
            Log($"\n[帧 {_globalTick}] 检测到目标!");
            aiFsm.Trigger("OnTargetDetected");

            for (int i = 0; i < 3; i++)
            {
                Log($"\n[帧 {_globalTick}] 当前状态: Chase");
                aiFsm.OnLogic();
                _globalTick++;
            }

            // 进入攻击范围
            Log($"\n[帧 {_globalTick}] 进入攻击范围!");
            aiFsm.Trigger("OnInAttackRange");

            for (int i = 0; i < 5; i++)
            {
                Log($"\n[帧 {_globalTick}] 当前状态: Attack");
                aiFsm.OnLogic();
                _globalTick++;
            }

            // 目标逃离
            Log($"\n[帧 {_globalTick}] 目标逃离!");
            aiFsm.Trigger("OnTargetLost");

            for (int i = 0; i < 3; i++)
            {
                Log($"\n[帧 {_globalTick}] 当前状态: Patrol");
                aiFsm.OnLogic();
                _globalTick++;
            }

            Output.Divider();

            // 3. 技能释放行为树
            Log("【3】技能释放行为树示例");
            Log("场景: 技能施放序列 - 吟唱 -> 引导 -> 释放");
            Log("");

            _globalTick = 0;
            var skillFsm = CreateSkillBehaviorTree();
            Log("技能行为树已创建");

            for (int i = 0; i < 8; i++)
            {
                skillFsm.OnLogic();
                _globalTick++;
            }

            Output.Divider();

            Log("【4】总结");
            Output.Bullet("行为树 = 状态逻辑的可视化组合");
            Output.Bullet("原子行为 (Action) 是叶子节点");
            Output.Bullet("复合行为 (Sequence/Selector/Parallel) 是组合节点");
            Output.Bullet("装饰器 (Decorator) 包装现有行为");
            Output.Bullet("与 HFSM 结合 = 状态 + 行为逻辑");
        }

        #region 示例 1: 敌人 AI 行为树

        private StateMachine<string, string> CreateEnemyAI()
        {
            var fsm = new StateMachine<string, string>(needsExitTime: true);

            // 模拟数据
            var hasTarget = false;
            var inAttackRange = false;
            var health = 100f;
            var patrolPoint = 0;

            // ===== Idle 状态 =====
            var idleState = new CompositeActionState<string, string>(needsExitTime: true);
            idleState.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() => Log("  [Idle] 待机观察...")))
                    .Add(new DelayBehaviour(0.2f))
                    .Add(new CallbackBehaviour(() =>
                    {
                        var randomChance = new Random().NextDouble();
                        Log($"  [检测] 检查周围环境... 随机值: {randomChance:F2}");
                        if (randomChance > 0.7)
                        {
                            Log("  [检测] 发现敌人!");
                        }
                    }))
                    .Add(new DelayBehaviour(0.1f))
            );
            idleState.SetLoop(true);

            // ===== Patrol 状态 =====
            var patrolState = new CompositeActionState<string, string>(needsExitTime: true);
            patrolState.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() =>
                    {
                        patrolPoint = (patrolPoint + 1) % 4;
                        Log($"  [Patrol] 前往巡逻点 {patrolPoint}");
                    }))
                    .Add(new DelayBehaviour(0.1f))
                    .Add(new CallbackBehaviour(() => Log("  [Patrol] 到达巡逻点，观察...")))
                    .Add(new DelayBehaviour(0.1f))
            );
            patrolState.SetLoop(true);

            // ===== Chase 状态 =====
            var chaseState = new CompositeActionState<string, string>(needsExitTime: true);
            chaseState.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() => Log("  [Chase] 发现目标! 追击...")))
                    .Add(new ParallelBehaviour()
                        // 追逐行为
                        .Add(new SequenceBehaviour()
                            .Add(new CallbackBehaviour(() => Log("  [Chase] 移动到目标位置")))
                            .Add(new DelayBehaviour(0.2f))
                        )
                        // 检测行为
                        .Add(new SequenceBehaviour()
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() =>
                            {
                                if (!hasTarget)
                                    Log("  [Chase] 目标已逃离!");
                                else
                                    Log("  [Chase] 继续追踪目标...");
                            }))
                        )
                    )
            );
            chaseState.SetLoop(true);

            // ===== Attack 状态 =====
            var attackState = new CompositeActionState<string, string>(needsExitTime: true);
            attackState.SetRoot(
                new SequenceBehaviour()
                    .Add(new CallbackBehaviour(() => Log("  [Attack] 进入攻击范围!")))
                    .Add(new ParallelBehaviour()
                        // 攻击行为
                        .Add(new SequenceBehaviour()
                            .Add(new CallbackBehaviour(() => Log("  [Attack] 施放技能 A")))
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() =>
                            {
                                health -= 15f;
                                Log($"  [Attack] 造成伤害! 目标生命值: {health}");
                            }))
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() => Log("  [Attack] 施放技能 B")))
                            .Add(new DelayBehaviour(0.1f))
                        )
                        // 冷却检测
                        .Add(new SequenceBehaviour()
                            .Add(new DelayBehaviour(0.2f))
                            .Add(new CallbackBehaviour(() => Log("  [Attack] 技能冷却中...")))
                        )
                    )
            );
            attackState.SetLoop(true);

            fsm.AddState("Idle", idleState);
            fsm.AddState("Patrol", patrolState);
            fsm.AddState("Chase", chaseState);
            fsm.AddState("Attack", attackState);

            // 添加转换
            fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Patrol",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Patrol",
                to: "Idle",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Chase",
                condition: t => hasTarget,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Patrol",
                to: "Chase",
                condition: t => hasTarget,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Chase",
                to: "Attack",
                condition: t => inAttackRange,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Chase",
                to: "Patrol",
                condition: t => !hasTarget,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Attack",
                to: "Chase",
                condition: t => !inAttackRange,
                onTransition: null
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Attack",
                to: "Patrol",
                condition: t => !hasTarget,
                onTransition: null
            ));

            // 设置触发器转换
            fsm.AddTriggerTransition("OnTargetDetected", new Transition<string>(
                from: "Idle",
                to: "Chase",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTriggerTransition("OnTargetDetected", new Transition<string>(
                from: "Patrol",
                to: "Chase",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTriggerTransition("OnInAttackRange", new Transition<string>(
                from: "Chase",
                to: "Attack",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTriggerTransition("OnTargetLost", new Transition<string>(
                from: "Chase",
                to: "Patrol",
                condition: t => true,
                onTransition: null
            ));

            fsm.AddTriggerTransition("OnTargetLost", new Transition<string>(
                from: "Attack",
                to: "Patrol",
                condition: t => true,
                onTransition: null
            ));

            fsm.SetStartState("Idle");
            fsm.Init();

            return fsm;
        }

        #endregion

        #region 示例 2: 技能释放行为树

        private StateMachine<string> CreateSkillBehaviorTree()
        {
            var fsm = new StateMachine<string>(needsExitTime: true);

            var channelTime = 0f;
            var castTime = 0f;
            var damage = 0f;
            var cooldown = 3f;

            var skillState = new CompositeActionState<string, string>(needsExitTime: true);
            skillState.SetRoot(
                new SequenceBehaviour()
                    // 第一阶段: 吟唱
                    .Add(new SequenceBehaviour()
                        .Add(new CallbackBehaviour(() =>
                        {
                            channelTime = 0f;
                            Log("  [技能] 开始吟唱...");
                        }))
                        .Add(new LoopBehaviour(3, new SequenceBehaviour()
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() =>
                            {
                                channelTime += 0.1f;
                                Log($"  [技能] 吟唱中... {channelTime:F1}s / 0.3s");
                            }))
                        ))
                        .Add(new CallbackBehaviour(() => Log("  [技能] 吟唱完成!")))
                    )
                    // 第二阶段: 引导
                    .Add(new SequenceBehaviour()
                        .Add(new CallbackBehaviour(() =>
                        {
                            castTime = 0f;
                            Log("  [技能] 开始引导...");
                        }))
                        .Add(new LoopBehaviour(2, new SequenceBehaviour()
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() =>
                            {
                                castTime += 0.1f;
                                Log($"  [技能] 引导中... {castTime:F1}s / 0.2s");
                            }))
                        ))
                        .Add(new CallbackBehaviour(() => Log("  [技能] 引导完成!")))
                    )
                    // 第三阶段: 释放
                    .Add(new SequenceBehaviour()
                        .Add(new CallbackBehaviour(() =>
                        {
                            damage = 100f;
                            Log($"  [技能] 释放技能! 造成 {damage} 点伤害!");
                        }))
                        .Add(new DelayBehaviour(0.05f))
                        .Add(new CallbackBehaviour(() => Log("  [技能] 技能释放完成")))
                    )
                    // 第四阶段: 冷却
                    .Add(new SequenceBehaviour()
                        .Add(new CallbackBehaviour(() =>
                        {
                            cooldown = 0.3f;
                            Log($"  [技能] 进入 {cooldown:F1}s 冷却...");
                        }))
                        .Add(new LoopBehaviour(3, new SequenceBehaviour()
                            .Add(new DelayBehaviour(0.1f))
                            .Add(new CallbackBehaviour(() =>
                            {
                                cooldown -= 0.1f;
                                Log($"  [技能] 冷却中... {cooldown:F1}s 剩余");
                            }))
                        ))
                        .Add(new CallbackBehaviour(() => Log("  [技能] 冷却完成! 可以再次施放")))
                    )
            );

            skillState.SetLoop(true);

            fsm.AddState("CastSkill", skillState);
            fsm.SetStartState("CastSkill");
            fsm.Init();

            return fsm;
        }

        #endregion
    }

    /// <summary>
    /// 选择器行为 - 依次执行子节点，返回第一个成功的结果
    /// 如果所有子节点都失败，则返回失败
    /// </summary>
    public sealed class SelectorBehaviour : IActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();
        private int _index;

        public SelectorBehaviour Add(IActionBehaviour child)
        {
            if (child != null) _children.Add(child);
            return this;
        }

        public void Reset()
        {
            _index = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Reset();
            }
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            while (_index < _children.Count)
            {
                var status = _children[_index].Tick(ctx);

                if (status == ActionBehaviourStatus.Running)
                    return ActionBehaviourStatus.Running;

                if (status == ActionBehaviourStatus.Success)
                {
                    _index = 0;
                    return ActionBehaviourStatus.Success;
                }

                _index++;
            }

            _index = 0;
            return ActionBehaviourStatus.Failure;
        }
    }
}
