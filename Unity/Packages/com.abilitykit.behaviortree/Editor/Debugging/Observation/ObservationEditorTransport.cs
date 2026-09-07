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
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationTransportFrameKind")]
    public enum ObservationTransportFrameKind
    {
        Reset = 0,
        Snapshot = 1,
    }

    [Serializable]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationTransportFrameDto")]
    public sealed class ObservationTransportFrameDto
    {
        public int ContractVersion = ObservationEditorTransport.ContractVersion;
        public int Kind = (int)ObservationTransportFrameKind.Snapshot;
        public bool HasSnapshot;
        public ObservationSnapshotDto Snapshot = new();
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtObservationSnapshotSink")]
    public interface IObservationSnapshotSink
    {
        void Reset();
        void PushSnapshot(ObservationSnapshot snapshot);
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationTimelineSnapshotSink")]
    public sealed class ObservationTimelineSnapshotSink : IObservationSnapshotSink
    {
        public ObservationTimeline Timeline { get; }

        public ObservationTimelineSnapshotSink(ObservationTimeline timeline)
        {
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        }

        public void Reset() => Timeline.Clear();

        public void PushSnapshot(ObservationSnapshot snapshot) => Timeline.Append(snapshot);
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationEditorTransport")]
    public static class ObservationEditorTransport
    {
        public const int ContractVersion = 1;

        public static ObservationTransportFrameDto FullSnapshot(ObservationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new ObservationTransportFrameDto
            {
                ContractVersion = ContractVersion,
                Kind = (int)ObservationTransportFrameKind.Snapshot,
                HasSnapshot = true,
                Snapshot = ObservationRecording.ToDto(snapshot),
            };
        }

        public static ObservationTransportFrameDto Reset() =>
            new()
            {
                ContractVersion = ContractVersion,
                Kind = (int)ObservationTransportFrameKind.Reset,
                HasSnapshot = false,
            };

        public static bool TryApply(ObservationTransportFrameDto frame, IObservationSnapshotSink sink)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (frame.ContractVersion <= 0 || frame.ContractVersion > ContractVersion) return false;

            switch ((ObservationTransportFrameKind)frame.Kind)
            {
                case ObservationTransportFrameKind.Reset:
                    sink.Reset();
                    return true;
                case ObservationTransportFrameKind.Snapshot:
                    if (!frame.HasSnapshot || frame.Snapshot == null) return false;
                    sink.PushSnapshot(ObservationRecording.SnapshotFromDto(frame.Snapshot));
                    return true;
                default:
                    return false;
            }
        }

        public static string CreateRuntimeSnapshotJson(ObservationSnapshot snapshot) =>
            TreeJson.SaveSnapshot(ToRuntimeSnapshot(snapshot));

        public static TreeRuntimeSnapshot ToRuntimeSnapshot(ObservationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var runtime = new TreeRuntimeSnapshot
            {
                SnapshotVersion = 1,
                Enabled = true,
                TreeState = snapshot.NodeCount > 0 ? snapshot.Nodes[0].State : NodeState.Inactive,
                Blackboard = ToBlackboardSnapshot(snapshot.Blackboard),
            };

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                runtime.Nodes.Add(new NodeRuntimeSnapshot
                {
                    NodeId = node.NodeId ?? "",
                    State = node.State,
                    RunningChildIndex = node.RunningChildIndex,
                });
            }

            var stack = BuildRunStack(snapshot);
            if (stack.NodeIndexes.Count > 0) runtime.RunStacks.Add(stack);
            return runtime;
        }

        private static RunStackSnapshot BuildRunStack(ObservationSnapshot snapshot)
        {
            var stack = new RunStackSnapshot();
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                if (snapshot.Nodes[i].OnStackCount > 0) stack.NodeIndexes.Add(i);
            }
            return stack;
        }

        private static BlackboardValueSnapshot? ToBlackboardSnapshot(ObservationBlackboard? source)
        {
            if (source == null) return null;
            var snapshot = new BlackboardValueSnapshot
            {
                KeyNames = new List<string>(source.Count),
                KeyTypes = new List<ValueType>(source.Count),
                BoolValues = new List<bool>(source.Count),
                Int64Values = new List<long>(source.Count),
                Fixed64RawValues = new List<long>(source.Count),
                StringValues = new List<string>(source.Count),
            };

            for (var i = 0; i < source.Count; i++)
            {
                var key = source.KeyName(i);
                var type = source.KeyType(i);
                snapshot.KeyNames.Add(key);
                snapshot.KeyTypes.Add(type);
                snapshot.BoolValues.Add(source.TryGetBool(key, out var boolValue) ? boolValue : false);
                snapshot.Int64Values.Add(source.TryGetInt64(key, out var int64Value) ? int64Value : 0L);
                snapshot.Fixed64RawValues.Add(source.TryGetFixed64Raw(key, out var fixedRawValue) ? fixedRawValue : 0L);
                snapshot.StringValues.Add(source.TryGetString(key, out var stringValue) ? stringValue : "");
            }

            return snapshot;
        }
    }

    internal sealed class ObservationSnapshotDebugView : TreeDebugView
    {
        private readonly ObservationSnapshot _snapshot;

        public ObservationSnapshotDebugView(ObservationSnapshot snapshot, TreeDefinition? definition = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TreeDefinition = definition ?? BuildFallbackDefinition(snapshot);
        }

        public string TreeId => _snapshot.TreeId;
        public string DisplayName => _snapshot.DisplayName;
        public string OwnerLabel => _snapshot.OwnerLabel;
        public int NodeCount => _snapshot.NodeCount;
        public int LastFrame => _snapshot.Frame;
        public TreeDefinition TreeDefinition { get; }
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _snapshot.SourceTree;
        public IReadOnlyDictionary<string, string>? NodeSourceNode => _snapshot.SourceNode;
        public IReadOnlyList<SubtreeInstance> SubtreeInstances => Array.Empty<SubtreeInstance>();

        public List<NodeDebugInfo> GetNodeStates() => new(_snapshot.Nodes);

        public BlackboardValueSnapshot GetBlackboard() =>
            ObservationEditorTransport.ToRuntimeSnapshot(_snapshot).Blackboard ?? new BlackboardValueSnapshot();

        public TreeRuntimeSnapshot CaptureState() => ObservationEditorTransport.ToRuntimeSnapshot(_snapshot);

        private static TreeDefinition BuildFallbackDefinition(ObservationSnapshot snapshot)
        {
            var definition = new TreeDefinition
            {
                TreeId = snapshot.TreeId,
                RootNodeId = snapshot.NodeCount > 0 ? snapshot.Nodes[0].NodeId : "",
            };
            foreach (var node in snapshot.Nodes)
            {
                definition.Nodes.Add(new NodeDefinition
                {
                    Id = node.NodeId,
                    Type = string.IsNullOrEmpty(node.TypeId) ? BuiltInNodeTypes.Succeed : node.TypeId,
                });
            }
            return definition;
        }
    }
}
