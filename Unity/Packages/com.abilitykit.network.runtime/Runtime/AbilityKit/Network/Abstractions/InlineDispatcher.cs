using System;

namespace AbilityKit.Network.Abstractions
{
    public sealed class InlineDispatcher : IDispatcher
    {
        public static readonly InlineDispatcher Instance = new InlineDispatcher();

        private InlineDispatcher()
        {
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            action.Invoke();
        }
    }
}
