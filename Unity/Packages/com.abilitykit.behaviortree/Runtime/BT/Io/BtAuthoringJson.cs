using AbilityKit.BehaviorTree.Authoring;
using Newtonsoft.Json;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 授权源文档（编辑态 JSON）的读写。复用运行时 IR 的序列化设置与转换器，
    /// 保证文档内嵌的 <see cref="BtTreeDefinition"/> 与导出格式逐字节一致。
    /// </summary>
    public static class BtAuthoringJson
    {
        private static readonly JsonSerializerSettings Settings = BtTreeJson.CreateSettings(true);

        public static string Save(BtAuthoringSourceDocument document)
            => JsonConvert.SerializeObject(document, Settings);

        public static BtAuthoringSourceDocument Load(string json)
        {
            var document = JsonConvert.DeserializeObject<BtAuthoringSourceDocument>(json, Settings);
            if (document == null)
                throw new System.InvalidOperationException("BT authoring JSON produced a null document.");
            return document;
        }

        public static string SaveProjectManifest(BtAuthoringProjectManifest manifest)
            => JsonConvert.SerializeObject(manifest, Settings);

        public static BtAuthoringProjectManifest LoadProjectManifest(string json)
        {
            var manifest = JsonConvert.DeserializeObject<BtAuthoringProjectManifest>(json, Settings);
            if (manifest == null)
                throw new System.InvalidOperationException("BT project manifest JSON produced a null manifest.");
            return manifest;
        }
    }
}
