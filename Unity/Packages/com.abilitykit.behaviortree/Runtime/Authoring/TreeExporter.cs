using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
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

        public static string? Export(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            out List<string> errors,
            TreeDefinitionResolver? resolver = null)
        {
            if (document == null)
            {
                errors = new List<string> { "行为树编辑文档为空。" };
                return null;
            }

            var result = BehaviorTreeBuildPipeline.Build(document, registry, resolver);
            errors = new List<string>();
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == ValidationSeverity.Error)
                    errors.Add($"[{diagnostic.Code}] {diagnostic.Message}");
            }
            if (!result.Success) return null;

            return TreeJson.Save(result.SourceDefinition);
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
