using System;
using UnityHFSM;

namespace AbilityKit.Ability.Flow
{
    public sealed class HfsmFlowRunner<TOwnId, TStateId, TEvent> : IDisposable
    {
        private readonly FlowContext _ctx;
        private readonly StateMachine<TOwnId, TStateId, TEvent> _machine;
        private readonly FlowEventQueue<TEvent> _events;

        private bool _started;

        public HfsmFlowRunner(FlowContext ctx, StateMachine<TOwnId, TStateId, TEvent> machine, FlowEventQueue<TEvent> events)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public FlowContext Context => _ctx;
        public StateMachine<TOwnId, TStateId, TEvent> Machine => _machine;
        public FlowEventQueue<TEvent> Events => _events;

        public void Start()
        {
            if (_started) return;
            _started = true;
            _machine.OnEnter();
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;
            _machine.OnExit();
            _events.Clear();
        }

        public void Step(float deltaTime)
        {
            if (!_started) return;

            while (_events.TryDequeue(out var e))
            {
                _machine.Trigger(e);
            }

            _machine.OnLogic();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
