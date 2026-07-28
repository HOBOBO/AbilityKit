using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public enum HfsmRuntimeSnapshotNodeKind
    {
        StateMachine = 0,
        CompositeActionState = 1,
    }

    public sealed class HfsmRuntimeSnapshot
    {
        public HfsmRuntimeSnapshot(
            HfsmRuntimeSnapshotNodeKind kind,
            string stateId,
            bool isActive,
            string activeStateId,
            string rememberedStartStateId,
            CompositeActionStateSnapshot actionState,
            IReadOnlyList<HfsmRuntimeSnapshot> children)
        {
            Kind = kind;
            StateId = stateId ?? string.Empty;
            IsActive = isActive;
            ActiveStateId = activeStateId ?? string.Empty;
            RememberedStartStateId = rememberedStartStateId ?? string.Empty;
            ActionState = actionState;
            Children = children ?? Array.Empty<HfsmRuntimeSnapshot>();
        }

        public HfsmRuntimeSnapshotNodeKind Kind { get; }
        public string StateId { get; }
        public bool IsActive { get; }
        public string ActiveStateId { get; }
        public string RememberedStartStateId { get; }
        public CompositeActionStateSnapshot ActionState { get; }
        public IReadOnlyList<HfsmRuntimeSnapshot> Children { get; }
    }

    public static class HfsmRuntimeSnapshotUtility
    {
        public static HfsmRuntimeSnapshot Capture(StateMachine<string> root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return CaptureMachine(root, string.Empty);
        }

        public static void Restore(StateMachine<string> root, HfsmRuntimeSnapshot snapshot)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RestoreMachine(root, snapshot, string.Empty);
        }

        private static HfsmRuntimeSnapshot CaptureMachine(
            StateMachine<string, string, string> machine,
            string stateId)
        {
            var stateNames = machine.GetAllStateNames();
            var children = new HfsmRuntimeSnapshot[stateNames.Count];
            for (var i = 0; i < stateNames.Count; i++)
            {
                var childId = stateNames[i];
                var child = machine.GetState(childId);
                if (child is StateMachine<string, string, string> childMachine)
                {
                    children[i] = CaptureMachine(childMachine, childId);
                }
                else if (child is CompositeActionState<string, string> actionState)
                {
                    children[i] = new HfsmRuntimeSnapshot(
                        HfsmRuntimeSnapshotNodeKind.CompositeActionState,
                        childId,
                        isActive: machine.IsActive && string.Equals(machine.ActiveStateName, childId, StringComparison.Ordinal),
                        activeStateId: string.Empty,
                        rememberedStartStateId: string.Empty,
                        actionState: actionState.CaptureRuntimeSnapshot(),
                        children: null);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"HFSM state '{childId}' of type '{child.GetType().FullName}' does not support rollback.");
                }
            }

            return new HfsmRuntimeSnapshot(
                HfsmRuntimeSnapshotNodeKind.StateMachine,
                stateId,
                machine.IsActive,
                machine.IsActive ? machine.ActiveStateName : string.Empty,
                machine.GetStartStateName(),
                actionState: null,
                children: children);
        }

        private static void RestoreMachine(
            StateMachine<string, string, string> machine,
            HfsmRuntimeSnapshot snapshot,
            string expectedStateId)
        {
            ValidateNode(snapshot, HfsmRuntimeSnapshotNodeKind.StateMachine, expectedStateId);

            var stateNames = machine.GetAllStateNames();
            if (snapshot.Children.Count != stateNames.Count)
            {
                throw new InvalidOperationException(
                    $"HFSM snapshot node '{expectedStateId}' has {snapshot.Children.Count} children; expected {stateNames.Count}.");
            }

            var snapshotsById = new Dictionary<string, HfsmRuntimeSnapshot>(snapshot.Children.Count, StringComparer.Ordinal);
            for (var i = 0; i < snapshot.Children.Count; i++)
            {
                var childSnapshot = snapshot.Children[i];
                if (childSnapshot == null || !snapshotsById.TryAdd(childSnapshot.StateId, childSnapshot))
                {
                    throw new InvalidOperationException(
                        $"HFSM snapshot node '{expectedStateId}' contains a null or duplicate child.");
                }
            }

            machine.RestoreRuntimeState(
                snapshot.IsActive,
                snapshot.ActiveStateId,
                snapshot.RememberedStartStateId);

            for (var i = 0; i < stateNames.Count; i++)
            {
                var childId = stateNames[i];
                if (!snapshotsById.TryGetValue(childId, out var childSnapshot))
                {
                    throw new InvalidOperationException(
                        $"HFSM snapshot node '{expectedStateId}' is missing state '{childId}'.");
                }

                var child = machine.GetState(childId);
                if (child is StateMachine<string, string, string> childMachine)
                {
                    RestoreMachine(childMachine, childSnapshot, childId);
                }
                else if (child is CompositeActionState<string, string> actionState)
                {
                    ValidateNode(childSnapshot, HfsmRuntimeSnapshotNodeKind.CompositeActionState, childId);
                    actionState.RestoreRuntimeSnapshot(childSnapshot.ActionState);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"HFSM state '{childId}' of type '{child.GetType().FullName}' does not support rollback.");
                }
            }
        }

        private static void ValidateNode(
            HfsmRuntimeSnapshot snapshot,
            HfsmRuntimeSnapshotNodeKind expectedKind,
            string expectedStateId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Kind != expectedKind || !string.Equals(snapshot.StateId, expectedStateId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"HFSM snapshot node '{snapshot.StateId}' ({snapshot.Kind}) does not match " +
                    $"'{expectedStateId}' ({expectedKind}).");
            }
        }
    }
}
