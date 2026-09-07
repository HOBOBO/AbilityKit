#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree;
using UnityEngine;

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
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationRecording")]
    public static class ObservationRecording
    {
        public const int FormatVersion = 1;

        public static ObservationRecordingDto ToDto(
            ObservationTimeline timeline,
            ObservationController? controller = null)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));

            var dto = new ObservationRecordingDto
            {
                FormatVersion = FormatVersion,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                Settings = new ObservationRecordingSettingsDto
                {
                    TimelineCapacity = timeline.SampleLimit,
                    SampleIntervalSeconds = controller?.SampleIntervalSeconds
                        ?? ObservationSettings.DefaultSampleIntervalSeconds,
                },
                Samples = new List<ObservationSnapshotDto>(timeline.Count),
            };

            for (var i = 0; i < timeline.Samples.Count; i++)
                dto.Samples.Add(ToDto(timeline.Samples[i]));
            return dto;
        }

        public static ObservationSnapshotDto ToDto(ObservationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var dto = new ObservationSnapshotDto
            {
                InstanceId = snapshot.InstanceId,
                Sequence = snapshot.Sequence,
                TreeId = snapshot.TreeId,
                DisplayName = snapshot.DisplayName,
                OwnerLabel = snapshot.OwnerLabel,
                Frame = snapshot.Frame,
                Nodes = new List<ObservationNodeDto>(snapshot.NodeCount),
                ActiveNodeIds = new List<string>(snapshot.ActiveNodeIds),
                SourceTree = ToMapDto(snapshot.SourceTree),
                SourceNode = ToMapDto(snapshot.SourceNode),
                HasBlackboard = snapshot.Blackboard != null,
                Blackboard = snapshot.Blackboard == null
                    ? new ObservationBlackboardDto()
                    : ToDto(snapshot.Blackboard),
            };

            foreach (var node in snapshot.Nodes)
            {
                dto.Nodes.Add(new ObservationNodeDto
                {
                    NodeId = node.NodeId ?? "",
                    Name = node.Name ?? "",
                    TypeId = node.TypeId ?? "",
                    Kind = (int)node.Kind,
                    State = (int)node.State,
                    Depth = node.Depth,
                    OnStackCount = node.OnStackCount,
                    RunningChildIndex = node.RunningChildIndex,
                    SourceTreeId = node.SourceTreeId ?? "",
                    HasSourceTreeId = node.SourceTreeId != null,
                });
            }
            return dto;
        }

        public static string ToJson(
            ObservationTimeline timeline,
            ObservationController? controller = null,
            bool prettyPrint = true) =>
            JsonUtility.ToJson(ToDto(timeline, controller), prettyPrint);

        public static ObservationTimeline TimelineFromDto(ObservationRecordingDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.FormatVersion <= 0 || dto.FormatVersion > FormatVersion)
                throw new NotSupportedException("Unsupported behavior tree observation recording format: " + dto.FormatVersion);

            var requestedCapacity = dto.Settings?.TimelineCapacity
                ?? Math.Max(dto.Samples?.Count ?? 0, ObservationSettings.DefaultTimelineCapacity);
            var capacity = Math.Max(requestedCapacity, dto.Samples?.Count ?? 0);
            var timeline = new ObservationTimeline(capacity);
            if (dto.Samples == null) return timeline;

            for (var i = 0; i < dto.Samples.Count; i++)
                timeline.Append(SnapshotFromDto(dto.Samples[i]));
            return timeline;
        }

        public static ObservationSnapshot SnapshotFromDto(ObservationSnapshotDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var sourceNodes = dto.Nodes ?? new List<ObservationNodeDto>();
            var nodes = new NodeDebugInfo[sourceNodes.Count];
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = sourceNodes[i] ?? new ObservationNodeDto();
                nodes[i] = new NodeDebugInfo(
                    node.NodeId ?? "",
                    node.Name ?? "",
                    node.TypeId ?? "",
                    (NodeKind)node.Kind,
                    (NodeState)node.State,
                    node.Depth,
                    node.OnStackCount,
                    node.RunningChildIndex,
                    node.HasSourceTreeId ? node.SourceTreeId ?? "" : null);
            }

            return ObservationSnapshot.CreateForReplay(
                dto.InstanceId,
                dto.Sequence,
                dto.TreeId ?? "",
                dto.DisplayName ?? "",
                dto.OwnerLabel ?? "",
                dto.Frame,
                nodes,
                (dto.ActiveNodeIds ?? new List<string>()).ToArray(),
                FromMapDto(dto.SourceTree),
                FromMapDto(dto.SourceNode),
                dto.HasBlackboard ? BlackboardFromDto(dto.Blackboard) : null);
        }

        public static string SnapshotToJson(ObservationSnapshot snapshot, bool prettyPrint = true) =>
            JsonUtility.ToJson(ToDto(snapshot), prettyPrint);

        public static ObservationSnapshot SnapshotFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON cannot be empty.", nameof(json));
            return SnapshotFromDto(JsonUtility.FromJson<ObservationSnapshotDto>(json));
        }

        public static ObservationTimeline TimelineFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON cannot be empty.", nameof(json));
            return TimelineFromDto(JsonUtility.FromJson<ObservationRecordingDto>(json));
        }

        public static ObservationOfflineReplay ReplayFromJson(string json) =>
            new(TimelineFromJson(json));

        public static void ExportToFile(string path, ObservationTimeline timeline, ObservationController? controller = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
            File.WriteAllText(path, ToJson(timeline, controller));
        }

        public static ObservationTimeline ImportTimelineFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
            return TimelineFromJson(File.ReadAllText(path));
        }

        public static ObservationOfflineReplay ImportReplayFromFile(string path) =>
            new(ImportTimelineFromFile(path));

        private static ObservationBlackboardDto ToDto(ObservationBlackboard blackboard)
        {
            var dto = new ObservationBlackboardDto
            {
                KeyNames = new List<string>(blackboard.Count),
                KeyTypes = new List<int>(blackboard.Count),
                BoolValues = new List<bool>(blackboard.Count),
                Int64Values = new List<long>(blackboard.Count),
                Fixed64RawValues = new List<long>(blackboard.Count),
                StringValues = new List<string>(blackboard.Count),
            };

            for (var i = 0; i < blackboard.Count; i++)
            {
                var type = blackboard.KeyType(i);
                dto.KeyNames.Add(blackboard.KeyName(i));
                dto.KeyTypes.Add((int)type);
                dto.BoolValues.Add(blackboard.TryGetBool(blackboard.KeyName(i), out var b) ? b : false);
                dto.Int64Values.Add(blackboard.TryGetInt64(blackboard.KeyName(i), out var l) ? l : 0L);
                dto.Fixed64RawValues.Add(blackboard.TryGetFixed64Raw(blackboard.KeyName(i), out var f) ? f : 0L);
                dto.StringValues.Add(blackboard.TryGetString(blackboard.KeyName(i), out var s) ? s : "");
            }
            return dto;
        }

        private static ObservationBlackboard BlackboardFromDto(ObservationBlackboardDto? dto)
        {
            dto ??= new ObservationBlackboardDto();
            var keyNames = dto.KeyNames ?? new List<string>();
            var keyTypes = dto.KeyTypes ?? new List<int>();
            var boolValues = dto.BoolValues ?? new List<bool>();
            var int64Values = dto.Int64Values ?? new List<long>();
            var fixed64RawValues = dto.Fixed64RawValues ?? new List<long>();
            var stringValues = dto.StringValues ?? new List<string>();
            var count = keyNames.Count;
            var names = new string[count];
            var types = new ValueType[count];
            var bools = new bool[count];
            var int64s = new long[count];
            var fixedRaw = new long[count];
            var strings = new string[count];

            for (var i = 0; i < count; i++)
            {
                names[i] = keyNames[i] ?? "";
                types[i] = i < keyTypes.Count ? (ValueType)keyTypes[i] : ValueType.String;
                bools[i] = i < boolValues.Count && boolValues[i];
                int64s[i] = i < int64Values.Count ? int64Values[i] : 0L;
                fixedRaw[i] = i < fixed64RawValues.Count ? fixed64RawValues[i] : 0L;
                strings[i] = i < stringValues.Count ? stringValues[i] ?? "" : "";
            }

            return ObservationBlackboard.CreateForReplay(names, types, bools, int64s, fixedRaw, strings);
        }

        private static List<ObservationStringMapEntryDto> ToMapDto(IReadOnlyDictionary<string, string> source)
        {
            var result = new List<ObservationStringMapEntryDto>(source?.Count ?? 0);
            if (source == null) return result;
            foreach (var pair in source)
            {
                result.Add(new ObservationStringMapEntryDto
                {
                    Key = pair.Key ?? "",
                    Value = pair.Value ?? "",
                });
            }
            result.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        private static IReadOnlyDictionary<string, string> FromMapDto(List<ObservationStringMapEntryDto>? source)
        {
            if (source == null || source.Count == 0) return ObservationSnapshot.EmptySourceMap;
            var result = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null || string.IsNullOrEmpty(item.Key)) continue;
                result[item.Key] = item.Value ?? "";
            }
            return result;
        }
    }

}
