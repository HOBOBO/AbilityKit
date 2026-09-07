using System;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMWithHierarchy - 演示分层状态机（嵌套状态机）
    /// </summary>
    [Sample]
    public sealed class HFSMWithHierarchy : SampleBase
    {
        public override string Title => "HFSM with Hierarchy";
        public override string Description => "使用分层状态机实现嵌套状态";
        public override SampleCategory Category => SampleCategory.StateMachine;

        protected override void OnRun()
        {
            Log("=== HFSM 分层状态机示例 ===");
            Output.Divider();

            // 1. 分层状态机概念
            Log("【1】分层状态机概念");
            Output.Bullet("分层状态机允许状态机嵌套在其他状态机内部");
            Output.Bullet("子状态机可以有自己的子状态和转换");
            Output.Bullet("通过退出点（Exit Transition）向上层状态机发出退出信号");
            Output.Bullet("适合实现复杂的状态逻辑，如：战斗状态（包含攻击、防御、躲避子状态）");
            Log("");

            // 2. 创建简单的分层状态机
            Log("【2】创建角色状态机（包含战斗子状态机）");
            var characterFsm = CreateCharacterFSM();
            Log("状态机结构:");
            Log("  Root: CharacterFSM");
            Log("    |- Idle (普通状态)");
            Log("    |- Move (普通状态)");
            Log("    |- Combat (嵌套状态机)");
            Log("        |- Normal (普通状态)");
            Log("        |- Attacking (普通状态)");
            Log("        |- Defending (普通状态)");
            Log("");

            // 3. 初始化
            Log("【3】初始化并进入初始状态");
            characterFsm.Init();
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log($"完整路径: {characterFsm.GetActiveHierarchyPath()}");
            Log("");

            // 4. 正常逻辑更新
            Log("【4】正常逻辑更新");
            Log("--- OnLogic() ---");
            characterFsm.OnLogic();
            Log("");

            // 5. 转换到移动
            Log("【5】转换到移动状态");
            Log("--- Trigger(\"ToMove\") ---");
            characterFsm.Trigger("ToMove");
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log("");

            // 6. 进入战斗状态
            Log("【6】进入战斗状态");
            Log("--- Trigger(\"ToCombat\") ---");
            characterFsm.Trigger("ToCombat");
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log($"完整路径: {characterFsm.GetActiveHierarchyPath()}");
            Log("");

            // 7. 在战斗子状态机内切换
            Log("【7】在战斗子状态机内切换子状态");
            Log("--- Trigger(\"ToAttack\") ---");
            characterFsm.Trigger("ToAttack");
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log($"完整路径: {characterFsm.GetActiveHierarchyPath()}");
            Log("");

            Log("--- Trigger(\"ToDefend\") ---");
            characterFsm.Trigger("ToDefend");
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log($"完整路径: {characterFsm.GetActiveHierarchyPath()}");
            Log("");

            // 8. 退出战斗状态机
            Log("【8】退出战斗状态机");
            Log("--- Trigger(\"ExitCombat\") ---");
            characterFsm.Trigger("ExitCombat");
            Log($"当前状态: {characterFsm.ActiveStateName}");
            Log($"完整路径: {characterFsm.GetActiveHierarchyPath()}");
            Log("");

            // 9. 分层状态机的优势
            Log("【9】分层状态机的优势");
            Output.Bullet("代码复用：通用逻辑可以在父状态中定义");
            Output.Bullet("状态聚合：相关状态可以组织在一起");
            Output.Bullet("易于维护：状态逻辑隔离，修改某一分支不影响其他");
            Output.Bullet("层次清晰：可以通过路径访问特定状态");

            Output.Divider();
        }

        private StateMachine<string, string, string> CreateCharacterFSM()
        {
            var fsm = new StateMachine<string, string, string>();

            // ========== 顶层状态 ==========
            // Idle 状态
            fsm.AddState("Idle", new State(
                onEnter: s => Log("[Idle] 进入空闲状态"),
                onLogic: s => Log("[Idle] 待机中...")
            ));

            // Move 状态
            fsm.AddState("Move", new State(
                onEnter: s => Log("[Move] 进入移动状态"),
                onLogic: s => Log("[Move] 移动中...")
            ));

            // ========== Combat 嵌套状态机 ==========
            var combatFsm = new StateMachine<string, string, string>(needsExitTime: true);

            // Combat 内部的子状态
            combatFsm.AddState("Normal", new State(
                onEnter: s => Log("[Combat/Normal] 进入普通战斗姿态"),
                onLogic: s => Log("[Combat/Normal] 警戒中...")
            ));

            combatFsm.AddState("Attacking", new State(
                onEnter: s => Log("[Combat/Attacking] 进入攻击姿态"),
                onLogic: s => Log("[Combat/Attacking] 攻击中..."),
                onExit: s => Log("[Combat/Attacking] 攻击结束")
            ));

            combatFsm.AddState("Defending", new State(
                onEnter: s => Log("[Combat/Defending] 进入防御姿态"),
                onLogic: s => Log("[Combat/Defending] 防御中..."),
                needsExitTime: true
            ));

            // Combat 内部转换
            combatFsm.AddTriggerTransition("ToAttack", new Transition<string>(
                from: "Normal",
                to: "Attacking",
                onTransition: t => Log("[Combat] 转换: Normal -> Attacking")
            ));

            combatFsm.AddTriggerTransition("ToDefend", new Transition<string>(
                from: "Normal",
                to: "Defending",
                onTransition: t => Log("[Combat] 转换: Normal -> Defending")
            ));

            combatFsm.AddTriggerTransition("ToNormal", new Transition<string>(
                from: "Attacking",
                to: "Normal",
                onTransition: t => Log("[Combat] 转换: Attacking -> Normal")
            ));

            combatFsm.AddTriggerTransition("ToNormal", new Transition<string>(
                from: "Defending",
                to: "Normal",
                onTransition: t => Log("[Combat] 转换: Defending -> Normal")
            ));

            combatFsm.SetStartState("Normal");

            // 将 combatFsm 作为子状态机添加到主状态机
            fsm.AddState("Combat", combatFsm);

            // ========== 顶层转换 ==========
            // Idle -> Move
            fsm.AddTriggerTransition("ToMove", new Transition<string>(
                from: "Idle",
                to: "Move",
                onTransition: t => Log("[Root] 转换: Idle -> Move")
            ));

            // Move -> Idle
            fsm.AddTriggerTransition("ToIdle", new Transition<string>(
                from: "Move",
                to: "Idle",
                onTransition: t => Log("[Root] 转换: Move -> Idle")
            ));

            // Idle -> Combat
            fsm.AddTriggerTransition("ToCombat", new Transition<string>(
                from: "Idle",
                to: "Combat",
                onTransition: t => Log("[Root] 转换: Idle -> Combat")
            ));

            // Move -> Combat
            fsm.AddTriggerTransition("ToCombat", new Transition<string>(
                from: "Move",
                to: "Combat",
                onTransition: t => Log("[Root] 转换: Move -> Combat")
            ));

            // Combat -> Idle (通过退出转换)
            fsm.AddExitTriggerTransition("ExitCombat", new Transition<string>(
                from: "Combat",
                to: "",
                forceInstantly: true,
                onTransition: t => Log("[Root] 转换: Combat -> Idle (退出战斗)")
            ));

            // 设置初始状态
            fsm.SetStartState("Idle");

            return fsm;
        }
    }
}
