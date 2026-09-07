using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;

namespace AbilityKit.BehaviorTree.Authoring
{
    public static class TreeExporter
    {
        public static TreeDefinition ToRuntimeDefinition(AuthoringSourceDocument document)
        {
            if (document == null || document.Tree == null)
                return new TreeDefinition();

            return document.Tree.DeepClone();
        }

        public static string? Export(AuthoringSourceDocument document, NodeRegistry registry, out List<string> errors)
        {
            if (document == null)
            {
                errors = new List<string> { "Authoring document is null." };
                return null;
            }

            var definition = ToRuntimeDefinition(document);
            errors = TreeValidator.Validate(definition, registry);
            if (errors.Count > 0)
            {
                return null;
            }

            return TreeJson.Save(definition);
        }

        public static AuthoringSourceDocument Import(TreeDefinition definition, NodeRegistry? registry = null)
        {
            var document = new AuthoringSourceDocument();
            if (definition != null)
            {
                document.Tree = definition.DeepClone();
                foreach (var node in document.Tree.Nodes)
                {
                    document.Layout.Add(new NodeLayoutData { NodeId = node.Id });
                    var displayName = registry != null && registry.TryGetDescriptor(node.Type, out var descriptor)
                        ? descriptor.DisplayName
                        : node.Id;
                    document.NodeMetadata.Add(new AuthoringNodeMetadata
                    {
                        NodeId = node.Id,
                        DisplayName = displayName,
                    });
                }
            }
            return document;
        }
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.TreeExporter.", false)]
    public static class BtTreeExporter
    {
#pragma warning disable CS0618
        public static BtTreeDefinition ToRuntimeDefinition(BtAuthoringSourceDocument document)
            => AuthoringCompatibility.ToLegacy(TreeExporter.ToRuntimeDefinition(AuthoringCompatibility.ToModel(document)));

        public static string? Export(BtAuthoringSourceDocument document, BtNodeRegistry registry, out List<string> errors)
            => TreeExporter.Export(
                AuthoringCompatibility.ToModel(document),
                AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry),
                out errors);

        public static BtAuthoringSourceDocument Import(BtTreeDefinition definition, BtNodeRegistry? registry = null)
            => AuthoringCompatibility.ToLegacy(TreeExporter.Import(
                AuthoringCompatibility.ToModel(definition),
                registry == null ? null : AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry)));
#pragma warning restore CS0618
    }
}
