#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 基于 JSON 的配置提供器
    /// </summary>
    public sealed class JsonConfigProvider : IConfigProvider
    {
        // Unity 不提供 System.Text.Json，示例统一走 Newtonsoft.Json。
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Converters = { new StringEnumConverter() }
        });

        private readonly JObject _document;

        private JsonConfigProvider(JObject document)
        {
            _document = document;
        }

        /// <summary>
        /// 从字符串创建
        /// </summary>
        public static JsonConfigProvider FromString(string json)
        {
            return new JsonConfigProvider(JObject.Parse(json));
        }

        public T GetSection<T>(string sectionName) where T : class, new()
        {
            var result = GetSectionOrDefault<T>(sectionName);
            return result ?? new T();
        }

        /// <summary>
        /// 获取配置节（可返回 null）
        /// </summary>
        public T? GetSectionOrDefault<T>(string sectionName) where T : class
        {
            return _document.TryGetValue(sectionName, out var section)
                ? section.ToObject<T>(Serializer)
                : null;
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (!_document.TryGetValue(key, out var token))
            {
                return defaultValue;
            }

            try
            {
                return token.ToObject<T>(Serializer);
            }
            catch
            {
                return defaultValue;
            }
        }

        public Dictionary<string, T> GetDictionary<T>(string sectionName) where T : class, new()
        {
            var result = new Dictionary<string, T>();

            if (!_document.TryGetValue(sectionName, out var section) || !(section is JObject obj))
            {
                return result;
            }

            foreach (var property in obj.Properties())
            {
                var item = property.Value.ToObject<T>(Serializer);
                if (item != null)
                {
                    result[property.Name] = item;
                }
            }

            return result;
        }

        public bool HasSection(string sectionName) => _document.ContainsKey(sectionName);

        public bool HasKey(string key) => _document.ContainsKey(key);

        public void Dispose()
        {
            // JObject 不持有非托管资源，保留该方法以维持提供器可释放的调用形态。
        }
    }
}
