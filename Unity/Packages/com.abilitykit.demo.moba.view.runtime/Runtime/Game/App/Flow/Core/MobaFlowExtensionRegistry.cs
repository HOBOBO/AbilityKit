using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed class MobaFlowExtensionRegistry
    {
        private readonly Dictionary<string, Action<GamePhaseContext, int>> _actions =
            new Dictionary<string, Action<GamePhaseContext, int>>(StringComparer.Ordinal);

        public MobaFlowExtensionRegistry Register(string actionId, Action<GamePhaseContext, int> action)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("Action id is required.", nameof(actionId));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (actionId == MobaFlowActionIds.ResetBattleSessionRuntimeState ||
                actionId == MobaFlowActionIds.ReturnLobbyAfterBattleEnd)
                throw new InvalidOperationException("Built-in flow action cannot be replaced: " + actionId);
            if (!_actions.TryAdd(actionId, action))
                throw new InvalidOperationException("Duplicate flow extension action: " + actionId);
            return this;
        }

        public bool Contains(string actionId) => _actions.ContainsKey(actionId);

        internal bool TryExecute(string actionId, GamePhaseContext context, int installedCount)
        {
            if (!_actions.TryGetValue(actionId, out var action)) return false;
            action(context, installedCount);
            return true;
        }
    }
}
