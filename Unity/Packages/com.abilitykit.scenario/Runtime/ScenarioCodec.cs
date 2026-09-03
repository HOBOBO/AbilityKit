using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.Scenario
{
    /// <summary>
    /// 玩法中立场景 IR 的 Json.NET 编解码（Unity 与 .NET 都可用，保持 shell-out 的 JSON 一致）。
    /// 用于「编辑器 shell-out 到 headless 命令」的往返：编辑器序列化 TestScenario → 写文件 → headless 命令读取并运行。
    /// <see cref="TestScenario.Expectations"/> 是 opaque（编辑器场景无断言时为 null，序列化时省略）。
    /// </summary>
    public static class ScenarioCodec
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        /// <summary>序列化为 JSON。</summary>
        public static string Serialize(TestScenario scenario) => JsonConvert.SerializeObject(scenario, Settings);

        /// <summary>从 JSON 反序列化。</summary>
        public static TestScenario Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Scenario JSON is empty.", nameof(json));
            return JsonConvert.DeserializeObject<TestScenario>(json, Settings)
                   ?? throw new InvalidDataException("Scenario JSON did not contain an object.");
        }

        /// <summary>从文件读取。</summary>
        public static TestScenario Load(string path) => Parse(File.ReadAllText(path));

        /// <summary>写到文件。</summary>
        public static void Save(string path, TestScenario scenario) => File.WriteAllText(path, Serialize(scenario));
    }
}
