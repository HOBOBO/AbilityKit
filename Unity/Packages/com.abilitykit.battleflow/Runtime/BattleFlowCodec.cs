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
    }
}
