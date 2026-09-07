using AbilityKit.Core.Eventing;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Triggering.Eventing;
using AbilityKit.HFSM;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// 演示 Triggering 事件如何桥接到 AbilityKit.HFSM 的 trigger transition。
    /// </summary>
    [Sample(532, "hfsm", "trigger", "bridge", "package-api", "web", "deterministic")]
    public sealed class HFSMWithTriggers : SampleBase
    {
        public override string Title => "HFSM Trigger Bridge";
        public override string Description => "使用 EventBus 把战斗事件桥接到 HFSM trigger transition";
        public override SampleCategory Category => SampleCategory.StateMachine;

        protected override void OnRun()
        {
            var actor = new ActorState();
            var eventBus = new EventBus();
            var fsm = CreateStateMachine(actor);
            var damageKey = new EventKey<DamageEvent>("damage-taken");
            var commandKey = new EventKey<CommandEvent>("command-issued");

            using var bridge = new HfsmEventBridge(eventBus, fsm, actor, damageKey, commandKey, Log, KeyValue);

            Section("初始化状态机与事件桥");
            fsm.Init();
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSMBridge.InitialState", fsm.ActiveStateName);
            Bullet("业务只发布事件；桥接层把事件翻译为 HFSM trigger。 ");

            Divider();
            Section("CommandEvent -> Attack trigger");
            PublishCommand(eventBus, commandKey, "Attack");
            Step(fsm, actor, 0.25f, "攻击帧");
            fsm.Trigger("FinishAttack");
            KeyValue("HFSMBridge.Trigger", "FinishAttack");
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSMBridge.Combo", actor.Combo.ToString());

            Divider();
            Section("DamageEvent -> Stagger trigger");
            PublishDamage(eventBus, damageKey, amount: 30f, source: "orc");
            Step(fsm, actor, 0.25f, "硬直帧");
            fsm.Trigger("Recover");
            KeyValue("HFSMBridge.Trigger", "Recover");
            KeyValue("ActiveState", fsm.ActiveStateName);

            Divider();
            Section("Fatal Damage -> Dead trigger");
            PublishDamage(eventBus, damageKey, amount: 80f, source: "boss");
            Step(fsm, actor, 0.10f, "死亡帧");
            PublishCommand(eventBus, commandKey, "Attack");
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSMBridge.FinalState", fsm.ActiveStateName);

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("EventBus：由业务系统发布 CommandEvent 与 DamageEvent。 ");
            Bullet("StateMachine.Trigger：桥接层把事件转换为 Attack、Stagger、Dead 等状态机 trigger。 ");
            Bullet("AddTriggerTransition：只在指定 trigger 触发时检查对应状态转换。 ");
            Bullet("State 生命周期：在进入 Attack、Stagger、Dead 时执行确定性业务回调。 ");
        }

        private StateMachine<string, string, string> CreateStateMachine(ActorState actor)
        {
            var fsm = new StateMachine<string, string, string>();
            fsm.StateChanged += state => KeyValue("StateChanged", state.name);

            fsm.AddState("Idle", new State(
                onEnter: _ => Log("[Idle] wait for events"),
                onLogic: _ => KeyValue("Idle.Hp", actor.Hp.ToString("F1"))));

            fsm.AddState("Attack", new State(
                onEnter: _ =>
                {
                    actor.Combo += 1;
                    Log($"[Attack] combo={actor.Combo}");
                },
                onLogic: _ => Log("[Attack] resolve hit window"),
                onExit: _ => Log("[Attack] exit")));

            fsm.AddState("Stagger", new State(
                onEnter: _ => Log($"[Stagger] interrupted by {actor.LastDamageSource}"),
                onLogic: _ => KeyValue("Stagger.Hp", actor.Hp.ToString("F1")),
                onExit: _ => Log("[Stagger] recovered")));

            fsm.AddState("Dead", new State(
                onEnter: _ => Log("[Dead] stop combat logic"),
                onLogic: _ => KeyValue("Dead.Hp", actor.Hp.ToString("F1"))));

            fsm.AddTriggerTransition("Attack", new Transition<string>(
                from: "Idle",
                to: "Attack",
                condition: _ => actor.Hp > 0f,
                onTransition: _ => Log("[Trigger] Idle -> Attack")));

            fsm.AddTriggerTransition("FinishAttack", new Transition<string>(
                from: "Attack",
                to: "Idle",
                onTransition: _ => Log("[Trigger] Attack -> Idle")));

            fsm.AddTriggerTransition("Stagger", new Transition<string>(
                from: "Idle",
                to: "Stagger",
                condition: _ => actor.Hp > 0f,
                onTransition: _ => Log("[Trigger] Idle -> Stagger")));

            fsm.AddTriggerTransition("Stagger", new Transition<string>(
                from: "Attack",
                to: "Stagger",
                condition: _ => actor.Hp > 0f,
                onTransition: _ => Log("[Trigger] Attack -> Stagger")));

            fsm.AddTriggerTransition("Recover", new Transition<string>(
                from: "Stagger",
                to: "Idle",
                condition: _ => actor.Hp > 0f,
                onTransition: _ => Log("[Trigger] Stagger -> Idle")));

            fsm.AddTriggerTransitionFromAny("Dead", new Transition<string>(
                from: string.Empty,
                to: "Dead",
                condition: _ => actor.Hp <= 0f,
                onTransition: _ => Log("[Trigger] Any -> Dead")));

            fsm.SetStartState("Idle");
            return fsm;
        }

        private void PublishCommand(EventBus eventBus, EventKey<CommandEvent> key, string command)
        {
            Log($"Publish CommandEvent({command})");
            KeyValue("HFSMBridge.Command", command);
            eventBus.Publish(key, new CommandEvent(command));
        }

        private void PublishDamage(EventBus eventBus, EventKey<DamageEvent> key, float amount, string source)
        {
            Log($"Publish DamageEvent({amount:F1}, {source})");
            KeyValue("HFSMBridge.Damage", $"amount={amount:F1},source={source}");
            eventBus.Publish(key, new DamageEvent(amount, source));
        }

        private void Step(StateMachine<string, string, string> fsm, ActorState actor, float deltaTime, string label)
        {
            actor.DeltaTime = deltaTime;
            AdvanceTime(deltaTime);
            Log($"-- {label} (+{deltaTime:F2}s) --");
            fsm.OnLogic();
            KeyValue("ActiveState", fsm.ActiveStateName);
            KeyValue("HFSMBridge.FinalState", fsm.ActiveStateName);
        }

        private readonly struct CommandEvent
        {
            public CommandEvent(string command)
            {
                Command = command;
            }

            public string Command { get; }
        }

        private readonly struct DamageEvent
        {
            public DamageEvent(float amount, string source)
            {
                Amount = amount;
                Source = source;
            }

            public float Amount { get; }
            public string Source { get; }
        }

        private sealed class ActorState
        {
            public float Hp { get; set; } = 100f;
            public int Combo { get; set; }
            public string LastDamageSource { get; set; } = "none";
            public float DeltaTime { get; set; }
        }

        private sealed class HfsmEventBridge : System.IDisposable
        {
            private readonly System.IDisposable _damageSubscription;
            private readonly System.IDisposable _commandSubscription;
            private readonly StateMachine<string, string, string> _fsm;
            private readonly ActorState _actor;
            private readonly System.Action<string> _log;

            private readonly System.Action<string, string> _keyValue;

            public HfsmEventBridge(
                EventBus eventBus,
                StateMachine<string, string, string> fsm,
                ActorState actor,
                EventKey<DamageEvent> damageKey,
                EventKey<CommandEvent> commandKey,
                System.Action<string> log,
                System.Action<string, string> keyValue)
            {
                _fsm = fsm;
                _actor = actor;
                _log = log;
                _keyValue = keyValue;
                _damageSubscription = eventBus.Subscribe<DamageEvent>(damageKey, OnDamage);
                _commandSubscription = eventBus.Subscribe<CommandEvent>(commandKey, OnCommand);
            }

            public void Dispose()
            {
                _damageSubscription.Dispose();
                _commandSubscription.Dispose();
            }

            private void OnCommand(CommandEvent args)
            {
                _log($"Bridge CommandEvent -> {args.Command} trigger");
                _keyValue("HFSMBridge.Trigger", args.Command);
                _fsm.Trigger(args.Command);
            }

            private void OnDamage(DamageEvent args)
            {
                _actor.Hp -= args.Amount;
                _actor.LastDamageSource = args.Source;
                var trigger = _actor.Hp <= 0f ? "Dead" : "Stagger";
                _log($"Bridge DamageEvent -> hp={_actor.Hp:F1}");
                _keyValue("HFSMBridge.Trigger", trigger);
                _fsm.Trigger(trigger);
            }
        }
    }
}
