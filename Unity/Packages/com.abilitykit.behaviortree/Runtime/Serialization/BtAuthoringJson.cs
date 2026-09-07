using AbilityKit.BehaviorTree.Authoring;

namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.AuthoringJson.", false)]
    public static class BtAuthoringJson
    {
#pragma warning disable CS0618
        public static string Save(BtAuthoringSourceDocument document)
            => AuthoringJson.Save(AuthoringCompatibility.ToModel(document));

        public static BtAuthoringSourceDocument Load(string json)
            => AuthoringCompatibility.ToLegacy(AuthoringJson.Load(json));

        public static string SaveProjectManifest(BtAuthoringProjectManifest manifest)
            => AuthoringJson.SaveProjectManifest(AuthoringCompatibility.ToModel(manifest));

        public static BtAuthoringProjectManifest LoadProjectManifest(string json)
            => AuthoringCompatibility.ToLegacy(AuthoringJson.LoadProjectManifest(json));
#pragma warning restore CS0618
    }
}
