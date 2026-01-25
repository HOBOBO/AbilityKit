using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowCompletion
    {
        private bool _done;
        private bool _succeeded;
        private FlowWakeUp _wakeUp;

        public bool IsDone => _done;
        public bool Succeeded => _succeeded;

        public void AttachWakeUp(FlowWakeUp wakeUp)
        {
            _wakeUp = wakeUp;
        }

        public void DetachWakeUp()
        {
            _wakeUp = null;
        }

        public void Complete(bool succeeded)
        {
            if (_done) return;
            _done = true;
            _succeeded = succeeded;
            _wakeUp?.Wake();
        }

        public void Reset()
        {
            _done = false;
            _succeeded = false;
        }
    }
}
