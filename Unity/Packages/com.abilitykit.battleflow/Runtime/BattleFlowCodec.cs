using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.BattleFlow
{
    /// <summary>战斗流程文档的 Json.NET 编解码（TypeNameHandling 保积木多态类型，编辑器与 .NET runner 共用）。</summary>
    public static class BattleFlowCodec
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        /// <summary>序列化为 JSON。</summary>
        public static string Serialize(BattleFlowDocument doc) => JsonConvert.SerializeObject(doc, Settings);

        /// <summary>从 JSON 反序列化。</summary>
        public static BattleFlowDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("BattleFlow JSON is empty.", nameof(json));
            return JsonConvert.DeserializeObject<BattleFlowDocument>(json, Settings)
                   ?? throw new InvalidDataException("BattleFlow JSON did not contain an object.");
        }

        /// <summary>写到文件。</summary>
        public static void Save(string path, BattleFlowDocument doc) => File.WriteAllText(path, Serialize(doc));

        /// <summary>从文件读取。</summary>
        public static BattleFlowDocument Load(string path) => Parse(File.ReadAllText(path));

        /// <summary>Deep-clones one polymorphic block through the shared document codec.</summary>
        public static BattleBlock CloneBlock(BattleBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            var wrapper = new BattleFlowDocument
            {
                CaseId = "clone",
                Blocks = new System.Collections.Generic.List<BattleBlock> { block },
            };
            return Parse(Serialize(wrapper)).Blocks[0];
        }

        /// <summary>Serializes a reusable battle scene asset.</summary>
        public static string SerializeScene(BattleSceneDocument scene) =>
            JsonConvert.SerializeObject(scene, Settings);

        /// <summary>Parses a reusable battle scene asset.</summary>
        public static BattleSceneDocument ParseScene(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Battle scene JSON is empty.", nameof(json));
            return JsonConvert.DeserializeObject<BattleSceneDocument>(json, Settings)
                   ?? throw new InvalidDataException("Battle scene JSON did not contain an object.");
        }

        /// <summary>Writes a reusable scene asset.</summary>
        public static void SaveScene(string path, BattleSceneDocument scene) =>
            File.WriteAllText(path, SerializeScene(scene));

        /// <summary>Loads a reusable scene asset.</summary>
        public static BattleSceneDocument LoadScene(string path) => ParseScene(File.ReadAllText(path));

        /// <summary>Resolves a scene reference relative to its owning .battleflow file.</summary>
        public static string ResolveScenePath(string casePath, string scenarioRef)
        {
            if (string.IsNullOrWhiteSpace(casePath)) throw new ArgumentException("Case path is required.", nameof(casePath));
            if (string.IsNullOrWhiteSpace(scenarioRef))
                throw new ArgumentException("Scenario reference is required.", nameof(scenarioRef));

            var reference = scenarioRef;
            if (string.IsNullOrEmpty(Path.GetExtension(reference))) reference += ".battlescene";
            if (Path.IsPathRooted(reference)) return Path.GetFullPath(reference);
            var ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(casePath)) ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(ownerDirectory, reference));
        }
    }
}
