#nullable enable

using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringBatchPropertyModel
    {
        public AuthoringBatchPropertyModel(
            IReadOnlyList<string> nodeIds,
            IReadOnlyList<AuthoringBatchPropertyField> fields)
        {
            NodeIds = nodeIds;
            Fields = fields;
        }

        public IReadOnlyList<string> NodeIds { get; }
        public IReadOnlyList<AuthoringBatchPropertyField> Fields { get; }
    }
}
