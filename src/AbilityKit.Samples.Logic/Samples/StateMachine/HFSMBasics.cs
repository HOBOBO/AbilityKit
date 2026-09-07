using AbilityKit.Samples.Abstractions;
using AbilityKit.HFSM;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// 演示 AbilityKit.HFSM 如何管理角色 Idle、Casting、Dead 三个基础状态。
    /// </summary>
    [Sample(531, "hfsm", "basic", "state", "package-api", "web", "deterministic")]
    public sealed class HFSMBasics : SampleBase
    {
        public override string Title => "HFSM Basic State";
        public override string Description => "使用 StateMachine、State 和 Transition 管理角色基础状态";
        public override SampleCategory Category => SampleCategory.StateMachine;

        protected override void OnRun()
        {
            var actor = new ActorState();
            var fsm = CreateStateMachine(actor);

            Section("初始化状态机");
            fsm.Init();
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSM.InitialState", fsm.ActiveStateName);
            KeyValue("HFSM.ActiveState", fsm.ActiveStateName);

            Divider();
            Section("Idle -> Casting -> Idle");
            Step(fsm, actor, 0.25f, "等待输入");
            actor.CastRequested = true;
            KeyValue("HFSM.CastRequested", actor.CastRequested.ToString());
            Step(fsm, actor, 0.25f, "请求施法");
            Step(fsm, actor, 0.50f, "施法推进");
            Step(fsm, actor, 0.50f, "施法完成");

            Divider();
            Section("任意状态进入 Dead");
            actor.Hp = 0;
            KeyValue("HFSM.FatalHp", actor.Hp.ToString("F1"));
            Step(fsm, actor, 0.10f, "受到致命伤害");
            actor.CastRequested = true;
            Step(fsm, actor, 0.10f, "死亡后请求施法被忽略");

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("StateMachine：注册状态、转换、任意状态转换，并维护 ActiveStateName。");
            Bullet("State：承载 OnEnter、OnLogic、OnExit 生命周期回调。");
            Bullet("Transition：用业务条件表达 Idle、Casting、Dead 之间的切换规则。");
            Bullet("AddTransitionFromAny：表达死亡这类可从任意状态打断的全局门控。");
        }

        private StateMachine<string, string, string> CreateStateMachine(ActorState actor)
        {
            var fsm = new StateMachine<string, string, string>();
            fsm.StateChanged += state => KeyValue("StateChanged", state.name);

            fsm.AddState("Idle", new State(
                onEnter: _ =>
                {
                    actor.CastProgress = 0f;
                    actor.CastFinished = false;
                    Log("[Idle] ready for command");
                },
                onLogic: _ => KeyValue("Idle.Command", actor.CastRequested ? "CastRequested" : "None"),
                onExit: _ => Log("[Idle] exit")));

            fsm.AddState("Casting", new State(
                onEnter: _ =>
                {
                    actor.CastRequested = false;
                    actor.CastProgress = 0f;
                    actor.CastFinished = false;
                    Log("[Casting] begin fireball");
                },
                onLogic: _ =>
                {
                    actor.CastProgress += actor.DeltaTime;
                    KeyValue("Casting.Progress", actor.CastProgress.ToString("F2"));
                    if (actor.CastProgress >= 1f)
                    {
                        actor.CastFinished = true;
                    }
                },
                onExit: _ => Log("[Casting] exit")));

            fsm.AddState("Dead", new State(
                onEnter: _ => Log("[Dead] actor disabled"),
                onLogic: _ => KeyValue("Dead.Hp", actor.Hp.ToString("F1"))));

            fsm.AddTransition(new Transition<string>(
                from: "Idle",
                to: "Casting",
                condition: _ => actor.CastRequested && actor.Hp > 0f,
                onTransition: _ => Log("[Transition] Idle -> Casting")));

            fsm.AddTransition(new Transition<string>(
                from: "Casting",
                to: "Idle",
                condition: _ => actor.CastFinished && actor.Hp > 0f,
                onTransition: _ => Log("[Transition] Casting -> Idle")));

            fsm.AddTransitionFromAny(new Transition<string>(
                from: string.Empty,
                to: "Dead",
                condition: _ => actor.Hp <= 0f,
                onTransition: _ => Log("[Transition] Any -> Dead")));

            fsm.SetStartState("Idle");
            return fsm;
        }

        private void Step(StateMachine<string, string, string> fsm, ActorState actor, float deltaTime, string label)
        {
            actor.DeltaTime = deltaTime;
            AdvanceTime(deltaTime);
            Log($"-- {label} (+{deltaTime:F2}s) --");
            fsm.OnLogic();
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSM.ActiveState", fsm.ActiveStateName);
            KeyValue("HFSM.Step", $"{label}:time={Time:F2},state={fsm.ActiveStateName}");
        }

        private sealed class ActorState
        {
            public float Hp { get; set; } = 100f;
            public bool CastRequested { get; set; }
            public bool CastFinished { get; set; }
            public float CastProgress { get; set; }
            public float DeltaTime { get; set; }
        }
    }
}
