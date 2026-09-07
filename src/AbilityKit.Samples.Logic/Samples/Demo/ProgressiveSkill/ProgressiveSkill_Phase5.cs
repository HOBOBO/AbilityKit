using System;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase5 - HFSM 分层状态机 (框架提供)
    ///
    /// 需求: 角色行为状态：Idle → Combat → Dead，支持状态转换。
    ///
    /// 框架能力: com.abilitykit.hfsm 的 StateMachine (AbilityKit.HFSM)。
    /// 从 Phase5_HFSM.json 加载配置数据。
    /// 展示如何用 HFSM 管理角色行为状态，以及如何与 Pipeline/Triggering 协作。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase5 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase5";
        public override string Description => "HFSM - 分层状态机 (框架)";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase5: HFSM 分层状态机 (框架)  ===");
            Log("================================================================================");
            Output.Divider();

            // 从 JSON 配置加载
            var config = Phase5Config.Load();
            Log("【1】从 Phase5_HFSM.json 加载配置");
            Log($"  初始状态: {config.InitialState}");
            Log($"  状态数量: {config.States.Count}");
            Log($"  转换数量: {config.Transitions.Count}");
            Log("");

            // 构建状态机
            Log("【2】构建状态机 (框架 AbilityKit.HFSM)");
            var fsm = new AbilityKit.HFSM.StateMachine();
            BuildStates(fsm, Log);
            fsm.Init();

            Log($"  状态机已初始化");
            Log($"  初始状态: {fsm.ActiveStateName}");
            Log("");

            // 模拟运行
            Log("【3】模拟状态机运行");
            Output.Line();

            Log("  --- 帧 1: Idle 状态 ---");
            fsm.OnLogic();
            Log($"    当前状态: {fsm.ActiveStateName}");
            Log("");

            // 进入战斗
            Log("  --- 触发 EnterCombat ---");
            Log("");
            fsm.Trigger("EnterCombat");
            Log("");

            Log("  --- 帧 2: Combat 状态 ---");
            fsm.OnLogic();
            Log($"    当前状态: {fsm.ActiveStateName}");
            Log("");

            // 攻击
            Log("  --- 触发 Attack ---");
            Log("");
            fsm.Trigger("Attack");
            Log("");

            Log("  --- 帧 3: Combat/Attack 状态 ---");
            fsm.OnLogic();
            Log($"    当前状态: {fsm.ActiveStateName}");
            Log("");

            // 返回 Combat
            Log("  --- 触发 FinishAttack ---");
            Log("");
            fsm.Trigger("FinishAttack");
            Log("");

            Log("  --- 帧 4: 返回 Combat 状态 ---");
            fsm.OnLogic();
            Log($"    当前状态: {fsm.ActiveStateName}");
            Log("");

            // 死亡
            Log("  --- 触发 Die ---");
            Log("");
            fsm.Trigger("Die");
            Log("");

            Log("  --- 帧 5: Dead 状态 ---");
            fsm.OnLogic();
            Log($"    当前状态: {fsm.ActiveStateName}");
            Log("");

            // 重置并重新运行
            Log("【4】重置状态机");
            fsm = new AbilityKit.HFSM.StateMachine();
            BuildStates(fsm, Log);
            fsm.Init();
            Log($"  已重置，初始状态: {fsm.ActiveStateName}");
            Log("");

            // 架构说明
            Log("【5】完整系统架构 (Phase5 总结)");
            Output.Line();
            Log("  ┌─────────────────────────────────────────────────────────┐");
            Log("  │                    完整技能系统架构                       │");
            Log("  ├─────────────────────────────────────────────────────────┤");
            Log("  │                                                         │");
            Log("  │   ┌─────────┐                                          │");
            Log("  │   │   HFSM  │  角色行为状态 (Idle/Combat/Dead)         │");
            Log("  │   └────┬────┘                                          │");
            Log("  │        │                                               │");
            Log("  │        ↓                                               │");
            Log("  │   ┌─────────┐                                          │");
            Log("  │   │ Pipeline │  执行技能流程                              │");
            Log("  │   └────┬────┘                                          │");
            Log("  │        │ 派发事件                                        │");
            Log("  │        ↓                                                │");
            Log("  │   ┌─────────┐                                          │");
            Log("  │   │Triggering│  响应战斗事件                            │");
            Log("  │   └────┬────┘                                          │");
            Log("  │        │ 添加 Buff                                       │");
            Log("  │        ↓                                                │");
            Log("  │   ┌─────────────┐                                       │");
            Log("  │   │ Continuous │  管理 Buff 生命周期                     │");
            Log("  │   └─────────────┘                                       │");
            Log("  │                                                         │");
            Log("  └─────────────────────────────────────────────────────────┘");
            Log("");

            // 各模块职责总结
            Log("【6】各模块职责总结 (完整)");
            Output.Bullet("HFSM: 角色行为状态 (Idle/Combat/Dead/Attack)");
            Output.Bullet("Pipeline: 技能执行流程 (验证/消耗/施法/效果/冷却)");
            Output.Bullet("Triggering: 战斗事件响应 (受伤/击杀/Buff触发)");
            Output.Bullet("Continuous: Buff/DOT/HOT 生命周期管理");
            Output.Bullet("Flow: 复杂技能流程编排 (引导/并行/分支)");
            Log("");

            Output.Divider();
            Log("【总结】Phase5 完成了从基础到完整的演进");
            Log("       HFSM + Pipeline + Triggering + Continuous 组合成完整技能系统");
            Log("       每个模块各司其职，通过事件和接口组合在一起");
            Output.Divider();
        }

        private void BuildStates(AbilityKit.HFSM.StateMachine fsm, Action<string> log)
        {
            // Idle 状态
            fsm.AddState("Idle", new State(
                onEnter: state => log("    [Idle] 进入 Idle 状态"),
                onLogic: state => log("    [Idle] 等待中...")
            ));

            // Combat 状态
            fsm.AddState("Combat", new State(
                onEnter: state => log("    [Combat] 进入 Combat 状态"),
                onLogic: state => log("    [Combat] 战斗中...")
            ));

            // Combat.Attack 子状态
            fsm.AddState("Combat/Attack", new State(
                onEnter: state => log("    [Combat/Attack] 进入 Attack 状态"),
                onLogic: state => log("    [Combat/Attack] 攻击中...")
            ));

            // Dead 状态
            fsm.AddState("Dead", new State(
                onEnter: state => log("    [Dead] 进入 Dead 状态")
            ));

            // 设置初始状态
            fsm.SetStartState("Idle");

            // 添加条件转换
            fsm.AddTransition(new Transition(
                from: "Idle",
                to: "Combat",
                condition: t => { log("    [转换] Idle -> Combat (条件通过)"); return true; }
            ));

            fsm.AddTransition(new Transition(
                from: "Combat",
                to: "Idle",
                condition: t => { log("    [转换] Combat -> Idle (条件通过)"); return true; }
            ));

            fsm.AddTransition(new Transition(
                from: "Combat",
                to: "Combat/Attack",
                condition: t => { log("    [转换] Combat -> Combat/Attack (条件通过)"); return true; }
            ));

            fsm.AddTransition(new Transition(
                from: "Combat/Attack",
                to: "Combat",
                condition: t => { log("    [转换] Combat/Attack -> Combat (条件通过)"); return true; }
            ));

            // 添加触发转换
            fsm.AddTriggerTransition("EnterCombat", new Transition("Idle", "Combat"));
            fsm.AddTriggerTransition("Attack", new Transition("Combat", "Combat/Attack"));
            fsm.AddTriggerTransition("FinishAttack", new Transition("Combat/Attack", "Combat"));
            fsm.AddTriggerTransition("Die", new Transition("Idle", "Dead"));
            fsm.AddTriggerTransition("Die", new Transition("Combat", "Dead"));
        }
    }

    // ============================================================================
    // 配置模型
    // ============================================================================

    /// <summary>
    /// Phase5 配置 (从 JSON 加载)
    /// </summary>
    public sealed class Phase5Config
    {
        public string Name { get; set; }
        public string InitialState { get; set; }
        public string Description { get; set; }

        public System.Collections.Generic.List<Phase5StateConfig> States { get; set; } = new();
        public System.Collections.Generic.List<Phase5TransitionConfig> Transitions { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, Phase5ParameterConfig> Parameters { get; set; } = new();

        public static Phase5Config Load()
        {
            const string json = @"{
                ""name"": ""CharacterFSM"",
                ""initialState"": ""Idle"",
                ""description"": ""角色状态机配置示例"",
                ""states"": [
                    { ""id"": ""Idle"", ""type"": ""State"", ""description"": ""空闲状态"" },
                    { ""id"": ""Combat"", ""type"": ""State"", ""description"": ""战斗状态"" },
                    { ""id"": ""Combat/Attack"", ""type"": ""State"", ""description"": ""攻击子状态"" },
                    { ""id"": ""Dead"", ""type"": ""State"", ""description"": ""死亡状态"" }
                ],
                ""transitions"": [
                    { ""from"": ""Idle"", ""to"": ""Combat"", ""trigger"": ""EnterCombat"" },
                    { ""from"": ""Combat"", ""to"": ""Idle"", ""trigger"": ""ExitCombat"" },
                    { ""from"": ""Combat"", ""to"": ""Combat/Attack"", ""trigger"": ""Attack"" },
                    { ""from"": ""Combat/Attack"", ""to"": ""Combat"", ""trigger"": ""FinishAttack"" },
                    { ""from"": ""*"", ""to"": ""Dead"", ""trigger"": ""Die"" }
                ],
                ""parameters"": {
                    ""health"": { ""type"": ""Float"", ""defaultValue"": 100.0 },
                    ""isInCombat"": { ""type"": ""Bool"", ""defaultValue"": false }
                }
            }";
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Phase5Config>(json);
        }
    }

    public sealed class Phase5StateConfig
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool NeedsExitTime { get; set; }
    }

    public sealed class Phase5TransitionConfig
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Trigger { get; set; }
    }

    public sealed class Phase5ParameterConfig
    {
        public string Type { get; set; }
        public object DefaultValue { get; set; }
    }
}
