using AbilityKit.BehaviorTree.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        {
            if (document == null) throw new System.ArgumentNullException(nameof(document));
            document.Schema = BtAuthoringSchema.Id;
            document.Version = BtAuthoringSchema.Version;
            return JsonConvert.SerializeObject(document, Settings);
        }

        public static BtAuthoringSourceDocument Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new System.ArgumentException("BT authoring JSON must not be empty.", nameof(json));

            var root = JObject.Parse(json);
            var document = JsonConvert.DeserializeObject<BtAuthoringSourceDocument>(json, Settings);
            if (document == null)
                throw new System.InvalidOperationException("BT authoring JSON produced a null document.");

            if (!string.Equals(document.Schema, BtAuthoringSchema.Id, System.StringComparison.Ordinal))
                throw new System.InvalidOperationException($"Unsupported BT authoring schema '{document.Schema}'.");
            if (!string.Equals(document.Version, BtAuthoringSchema.Version, System.StringComparison.Ordinal)
                && !string.Equals(document.Version, BtAuthoringSchema.LegacyVersion, System.StringComparison.Ordinal))
                throw new System.InvalidOperationException($"Unsupported BT authoring version '{document.Version}'.");

            // 旧 authoring 文档没有画布注释字段，保持无迁移成本读取。
            document.Notes ??= new System.Collections.Generic.List<BtAuthoringNoteData>();

            // v1 把显示名/注释放在运行时节点里。先从原始 token 读取，再迁移到独立编辑元数据。
            if (root["tree"]?["nodes"] is JArray nodes)
            {
                foreach (var token in nodes)
                {
                    var nodeId = token?["id"]?.Value<string>() ?? "";
                    if (nodeId.Length == 0 || document.TryGetNodeMetadata(nodeId, out _)) continue;
                    var displayName = token?["name"]?.Value<string>() ?? "";
                    var comment = token?["comment"]?.Value<string>() ?? "";
                    if (displayName.Length == 0 && comment.Length == 0) continue;
                    document.NodeMetadata.Add(new BtAuthoringNodeMetadata
                    {
                        NodeId = nodeId,
                        DisplayName = displayName,
                        Comment = comment,
                    });
                }
            }

            document.Schema = BtAuthoringSchema.Id;
            document.Version = BtAuthoringSchema.Version;
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
