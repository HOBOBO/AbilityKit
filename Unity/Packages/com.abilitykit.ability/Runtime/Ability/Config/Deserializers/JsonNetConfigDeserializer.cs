using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// Newtonsoft.Json 配置反序列化器
    /// </summary>
    public sealed class JsonNetConfigDeserializer : ConfigDeserializerBase
    {
        public static readonly JsonNetConfigDeserializer Instance = new JsonNetConfigDeserializer();

        private JsonNetConfigDeserializer() { }

        public override Array DeserializeBytes(byte[] bytes, Type targetType)
        {
            throw CreateNotSupportedException(targetType, nameof(JsonNetConfigDeserializer));
        }

        public override Array DeserializeText(string text, Type targetType)
        {
            if (string.IsNullOrEmpty(text)) return Array.CreateInstance(targetType, 0);
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            var token = JToken.Parse(text);
            if (token is not JArray array) return Array.CreateInstance(targetType, 0);

            var list = new System.Collections.Generic.List<object>();
            foreach (var item in array)
            {
                try
                {
                    var obj = item.ToObject(targetType);
                    if (obj != null)
                    {
                        list.Add(obj);
                    }
                }
                catch
                {
                }
            }

            var result = Array.CreateInstance(targetType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                result.SetValue(list[i], i);
            }
            return result;
        }

        public override bool CanHandle(Type targetType)
        {
            return targetType != null;
        }
    }
}
