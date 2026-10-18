using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudInputEventSubscriptionList
    {
        private readonly List<Action> _unbinders = new List<Action>(8);

        public void Add(Action bind, Action unbind)
        {
            bind?.Invoke();
            if (unbind != null)
            {
                _unbinders.Add(unbind);
            }
        }

        public void Clear()
        {
            for (int i = _unbinders.Count - 1; i >= 0; i--)
            {
                _unbinders[i]?.Invoke();
            }

            _unbinders.Clear();
        }
    }
}
