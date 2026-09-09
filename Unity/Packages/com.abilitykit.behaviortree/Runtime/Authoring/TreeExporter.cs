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
                errors = new List<string> { "行为树编辑文档为空。" };
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

}
