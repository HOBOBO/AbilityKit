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

    internal sealed class LegacyDebugDeltaViewAdapter :
        LegacyDebugViewAdapter,
        AbilityKit.BehaviorTree.IBtTreeDebugDeltaView
    {
        private readonly TreeDebugDeltaView _deltaView;

        public LegacyDebugDeltaViewAdapter(TreeDebugView view, TreeDebugDeltaView deltaView)
            : base(view)
            => _deltaView = deltaView;

        public long DebugSequence => _deltaView.DebugSequence;

        public AbilityKit.BehaviorTree.BtTreeDebugDelta CaptureDebugDelta(
            long knownSequence,
            bool includeBlackboard)
            => _deltaView.CaptureDebugDelta(knownSequence, includeBlackboard).ToLegacy();
    }
}
