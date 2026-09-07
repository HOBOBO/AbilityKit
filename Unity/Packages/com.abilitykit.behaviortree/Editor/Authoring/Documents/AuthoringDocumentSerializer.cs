#nullable enable

using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Documents;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree serialization adapter for the platform document session.
    /// The platform owns lifecycle and history while this package remains authoritative
    /// for the BT source document format.
    /// </summary>
    internal sealed class AuthoringDocumentSerializer : IEditorDocumentSerializer<AuthoringSourceDocument>
    {
        public string Serialize(AuthoringSourceDocument document)
        {
            return AuthoringJson.Save(document);
        }

        public AuthoringSourceDocument Deserialize(string snapshot)
        {
            return AuthoringJson.Load(snapshot);
        }
    }
}
