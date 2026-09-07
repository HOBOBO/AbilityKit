using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public static class RuntimeSnapshotUtility
    {
        public static RuntimeSnapshot Capture(StateMachine<string> root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return CaptureMachine(root, string.Empty);
        }

        public static void Restore(StateMachine<string> root, RuntimeSnapshot snapshot)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            RestoreMachine(root, snapshot, string.Empty);
        }

        private static RuntimeSnapshot CaptureMachine(
            StateMachine<string, string, string> machine,
            string stateId)
        {
            var stateNames = machine.GetAllStateNames();
            var children = new RuntimeSnapshot[stateNames.Count];
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
                    children[i] = new RuntimeSnapshot(
                        SnapshotNodeKind.CompositeActionState,
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

            return new RuntimeSnapshot(
                SnapshotNodeKind.StateMachine,
                stateId,
                machine.IsActive,
                machine.IsActive ? machine.ActiveStateName : string.Empty,
                machine.GetStartStateName(),
                actionState: null,
                children: children);
        }

        private static void RestoreMachine(
            StateMachine<string, string, string> machine,
            RuntimeSnapshot snapshot,
            string expectedStateId)
        {
            ValidateNode(snapshot, SnapshotNodeKind.StateMachine, expectedStateId);

            var stateNames = machine.GetAllStateNames();
            if (snapshot.Children.Count != stateNames.Count)
            {
                throw new InvalidOperationException(
                    $"HFSM snapshot node '{expectedStateId}' has {snapshot.Children.Count} children; expected {stateNames.Count}.");
            }

            var snapshotsById = new Dictionary<string, RuntimeSnapshot>(snapshot.Children.Count, StringComparer.Ordinal);
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
                    ValidateNode(childSnapshot, SnapshotNodeKind.CompositeActionState, childId);
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
            RuntimeSnapshot snapshot,
            SnapshotNodeKind expectedKind,
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
