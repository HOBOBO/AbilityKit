#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class BtDebugObservationSessionTests
    {
        [Test]
        public void Capture_RecordsNodeAndBlackboardChanges()
        {
            var view = new FakeDebugView();
            var session = new BtDebugObservationSession();

            view.SetSample(1, BtNodeState.Running, 10);
            session.Capture(view);
            Assert.That(session.Events, Is.Empty);

            view.SetSample(2, BtNodeState.Success, 11);
            session.Capture(view);

            Assert.That(session.LastFrame, Is.EqualTo(2));
            Assert.That(session.Nodes[0].State, Is.EqualTo(BtNodeState.Success));
            Assert.That(session.HasBlackboardChanged("score"), Is.True);
            Assert.That(session.Events, Has.Count.EqualTo(2));
            Assert.That(session.Events[0].Source, Is.EqualTo("root"));
            Assert.That(session.Events[1].Source, Is.EqualTo("score"));
        }

        [Test]
        public void Capture_BoundsHistoryAndResetClearsSession()
        {
            var view = new FakeDebugView();
            var session = new BtDebugObservationSession(historyLimit: 2);

            for (var frame = 0; frame < 4; frame++)
            {
                view.SetSample(frame, frame % 2 == 0 ? BtNodeState.Running : BtNodeState.Success, frame);
                session.Capture(view);
            }

            Assert.That(session.Events, Has.Count.EqualTo(2));
            Assert.That(session.Events[0].Frame, Is.EqualTo(3));
            Assert.That(session.Events[1].Frame, Is.EqualTo(3));

            session.Reset();

            Assert.That(session.HasSample, Is.False);
            Assert.That(session.Nodes, Is.Empty);
            Assert.That(session.Events, Is.Empty);
            Assert.That(session.Blackboard, Is.Null);
        }

        private sealed class FakeDebugView : IBtTreeDebugView
        {
            private List<BtNodeDebugInfo> _nodes = new();
            private BtBlackboardValueSnapshot _blackboard = new();

            public string TreeId => "session-test";
            public string DisplayName => TreeId;
            public string OwnerLabel => "test";
            public int NodeCount => 1;
            public int LastFrame { get; private set; }
            public BtTreeDefinition TreeDefinition { get; } = BuildDefinition();
            public IReadOnlyDictionary<string, string> NodeSourceTree => null;
            public IReadOnlyDictionary<string, string> NodeSourceNode => null;
            public IReadOnlyList<BtSubtreeInstance> SubtreeInstances => new List<BtSubtreeInstance>();

            public List<BtNodeDebugInfo> GetNodeStates() => new(_nodes);
            public BtBlackboardValueSnapshot GetBlackboard() => _blackboard;
            public BtTreeRuntimeSnapshot CaptureState() => new();

            public void SetSample(int frame, BtNodeState state, long score)
            {
                LastFrame = frame;
                _nodes = new List<BtNodeDebugInfo>
                {
                    new("root", "Root", BtBuiltInNodeTypes.Succeed, BtNodeKind.Action, state, 0, 1, -1),
                };
                _blackboard = new BtBlackboardValueSnapshot
                {
                    KeyNames = new List<string> { "score" },
                    KeyTypes = new List<BtValueType> { BtValueType.Int64 },
                    BoolValues = new List<bool> { false },
                    Int64Values = new List<long> { score },
                    Fixed64RawValues = new List<long> { 0 },
                    StringValues = new List<string> { "" },
                };
            }

            private static BtTreeDefinition BuildDefinition()
            {
                var definition = new BtTreeDefinition { TreeId = "session-test", RootNodeId = "root" };
                definition.Nodes.Add(new BtNodeDefinition { Id = "root", Type = BtBuiltInNodeTypes.Succeed });
                return definition;
            }
        }
    }
}
#endif
