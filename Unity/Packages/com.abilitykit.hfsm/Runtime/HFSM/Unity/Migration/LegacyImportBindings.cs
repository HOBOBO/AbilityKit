#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Migration
{

    /// <summary>
    /// Explicit mapping from legacy executable payloads to stable Next-runtime binding keys.
    /// Import never derives keys from CLR method or behavior type names.
    /// </summary>
    public sealed class LegacyImportBindings
    {
        private readonly Dictionary<string, string> _stateKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _conditionKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public LegacyImportBindings RegisterState(string nodeId, string behaviorKey)
        {
            Register(_stateKeys, nodeId, behaviorKey, "state");
            return this;
        }

        public LegacyImportBindings RegisterCondition(string edgeId, string conditionKey)
        {
            Register(_conditionKeys, edgeId, conditionKey, "condition");
            return this;
        }

        internal bool TryGetState(string nodeId, out string key) => _stateKeys.TryGetValue(nodeId, out key);

        internal bool TryGetCondition(string edgeId, out string key) =>
            _conditionKeys.TryGetValue(edgeId, out key);

        private static void Register(Dictionary<string, string> target, string sourceId, string key, string kind)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException($"Legacy HFSM {kind} source id is required.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"Legacy HFSM {kind} binding key is required.", nameof(key));
            if (!target.TryAdd(sourceId, key))
                throw new InvalidOperationException($"Legacy HFSM {kind} mapping '{sourceId}' is already registered.");
        }
    }
}
