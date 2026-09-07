using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class RuntimeSnapshot
    {
        public RuntimeSnapshot(
            SnapshotNodeKind kind,
            string stateId,
            bool isActive,
            string activeStateId,
            string rememberedStartStateId,
            CompositeActionStateSnapshot actionState,
            IReadOnlyList<RuntimeSnapshot> children)
        {
            Kind = kind;
            StateId = stateId ?? string.Empty;
            IsActive = isActive;
            ActiveStateId = activeStateId ?? string.Empty;
            RememberedStartStateId = rememberedStartStateId ?? string.Empty;
            ActionState = actionState;
            Children = children ?? Array.Empty<RuntimeSnapshot>();
        }

        public SnapshotNodeKind Kind { get; }
        public string StateId { get; }
        public bool IsActive { get; }
        public string ActiveStateId { get; }
        public string RememberedStartStateId { get; }
        public CompositeActionStateSnapshot ActionState { get; }
        public IReadOnlyList<RuntimeSnapshot> Children { get; }
    }
}
