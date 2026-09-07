using System;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMWithActions - 演示状态动作和状态回调
    /// </summary>
    [Sample]
    public sealed class HFSMWithActions : SampleBase
    {
        public override string Title => "HFSM with Actions";
        public override string Description => "状态动作、回调函数和状态机事件";
        public override SampleCategory Category => SampleCategory.StateMachine;

        private int _frameCount;

        protected override void OnRun()
        {
            Log("=== HFSM 状态动作示例 ===");
            Output.Divider();
            _frameCount = 0;

            // 1. State 构造函数参数
            Log("【1】State 构造函数参数");
            Output.Bullet("onEnter - 进入状态时的回调");
            Output.Bullet("onLogic - 每帧逻辑更新回调");
            Output.Bullet("onExit - 离开状态时的回调");
            Output.Bullet("canExit - 判断是否能退出（needsExitTime=true 时）");
            Output.Bullet("needsExitTime - 是否需要等待退出时间");
            Output.Bullet("isGhostState - 是否是幽灵状态（立即尝试转换）");
            Log("");

            // 2. 创建带动作的状态
            Log("【2】创建带动作的状态");
            var fsm = new StateMachine<string, string, string>();

            // Idle 状态
            var idleState = new State<string>(
                onEnter: s => Log("[Idle] >>> 进入 Idle 状态"),
                onLogic: s =>
                {
                    _frameCount++;
                    Log($"[Idle] 第 {_frameCount} 帧执行");
                },
                onExit: s => Log("[Idle] <<< 离开 Idle 状态")
            );

            // Attack 状态（带 needsExitTime）
            var attackState = new State<string>(
                onEnter: s => Log("[Attack] >>> 进入 Attack 状态"),
                onLogic: s => Log("[Attack] 攻击动画播放中..."),
                onExit: s => Log("[Attack] <<< 离开 Attack 状态"),
                canExit: s => s.timer.Elapsed >= 1.0f, // 1秒后才能退出
                needsExitTime: true
            );

            fsm.AddState("Idle", idleState);
            fsm.AddState("Attack", attackState);

            // 转换
            fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Attack",
                condition: t => true,
                onTransition: t => Log("[转换] Idle -> Attack")
            ));

            fsm.AddTransition(new Transition<string>(
                from: "Attack",
                to: "Idle",
                condition: t => true,
                onTransition: t => Log("[转换] Attack -> Idle")
            ));

            fsm.SetStartState("Idle");
            fsm.Init();

            Log("状态机已初始化");
            Log("");

            // 3. 运行状态机
            Log("【3】运行状态机");
            for (int i = 0; i < 5; i++)
            {
                Log($"--- 帧 {i + 1} ---");
                fsm.OnLogic();
                Log("");
            }

            // 4. Transition 回调
            Log("【4】Transition 回调函数");
            Log("onTransition - 转换发生前调用");
            Log("");

            var callbackFsm = CreateCallbackFsm();
            Log("已创建带回调的状态机");
            Log("");

            callbackFsm.OnLogic();
            callbackFsm.OnLogic();

            Output.Divider();

            // 5. StateChanged 事件
            Log("【5】StateChanged 事件");
            Log("状态机提供 StateChanged 事件，可以监听状态变化");

            var eventFsm = new StateMachine<string, string, string>();
            eventFsm.AddState("A", new State(
                onEnter: s => Log("[A] 进入状态 A")
            ));
            eventFsm.AddState("B", new State(
                onEnter: s => Log("[B] 进入状态 B")
            ));

            eventFsm.AddTransition(new Transition<string>("A", "B", t => true));
            eventFsm.SetStartState("A");

            // 订阅状态变化事件
            eventFsm.StateChanged += OnStateChanged;
            Log("已订阅 StateChanged 事件");

            eventFsm.Init();
            eventFsm.OnLogic();

            Log("");
            Log("--- 触发转换 A -> B ---");
            eventFsm.OnLogic();

            Output.Divider();
        }

        private void OnStateChanged(StateBase<string> newState)
        {
            Log($"[事件] 状态变化 -> {newState.name}");
        }

        private StateMachine<string, string, string> CreateCallbackFsm()
        {
            var fsm = new StateMachine<string, string, string>();

            fsm.AddState("State1", new State(
                onEnter: s => Log("[State1] 进入"),
                onLogic: s => Log("[State1] 执行中")
            ));

            fsm.AddState("State2", new State(
                onEnter: s => Log("[State2] 进入"),
                onLogic: s => Log("[State2] 执行中")
            ));

            // 带回调的转换
            fsm.AddTransition(new Transition<string>(
                from: "State1",
                to: "State2",
                condition: t => true,
                onTransition: t => Log(">>> onTransition: 转换开始！")
            ));

            fsm.AddTransition(new Transition<string>(
                from: "State2",
                to: "State1",
                condition: t => true,
                onTransition: t => Log(">>> onTransition: 返回 State1")
            ));

            fsm.SetStartState("State1");
            fsm.Init();

            return fsm;
        }
    }
}
