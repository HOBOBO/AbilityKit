#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationBlackboardRow")]
    public sealed class ObservationBlackboardRow
    {
        public string Key { get; }
        public ValueType Type { get; }
        public string CurrentValue { get; }
        public string PreviousValue { get; }
        public bool HasCurrentValue { get; }
        public bool HasPreviousValue { get; }
        public bool IsChanged { get; }
        public bool IsAdded { get; }
        public bool IsRemoved { get; }

        internal ObservationBlackboardRow(
            string key,
            ValueType type,
            string currentValue,
            string previousValue,
            bool hasCurrentValue,
            bool hasPreviousValue,
            bool isChanged,
            bool isAdded,
            bool isRemoved)
        {
            Key = key ?? "";
            Type = type;
            CurrentValue = currentValue ?? "";
            PreviousValue = previousValue ?? "";
            HasCurrentValue = hasCurrentValue;
            HasPreviousValue = hasPreviousValue;
            IsChanged = isChanged;
            IsAdded = isAdded;
            IsRemoved = isRemoved;
        }
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationBlackboardView")]
    public sealed class ObservationBlackboardView
    {
        private static readonly ObservationBlackboardView EmptyInstance =
            new ObservationBlackboardView(Array.Empty<ObservationBlackboardRow>());

        private readonly ObservationBlackboardRow[] _rows;
        private readonly Dictionary<string, ObservationBlackboardRow> _rowsByKey;

        public IReadOnlyList<ObservationBlackboardRow> Rows => _rows;
        public int Count => _rows.Length;

        private ObservationBlackboardView(ObservationBlackboardRow[] rows)
        {
            _rows = rows;
            _rowsByKey = new Dictionary<string, ObservationBlackboardRow>(rows.Length, StringComparer.Ordinal);
            for (var i = 0; i < rows.Length; i++) _rowsByKey[rows[i].Key] = rows[i];
        }

        public static ObservationBlackboardView Empty => EmptyInstance;

        public static ObservationBlackboardView Create(
            ObservationSnapshot? current,
            ObservationSnapshot? previous = null,
            ObservationDiff? diff = null)
        {
            var currentBlackboard = current?.Blackboard;
            var previousBlackboard = previous?.Blackboard;
            if ((currentBlackboard == null || currentBlackboard.Count == 0)
                && (previousBlackboard == null || previousBlackboard.Count == 0))
            {
                return EmptyInstance;
            }

            var rows = new List<ObservationBlackboardRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (currentBlackboard != null)
            {
                for (var i = 0; i < currentBlackboard.Count; i++)
                {
                    var key = currentBlackboard.KeyName(i);
                    if (!seen.Add(key)) continue;
                    rows.Add(CreateRow(key, currentBlackboard, i, previousBlackboard, diff));
                }
            }

            if (previousBlackboard != null)
            {
                for (var i = 0; i < previousBlackboard.Count; i++)
                {
                    var key = previousBlackboard.KeyName(i);
                    if (!seen.Add(key)) continue;
                    rows.Add(CreateRemovedRow(key, previousBlackboard, i, diff));
                }
            }

            return rows.Count == 0 ? EmptyInstance : new ObservationBlackboardView(rows.ToArray());
        }

        public IReadOnlyList<ObservationBlackboardRow> Search(string query, bool changedOnly = false)
        {
            var q = query ?? "";
            if (q.Length == 0 && !changedOnly) return _rows;

            var result = new List<ObservationBlackboardRow>();
            for (var i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (changedOnly && !row.IsChanged) continue;
                if (q.Length > 0
                    && row.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && row.CurrentValue.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                    && row.PreviousValue.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                result.Add(row);
            }
            return result;
        }

        public bool TryGetRow(string key, out ObservationBlackboardRow row) =>
            _rowsByKey.TryGetValue(key ?? "", out row);

        private static ObservationBlackboardRow CreateRow(
            string key,
            ObservationBlackboard currentBlackboard,
            int currentIndex,
            ObservationBlackboard? previousBlackboard,
            ObservationDiff? diff)
        {
            var previousIndex = previousBlackboard?.IndexOf(key) ?? -1;
            var currentValue = currentBlackboard.GetDisplayValue(currentIndex);
            var previousValue = previousIndex >= 0 && previousBlackboard != null
                ? previousBlackboard.GetDisplayValue(previousIndex)
                : "";
            var isAdded = previousIndex < 0;
            var isChanged = diff?.ContainsChangedBlackboardKey(key)
                ?? (isAdded || !string.Equals(previousValue, currentValue, StringComparison.Ordinal));
            return new ObservationBlackboardRow(
                key,
                currentBlackboard.KeyType(currentIndex),
                currentValue,
                previousValue,
                true,
                previousIndex >= 0,
                isChanged,
                isAdded,
                false);
        }

        private static ObservationBlackboardRow CreateRemovedRow(
            string key,
            ObservationBlackboard previousBlackboard,
            int previousIndex,
            ObservationDiff? diff)
        {
            return new ObservationBlackboardRow(
                key,
                previousBlackboard.KeyType(previousIndex),
                "",
                previousBlackboard.GetDisplayValue(previousIndex),
                false,
                true,
                diff?.ContainsChangedBlackboardKey(key) ?? true,
                false,
                true);
        }
    }
}
