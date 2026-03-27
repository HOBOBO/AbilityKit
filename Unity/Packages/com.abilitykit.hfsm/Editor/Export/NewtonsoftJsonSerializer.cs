using System;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityHFSM.Editor.Export
{
    /// <summary>
    /// Newtonsoft JSON (Json.NET) 序列化器
    /// 支持更多功能如小写属性名、空值忽略等
    /// </summary>
    public class NewtonsoftJsonSerializer : IJsonSerializer
    {
        private readonly JsonSerializerSettings _defaultSettings;
        private readonly JsonSerializerSettings _prettyPrintSettings;

        public string Name => "Newtonsoft JSON";

        public NewtonsoftJsonSerializer()
        {
            _defaultSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.None
            };

            _prettyPrintSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.None
            };
        }

        public string Serialize<T>(T obj, bool prettyPrint = false) where T : class
        {
            if (obj == null)
                return "null";

            var settings = prettyPrint ? _prettyPrintSettings : _defaultSettings;
            return JsonConvert.SerializeObject(obj, settings);
        }

        public T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonConvert.DeserializeObject<T>(json, _defaultSettings);
        }
    }
}
