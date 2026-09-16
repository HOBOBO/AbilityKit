using System;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>负责把 authoring 文档组合为可观察的行为树运行实例。</summary>
    public static class ObservationRuntimeFactory
    {
        public static TreeRuntime Create(
            string authoringJson,
            ObservationRuntimeSettings settings,
            string ownerLabel)
        {
            if (string.IsNullOrWhiteSpace(authoringJson))
                throw new ArgumentException("Authoring JSON cannot be empty.", nameof(authoringJson));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            AuthoringSourceDocument document = AuthoringJson.Load(authoringJson);
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var definition = TreeExporter.ToRuntimeDefinition(document);

            return TreeRuntime.Create(
                definition,
                registry,
                options: new TreeRunOptions
                {
                    Seed = settings.Seed,
                    RestartWhenComplete = false,
                    DebugName = document.Tree.TreeId,
                    DebugOwnerLabel = ownerLabel ?? "",
                });
        }

        /// <summary>从编辑器导出的运行时 IR JSON 直接创建运行实例，验证「导出 → 运行时加载」正式链路。</summary>
        public static TreeRuntime CreateFromRuntimeJson(
            string runtimeJson,
            ObservationRuntimeSettings settings,
            string ownerLabel)
        {
            if (string.IsNullOrWhiteSpace(runtimeJson))
                throw new ArgumentException("Runtime JSON cannot be empty.", nameof(runtimeJson));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            var definition = TreeJson.Load(runtimeJson);

            return TreeRuntime.Create(
                definition,
                registry,
                options: new TreeRunOptions
                {
                    Seed = settings.Seed,
                    RestartWhenComplete = false,
                    DebugName = definition.TreeId,
                    DebugOwnerLabel = ownerLabel ?? "",
                });
        }
    }
}
