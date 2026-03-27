// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;
using System.Collections.Generic;

#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif

using UnityHFSM;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Represents a leaf state node in the HFSM graph.
    /// </summary>
    [Serializable]
    public class HfsmStateNode : HfsmNodeBase
    {
        [SerializeField]
        private bool _needsExitTime;

        [SerializeField]
        private bool _isGhostState;

        [SerializeField]
        private List<string> _entryActionMethodNames = new List<string>();

        [SerializeField]
        private List<string> _logicActionMethodNames = new List<string>();

        [SerializeField]
        private List<string> _exitActionMethodNames = new List<string>();

        [SerializeField]
        private List<string> _canExitMethodNames = new List<string>();

        // ========== Behavior System Support ==========
        [SerializeField]
        private List<HfsmBehaviorItem> _behaviorItems = new List<HfsmBehaviorItem>();

        [SerializeField]
        private string _rootBehaviorId;

        /// <summary>
        /// If true, the state will wait for CanExit to return true before transitioning.
        /// </summary>
        public bool NeedsExitTime
        {
            get => _needsExitTime;
            set => _needsExitTime = value;
        }

        /// <summary>
        /// If true, this state will not be shown in the active state path.
        /// </summary>
        public bool IsGhostState
        {
            get => _isGhostState;
            set => _isGhostState = value;
        }

        public IReadOnlyList<string> EntryActionMethodNames => _entryActionMethodNames;
        public IReadOnlyList<string> LogicActionMethodNames => _logicActionMethodNames;
        public IReadOnlyList<string> ExitActionMethodNames => _exitActionMethodNames;
        public IReadOnlyList<string> CanExitMethodNames => _canExitMethodNames;

        // ========== Behavior System Properties ==========
        /// <summary>
        /// All behavior items in this state.
        /// </summary>
        public IReadOnlyList<HfsmBehaviorItem> BehaviorItems => _behaviorItems;

        /// <summary>
        /// Access to behavior items list for editor and serialization
        /// </summary>
        public List<HfsmBehaviorItem> BehaviorItemsInternal => _behaviorItems;

        /// <summary>
        /// The root behavior item ID.
        /// </summary>
        public string RootBehaviorId
        {
            get => _rootBehaviorId;
            set => _rootBehaviorId = value;
        }

        /// <summary>
        /// Whether this state has any behaviors defined.
        /// </summary>
        public bool HasBehaviors => _behaviorItems != null && _behaviorItems.Count > 0;

        public HfsmStateNode()
        {
            _displayName = "New State";
            _nodeType = HfsmNodeType.State;
        }

        public HfsmStateNode(string displayName) : base(displayName, HfsmNodeType.State)
        {
        }

        public override string GetName() => _displayName ?? "New State";

        public override string GetNodeTypeDescription() => "Leaf State";

        public void AddEntryAction(string methodName)
        {
            if (!string.IsNullOrEmpty(methodName) && !_entryActionMethodNames.Contains(methodName))
            {
                _entryActionMethodNames.Add(methodName);
            }
        }

        public void AddLogicAction(string methodName)
        {
            if (!string.IsNullOrEmpty(methodName) && !_logicActionMethodNames.Contains(methodName))
            {
                _logicActionMethodNames.Add(methodName);
            }
        }

        public void AddExitAction(string methodName)
        {
            if (!string.IsNullOrEmpty(methodName) && !_exitActionMethodNames.Contains(methodName))
            {
                _exitActionMethodNames.Add(methodName);
            }
        }

        public void AddCanExitMethod(string methodName)
        {
            if (!string.IsNullOrEmpty(methodName) && !_canExitMethodNames.Contains(methodName))
            {
                _canExitMethodNames.Add(methodName);
            }
        }

        public void RemoveEntryAction(string methodName) => _entryActionMethodNames.Remove(methodName);
        public void RemoveLogicAction(string methodName) => _logicActionMethodNames.Remove(methodName);
        public void RemoveExitAction(string methodName) => _exitActionMethodNames.Remove(methodName);
        public void RemoveCanExitMethod(string methodName) => _canExitMethodNames.Remove(methodName);

        public void ClearEntryActions() => _entryActionMethodNames.Clear();
        public void ClearLogicActions() => _logicActionMethodNames.Clear();
        public void ClearExitActions() => _exitActionMethodNames.Clear();
        public void ClearCanExitMethods() => _canExitMethodNames.Clear();

        // ========== Behavior System Methods ==========
        /// <summary>
        /// Gets a behavior item by its ID.
        /// </summary>
        public HfsmBehaviorItem GetBehaviorItem(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var item in _behaviorItems)
            {
                if (item.id == id)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Adds a behavior item to this state.
        /// </summary>
        public void AddBehaviorItem(HfsmBehaviorItem item)
        {
            if (item == null)
                return;

            _behaviorItems.Add(item);
        }

        /// <summary>
        /// Clears all behavior items.
        /// </summary>
        public void ClearBehaviorItems()
        {
            _behaviorItems.Clear();
            _rootBehaviorId = null;
        }

        /// <summary>
        /// Initializes behavior items with a new list.
        /// </summary>
        public void InitializeBehaviorItems(List<HfsmBehaviorItem> items)
        {
            _behaviorItems = items ?? new List<HfsmBehaviorItem>();
        }

        /// <summary>
        /// Removes a behavior item by its ID.
        /// </summary>
        public bool RemoveBehaviorItem(string id)
        {
            var item = GetBehaviorItem(id);
            if (item == null)
                return false;

            // Remove from parent's child list
            if (!string.IsNullOrEmpty(item.parentId))
            {
                var parent = GetBehaviorItem(item.parentId);
                parent?.childIds.Remove(id);
            }

            // Recursively remove children
            RemoveBehaviorItemRecursive(id);

            return true;
        }

        private void RemoveBehaviorItemRecursive(string id)
        {
            var item = GetBehaviorItem(id);
            if (item == null)
                return;

            foreach (var childId in item.childIds)
            {
                RemoveBehaviorItemRecursive(childId);
            }

            _behaviorItems.Remove(item);
        }

        /// <summary>
        /// Gets all root behavior items (items without a parent).
        /// </summary>
        public List<HfsmBehaviorItem> GetRootBehaviorItems()
        {
            var roots = new List<HfsmBehaviorItem>();
            foreach (var item in _behaviorItems)
            {
                if (string.IsNullOrEmpty(item.parentId))
                {
                    roots.Add(item);
                }
            }
            return roots;
        }

        /// <summary>
        /// Gets all behavior items that are children of the specified parent.
        /// </summary>
        public List<HfsmBehaviorItem> GetBehaviorChildren(string parentId)
        {
            var parent = GetBehaviorItem(parentId);
            if (parent == null)
                return new List<HfsmBehaviorItem>();

            var children = new List<HfsmBehaviorItem>();
            foreach (var childId in parent.childIds)
            {
                var child = GetBehaviorItem(childId);
                if (child != null)
                {
                    children.Add(child);
                }
            }
            return children;
        }

        public override bool Validate()
        {
            if (!base.Validate())
                return false;

            if (string.IsNullOrEmpty(DisplayName))
            {
                HfsmLog.LogError($"HfsmStateNode with ID {Id} has an empty display name.");
                return false;
            }
            return true;
        }

        public override HfsmNodeBase Clone()
        {
            var clone = new HfsmStateNode();
            clone._displayName = _displayName;
            clone._position = _position + new Vector2(50, 50);
            clone._size = _size;
            clone._needsExitTime = _needsExitTime;
            clone._isGhostState = _isGhostState;
            clone._entryActionMethodNames = new List<string>(_entryActionMethodNames);
            clone._logicActionMethodNames = new List<string>(_logicActionMethodNames);
            clone._exitActionMethodNames = new List<string>(_exitActionMethodNames);
            clone._canExitMethodNames = new List<string>(_canExitMethodNames);

            // Clone behavior items with ID remapping
            if (_behaviorItems != null && _behaviorItems.Count > 0)
            {
                var idMapping = new Dictionary<string, string>();

                // First pass: create clones with new IDs
                foreach (var item in _behaviorItems)
                {
                    var clonedItem = item.Clone();
                    idMapping[item.id] = clonedItem.id;
                    clone._behaviorItems.Add(clonedItem);
                }

                // Second pass: update parent/child references using new IDs
                foreach (var clonedItem in clone._behaviorItems)
                {
                    if (!string.IsNullOrEmpty(clonedItem.parentId) && idMapping.TryGetValue(clonedItem.parentId, out var newParentId))
                    {
                        clonedItem.parentId = newParentId;
                    }

                    var newChildIds = new List<string>();
                    foreach (var childId in clonedItem.childIds)
                    {
                        if (idMapping.TryGetValue(childId, out var newChildId))
                        {
                            newChildIds.Add(newChildId);
                        }
                    }
                    clonedItem.childIds = newChildIds;
                }

                // Update root behavior ID
                if (!string.IsNullOrEmpty(_rootBehaviorId) && idMapping.TryGetValue(_rootBehaviorId, out var newRootId))
                {
                    clone._rootBehaviorId = newRootId;
                }
            }

            return clone;
        }
    }
}
