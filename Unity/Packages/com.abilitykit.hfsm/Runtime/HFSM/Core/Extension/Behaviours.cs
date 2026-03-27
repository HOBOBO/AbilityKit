using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public sealed class CallbackBehaviour : IActionBehaviour
    {
        private readonly Action _action;
        private bool _done;

        public CallbackBehaviour(Action action)
        {
            _action = action;
        }

        public void Reset()
        {
            _done = false;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_done) return ActionBehaviourStatus.Success;
            _done = true;
            _action?.Invoke();
            return ActionBehaviourStatus.Success;
        }
    }

    public sealed class DelayBehaviour : IActionBehaviour
    {
        private readonly float _duration;
        private readonly bool _useUnscaled;
        private float _elapsed;

        public DelayBehaviour(float duration, bool useUnscaled = false)
        {
            _duration = duration;
            _useUnscaled = useUnscaled;
        }

        public void Reset()
        {
            _elapsed = 0f;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            _elapsed += ctx.GetScaledDelta(_useUnscaled);
            return _elapsed >= _duration ? ActionBehaviourStatus.Success : ActionBehaviourStatus.Running;
        }
    }

    public sealed class SequenceBehaviour : IActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();
        private int _index;

        public SequenceBehaviour Add(IActionBehaviour child)
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
                var s = _children[_index].Tick(ctx);
                if (s == ActionBehaviourStatus.Running) return ActionBehaviourStatus.Running;
                if (s == ActionBehaviourStatus.Failure) return ActionBehaviourStatus.Failure;
                _index++;
            }

            return ActionBehaviourStatus.Success;
        }
    }

    public sealed class ParallelBehaviour : IActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();

        public ParallelBehaviour Add(IActionBehaviour child)
        {
            if (child != null) _children.Add(child);
            return this;
        }

        public void Reset()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Reset();
            }
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            var anyRunning = false;
            for (int i = 0; i < _children.Count; i++)
            {
                var s = _children[i].Tick(ctx);
                if (s == ActionBehaviourStatus.Failure) return ActionBehaviourStatus.Failure;
                if (s == ActionBehaviourStatus.Running) anyRunning = true;
            }

            return anyRunning ? ActionBehaviourStatus.Running : ActionBehaviourStatus.Success;
        }
    }
}
