using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowWakeUp
    {
        private readonly Action _wake;

        internal FlowWakeUp(Action wake)
        {
            _wake = wake ?? throw new ArgumentNullException(nameof(wake));
        }

        public void Wake()
        {
            _wake();
        }
    }
}
