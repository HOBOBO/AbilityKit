using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Diagnostics
{
    using AbilityKit.BehaviorTree.Blackboard;
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;
    using AbilityKit.BehaviorTree.Registry;

    internal sealed class DebugDeltaViewAdapter : DebugViewAdapter, TreeDebugDeltaView
    {
        private readonly AbilityKit.BehaviorTree.IBtTreeDebugDeltaView _deltaView;

        public DebugDeltaViewAdapter(
            AbilityKit.BehaviorTree.IBtTreeDebugView view,
            AbilityKit.BehaviorTree.IBtTreeDebugDeltaView deltaView)
            : base(view)
            => _deltaView = deltaView;

        public long DebugSequence => _deltaView.DebugSequence;

        public TreeDebugDelta CaptureDebugDelta(long knownSequence, bool includeBlackboard)
            => TreeDebugDelta.FromLegacy(
                _deltaView.CaptureDebugDelta(knownSequence, includeBlackboard));
    }
}
