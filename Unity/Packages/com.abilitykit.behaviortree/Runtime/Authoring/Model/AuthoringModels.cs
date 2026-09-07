using System.Collections.Generic;
using AbilityKit.BehaviorTree.Definition;

namespace AbilityKit.BehaviorTree.Authoring.Model
{
    public static class AuthoringSchema
    {
        public const string Id = "abilitykit-bt-authoring";
        public const string Version = "2.0";
        public const string LegacyVersion = "1.0";
    }

    public sealed class AuthoringMetadata
    {
        public string Author { get; set; } = "team";
        public string Description { get; set; } = "";
    }

    public sealed class AuthoringNodeMetadata
    {
        public string NodeId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    public sealed class NodeLayoutData
    {
        public string NodeId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class AuthoringGroupData
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public List<string> NodeIds { get; set; } = new();
    }

    public sealed class AuthoringNoteData
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 240f;
        public float Height { get; set; } = 140f;
    }

    public sealed class AuthoringSourceDocument
    {
        public string Schema { get; set; } = AuthoringSchema.Id;
        public string Version { get; set; } = AuthoringSchema.Version;
        public AuthoringMetadata Metadata { get; set; } = new();
        public TreeDefinition Tree { get; set; } = new();
        public List<AuthoringNodeMetadata> NodeMetadata { get; set; } = new();
        public List<NodeLayoutData> Layout { get; set; } = new();
        public List<AuthoringGroupData> Groups { get; set; } = new();
        public List<AuthoringNoteData> Notes { get; set; } = new();

        public AuthoringNodeMetadata GetOrCreateNodeMetadata(string nodeId)
        {
            foreach (var metadata in NodeMetadata)
            {
                if (string.Equals(metadata.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    return metadata;
                }
            }

            var created = new AuthoringNodeMetadata { NodeId = nodeId ?? "" };
            NodeMetadata.Add(created);
            return created;
        }

        public bool TryGetNodeMetadata(string nodeId, out AuthoringNodeMetadata metadata)
        {
            foreach (var candidate in NodeMetadata)
            {
                if (string.Equals(candidate.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    metadata = candidate;
                    return true;
                }
            }

            metadata = null!;
            return false;
        }
    }
}
