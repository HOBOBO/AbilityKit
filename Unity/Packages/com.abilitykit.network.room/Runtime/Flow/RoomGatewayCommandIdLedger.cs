#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Room
{
    /// <summary>Keeps one command ID for every pending logical room operation across retries.</summary>
    public sealed class RoomGatewayCommandIdLedger
    {
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>(StringComparer.Ordinal);

        public string GetOrCreate(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
                throw new ArgumentException("Operation key is required.", nameof(operationKey));
            if (!_pending.TryGetValue(operationKey, out var commandId))
            {
                commandId = Guid.NewGuid().ToString("N");
                _pending.Add(operationKey, commandId);
            }
            return commandId;
        }

        public void Complete(string operationKey) => _pending.Remove(operationKey);

        public void Clear() => _pending.Clear();
    }
}
