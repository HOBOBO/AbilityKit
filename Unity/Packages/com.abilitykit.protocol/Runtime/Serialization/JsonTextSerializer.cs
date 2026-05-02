#if UNITY_EDITOR || USE_NEWTONSOFT_JSON
using System;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace AbilityKit.Protocol.Serialization
{
    /// <summary>
    /// 基于 Newtonsoft.Json 的文本序列化实现
    /// </summary>
    public sealed class JsonTextSerializer : ITextSerializer
    {
        private readonly JsonSerializerSettings _settings;
        private readonly JsonSerializer _serializer;

        public JsonTextSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            _serializer = JsonSerializer.Create(_settings);
        }

        public JsonTextSerializer(JsonSerializerSettings settings)
        {
            _settings = settings ?? new JsonSerializerSettings();
            _serializer = JsonSerializer.Create(_settings);
        }

        public string Serialize<T>(T value, bool prettyPrint = false)
        {
            if (value == null) return null;
            var formatting = prettyPrint ? Formatting.Indented : Formatting.None;
            return JsonConvert.SerializeObject(value, formatting, _settings);
        }

        public T Deserialize<T>(string text)
        {
            if (string.IsNullOrEmpty(text)) return default;
            return JsonConvert.DeserializeObject<T>(text, _settings);
        }
    }
}
#endif
