using System.Collections.Generic;
using ApiBlackboardKeyDefinition = AbilityKit.BehaviorTree.Definition.BlackboardKeyDefinition;
using ApiNodeDefinition = AbilityKit.BehaviorTree.Definition.NodeDefinition;
using ApiPropertyValue = AbilityKit.BehaviorTree.Definition.PropertyValue;
using ApiTreeDefinition = AbilityKit.BehaviorTree.Definition.TreeDefinition;
using ModelAuthoringGroupData = AbilityKit.BehaviorTree.Authoring.Model.AuthoringGroupData;
using ModelAuthoringMetadata = AbilityKit.BehaviorTree.Authoring.Model.AuthoringMetadata;
using ModelAuthoringNodeMetadata = AbilityKit.BehaviorTree.Authoring.Model.AuthoringNodeMetadata;
using ModelAuthoringNoteData = AbilityKit.BehaviorTree.Authoring.Model.AuthoringNoteData;
using ModelAuthoringSourceDocument = AbilityKit.BehaviorTree.Authoring.Model.AuthoringSourceDocument;
using ModelNodeLayoutData = AbilityKit.BehaviorTree.Authoring.Model.NodeLayoutData;

namespace AbilityKit.BehaviorTree.Authoring
{
#pragma warning disable CS0618
    internal static class AuthoringCompatibility
    {
        public static BtExportStatus ToLegacy(ExportStatus status) => (BtExportStatus)(int)status;
        public static ExportStatus ToModel(BtExportStatus status) => (ExportStatus)(int)status;

        public static BtAuthoringSourceKind ToLegacy(SourceKind kind) => (BtAuthoringSourceKind)(int)kind;
        public static SourceKind ToModel(BtAuthoringSourceKind kind) => (SourceKind)(int)kind;

        public static BtExportReportEntry ToLegacy(ExportReportEntry source)
            => new(source.TreeId, source.Target, ToLegacy(source.Status), source.Message);

        public static List<BtExportReportEntry> ToLegacy(IReadOnlyList<ExportReportEntry> source)
        {
            var result = new List<BtExportReportEntry>(source.Count);
            foreach (var entry in source) result.Add(ToLegacy(entry));
            return result;
        }

        public static ModelAuthoringSourceDocument ToModel(BtAuthoringSourceDocument source)
        {
            var document = new ModelAuthoringSourceDocument
            {
                Schema = source.Schema,
                Version = source.Version,
                Metadata = ToModel(source.Metadata),
                Tree = ToModel(source.Tree),
            };

            foreach (var metadata in source.NodeMetadata) document.NodeMetadata.Add(ToModel(metadata));
            foreach (var layout in source.Layout) document.Layout.Add(ToModel(layout));
            foreach (var group in source.Groups) document.Groups.Add(ToModel(group));
            foreach (var note in source.Notes) document.Notes.Add(ToModel(note));
            return document;
        }

        public static BtAuthoringSourceDocument ToLegacy(ModelAuthoringSourceDocument source)
        {
            var document = new BtAuthoringSourceDocument
            {
                Schema = source.Schema,
                Version = source.Version,
                Metadata = ToLegacy(source.Metadata),
                Tree = ToLegacy(source.Tree),
            };

            foreach (var metadata in source.NodeMetadata) document.NodeMetadata.Add(ToLegacy(metadata));
            foreach (var layout in source.Layout) document.Layout.Add(ToLegacy(layout));
            foreach (var group in source.Groups) document.Groups.Add(ToLegacy(group));
            foreach (var note in source.Notes) document.Notes.Add(ToLegacy(note));
            return document;
        }

        public static ProjectManifest ToModel(BtAuthoringProjectManifest source) => new()
        {
            Trees = new List<string>(source.Trees),
            SourceDirectory = source.SourceDirectory,
            SourceKind = ToModel(source.SourceKind),
            ExportTargets = new List<string>(source.ExportTargets),
        };

        public static BtAuthoringProjectManifest ToLegacy(ProjectManifest source) => new()
        {
            Trees = new List<string>(source.Trees),
            SourceDirectory = source.SourceDirectory,
            SourceKind = ToLegacy(source.SourceKind),
            ExportTargets = new List<string>(source.ExportTargets),
        };

        private static ModelAuthoringMetadata ToModel(BtAuthoringMetadata source) => new()
        {
            Author = source.Author,
            Description = source.Description,
        };

        private static BtAuthoringMetadata ToLegacy(ModelAuthoringMetadata source) => new()
        {
            Author = source.Author,
            Description = source.Description,
        };

        private static ModelAuthoringNodeMetadata ToModel(BtAuthoringNodeMetadata source) => new()
        {
            NodeId = source.NodeId,
            DisplayName = source.DisplayName,
            Comment = source.Comment,
        };

        private static BtAuthoringNodeMetadata ToLegacy(ModelAuthoringNodeMetadata source) => new()
        {
            NodeId = source.NodeId,
            DisplayName = source.DisplayName,
            Comment = source.Comment,
        };

        private static ModelNodeLayoutData ToModel(BtNodeLayoutData source) => new()
        {
            NodeId = source.NodeId,
            X = source.X,
            Y = source.Y,
        };

        private static BtNodeLayoutData ToLegacy(ModelNodeLayoutData source) => new()
        {
            NodeId = source.NodeId,
            X = source.X,
            Y = source.Y,
        };

        private static ModelAuthoringGroupData ToModel(BtAuthoringGroupData source) => new()
        {
            Id = source.Id,
            Title = source.Title,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            NodeIds = new List<string>(source.NodeIds),
        };

        private static BtAuthoringGroupData ToLegacy(ModelAuthoringGroupData source) => new()
        {
            Id = source.Id,
            Title = source.Title,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            NodeIds = new List<string>(source.NodeIds),
        };

        private static ModelAuthoringNoteData ToModel(BtAuthoringNoteData source) => new()
        {
            Id = source.Id,
            Text = source.Text,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
        };

        private static BtAuthoringNoteData ToLegacy(ModelAuthoringNoteData source) => new()
        {
            Id = source.Id,
            Text = source.Text,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
        };

        public static ApiTreeDefinition ToModel(BtTreeDefinition source)
        {
            var definition = new ApiTreeDefinition
            {
                TreeId = source.TreeId,
                FormatVersion = source.FormatVersion,
                RootNodeId = source.RootNodeId,
            };
            foreach (var node in source.Nodes)
            {
                definition.Nodes.Add(ToModel(node));
            }
            foreach (var key in source.Blackboard.Keys)
            {
                definition.Blackboard.Keys.Add(ToModel(key));
            }
            return definition;
        }

        public static BtTreeDefinition ToLegacy(ApiTreeDefinition source)
        {
            var definition = new BtTreeDefinition
            {
                TreeId = source.TreeId,
                FormatVersion = source.FormatVersion,
                RootNodeId = source.RootNodeId,
            };
            foreach (var node in source.Nodes)
            {
                definition.Nodes.Add(ToLegacy(node));
            }
            foreach (var key in source.Blackboard.Keys)
            {
                definition.Blackboard.Keys.Add(ToLegacy(key));
            }
            return definition;
        }

        private static ApiNodeDefinition ToModel(BtNodeDefinition source)
        {
            var node = new ApiNodeDefinition
            {
                Id = source.Id,
                Type = source.Type,
                ChildIds = new List<string>(source.ChildIds),
            };
            foreach (var pair in source.Properties.Values)
            {
                node.Properties.Set(pair.Key, ToModel(pair.Value));
            }
            return node;
        }

        private static BtNodeDefinition ToLegacy(ApiNodeDefinition source)
        {
            var node = new BtNodeDefinition
            {
                Id = source.Id,
                Type = source.Type,
                ChildIds = new List<string>(source.ChildIds),
            };
            foreach (var pair in source.Properties.Values)
            {
                node.Properties.Set(pair.Key, ToLegacy(pair.Value));
            }
            return node;
        }

        private static ApiBlackboardKeyDefinition ToModel(BtBlackboardKeyDefinition source) => new()
        {
            Name = source.Name,
            Type = (AbilityKit.BehaviorTree.Definition.ValueType)(int)source.Type,
            Default = source.Default == null ? null : ToModel(source.Default),
        };

        private static BtBlackboardKeyDefinition ToLegacy(ApiBlackboardKeyDefinition source) => new()
        {
            Name = source.Name,
            Type = (BtValueType)(int)source.Type,
            Default = source.Default == null ? null : ToLegacy(source.Default),
        };

        private static ApiPropertyValue ToModel(BtPropertyValue source) => source.Type switch
        {
            BtValueType.Bool => ApiPropertyValue.Of(source.BoolValue),
            BtValueType.Int64 => ApiPropertyValue.Of(source.Int64Value),
            BtValueType.Fixed64 => ApiPropertyValue.Of(AbilityKit.Deterministic.Fixed64.FromRaw(source.Fixed64Raw)),
            BtValueType.String => ApiPropertyValue.Of(source.StringValue),
            _ => throw new System.InvalidOperationException($"Unsupported BT value type '{source.Type}'."),
        };

        private static BtPropertyValue ToLegacy(ApiPropertyValue source) => source.Type switch
        {
            AbilityKit.BehaviorTree.Definition.ValueType.Bool => BtPropertyValue.Of(source.BoolValue),
            AbilityKit.BehaviorTree.Definition.ValueType.Int64 => BtPropertyValue.Of(source.Int64Value),
            AbilityKit.BehaviorTree.Definition.ValueType.Fixed64 => BtPropertyValue.Of(AbilityKit.Deterministic.Fixed64.FromRaw(source.Fixed64Raw)),
            AbilityKit.BehaviorTree.Definition.ValueType.String => BtPropertyValue.Of(source.StringValue),
            _ => throw new System.InvalidOperationException($"Unsupported behavior tree value type '{source.Type}'."),
        };
    }
#pragma warning restore CS0618
}
