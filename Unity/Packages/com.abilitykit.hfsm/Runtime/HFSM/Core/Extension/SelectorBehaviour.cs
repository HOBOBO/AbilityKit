using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class SelectorBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
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
            for (var i = 0; i < _children.Count; i++) _children[i].Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            while (_index < _children.Count)
            {
                var status = _children[_index].Tick(in ctx);
                if (status == ActionBehaviourStatus.Running) return ActionBehaviourStatus.Running;
                if (status == ActionBehaviourStatus.Success) return ActionBehaviourStatus.Success;
                if (status == ActionBehaviourStatus.Cancelled) return ActionBehaviourStatus.Cancelled;
                _index++;
            }

            return ActionBehaviourStatus.Failure;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            if (_index < _children.Count) SequenceBehaviour.AbortChild(_children[_index], in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(SelectorBehaviour),
                integerValue: _index,
                children: SequenceBehaviour.CaptureChildren(_children, nameof(SelectorBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(SelectorBehaviour));
            SequenceBehaviour.RestoreChildren(_children, snapshot.Children, nameof(SelectorBehaviour));
            if (snapshot.IntegerValue < 0 || snapshot.IntegerValue > _children.Count)
            {
                throw new InvalidOperationException(
                    $"Selector action index '{snapshot.IntegerValue}' is outside [0, {_children.Count}].");
            }

            _index = snapshot.IntegerValue;
        }
    }
}
