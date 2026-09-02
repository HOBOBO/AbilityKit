#nullable enable

using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Documents;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree serialization adapter for the platform document session.
    /// The platform owns lifecycle and history while this package remains authoritative
    /// for the BT source document format.
    /// </summary>
    internal sealed class BtAuthoringDocumentSerializer : IEditorDocumentSerializer<BtAuthoringSourceDocument>
    {
        public string Serialize(BtAuthoringSourceDocument document)
        {
            return BtAuthoringJson.Save(document);
        }

        public BtAuthoringSourceDocument Deserialize(string snapshot)
        {
            return BtAuthoringJson.Load(snapshot);
        }
    }
}
