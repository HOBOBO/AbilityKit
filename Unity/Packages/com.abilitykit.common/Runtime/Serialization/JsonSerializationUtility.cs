using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AbilityKit.Common.Serialization
{
    /// <summary>
    /// 通用的 Newtonsoft.Json 序列化辅助类，提供强类型的对象转换支持
    /// </summary>
    public static class JsonSerializationUtility
    {
        #region 默认设置

        private static readonly JsonSerializerSettings DefaultSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            StringEscapeHandling = StringEscapeHandling.Default,
        };

        private static readonly JsonSerializerSettings CompactSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        private static readonly JsonSerializerSettings TypeNameSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        };

        #endregion

        #region 基础序列化/反序列化

        /// <summary>
        /// 将对象序列化为 JSON 字符串（格式化输出）
        /// </summary>
        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, DefaultSettings);
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串（紧凑输出，无缩进）
        /// </summary>
        public static string SerializeCompact<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, CompactSettings);
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串（包含类型信息）
        /// </summary>
        public static string SerializeWithType<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, TypeNameSettings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为强类型对象
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default;
            return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为强类型对象（包含类型信息）
        /// </summary>
        public static T DeserializeWithType<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default;
            return JsonConvert.DeserializeObject<T>(json, TypeNameSettings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定类型
        /// </summary>
        public static object Deserialize(string json, Type type)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            return JsonConvert.DeserializeObject(json, type, DefaultSettings);
        }

        #endregion

        #region 文件操作

        /// <summary>
        /// 将对象序列化并保存到文件
        /// </summary>
        public static void SerializeToFile<T>(T obj, string filePath, bool prettyPrint = true)
        {
            var json = prettyPrint ? Serialize(obj) : SerializeCompact(obj);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 从文件读取并反序列化为强类型对象
        /// </summary>
        public static T DeserializeFromFile<T>(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return Deserialize<T>(json);
        }

        /// <summary>
        /// 尝试从文件读取并反序列化，失败时返回默认值
        /// </summary>
        public static T DeserializeFromFileOrDefault<T>(string filePath, T defaultValue = default)
        {
            try
            {
                if (!File.Exists(filePath))
                    return defaultValue;
                return DeserializeFromFile<T>(filePath);
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region JToken 操作

        /// <summary>
        /// 将 JSON 字符串解析为 JToken
        /// </summary>
        public static JToken Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            return JToken.Parse(json);
        }

        /// <summary>
        /// 将对象转换为 JToken
        /// </summary>
        public static JToken ToJToken(object obj)
        {
            return JToken.FromObject(obj, JsonSerializer.CreateDefault(DefaultSettings));
        }

        /// <summary>
        /// 将 JToken 转换为指定类型
        /// </summary>
        public static T ToObject<T>(JToken token)
        {
            if (token == null)
                return default;
            return token.ToObject<T>(JsonSerializer.CreateDefault(DefaultSettings));
        }

        /// <summary>
        /// 将 JToken 转换为指定类型（使用 JsonSerializer）
        /// </summary>
        public static T ToObject<T>(JToken token, JsonSerializer serializer)
        {
            if (token == null)
                return default;
            return token.ToObject<T>(serializer);
        }

        #endregion

        #region 泛型容器序列化

        /// <summary>
        /// 序列化字典为 JSON
        /// </summary>
        public static string SerializeDictionary<TValue>(Dictionary<string, TValue> dict)
        {
            return Serialize(dict);
        }

        /// <summary>
        /// 反序列化 JSON 为字典
        /// </summary>
        public static Dictionary<string, TValue> DeserializeDictionary<TValue>(string json)
        {
            return Deserialize<Dictionary<string, TValue>>(json);
        }

        /// <summary>
        /// 序列化列表为 JSON
        /// </summary>
        public static string SerializeList<T>(IList<T> list)
        {
            return Serialize(list);
        }

        /// <summary>
        /// 反序列化 JSON 为列表
        /// </summary>
        public static List<T> DeserializeList<T>(string json)
        {
            return Deserialize<List<T>>(json);
        }

        #endregion

        #region Unity 特定类型支持

        /// <summary>
        /// 序列化 Vector2
        /// </summary>
        public static string SerializeVector2(Vector2 v)
        {
            return Serialize(new Vector2Dto { x = v.x, y = v.y });
        }

        /// <summary>
        /// 反序列化 Vector2
        /// </summary>
        public static Vector2 DeserializeVector2(string json)
        {
            var dto = Deserialize<Vector2Dto>(json);
            return dto != null ? new Vector2(dto.x, dto.y) : Vector2.zero;
        }

        /// <summary>
        /// 序列化 Vector3
        /// </summary>
        public static string SerializeVector3(Vector3 v)
        {
            return Serialize(new Vector3Dto { x = v.x, y = v.y, z = v.z });
        }

        /// <summary>
        /// 反序列化 Vector3
        /// </summary>
        public static Vector3 DeserializeVector3(string json)
        {
            var dto = Deserialize<Vector3Dto>(json);
            return dto != null ? new Vector3(dto.x, dto.y, dto.z) : Vector3.zero;
        }

        /// <summary>
        /// 序列化 Vector4
        /// </summary>
        public static string SerializeVector4(Vector4 v)
        {
            return Serialize(new Vector4Dto { x = v.x, y = v.y, z = v.z, w = v.w });
        }

        /// <summary>
        /// 反序列化 Vector4
        /// </summary>
        public static Vector4 DeserializeVector4(string json)
        {
            var dto = Deserialize<Vector4Dto>(json);
            return dto != null ? new Vector4(dto.x, dto.y, dto.z, dto.w) : Vector4.zero;
        }

        /// <summary>
        /// 序列化 Quaternion
        /// </summary>
        public static string SerializeQuaternion(Quaternion q)
        {
            return Serialize(new QuaternionDto { x = q.x, y = q.y, z = q.z, w = q.w });
        }

        /// <summary>
        /// 反序列化 Quaternion
        /// </summary>
        public static Quaternion DeserializeQuaternion(string json)
        {
            var dto = Deserialize<QuaternionDto>(json);
            return dto != null ? new Quaternion(dto.x, dto.y, dto.z, dto.w) : Quaternion.identity;
        }

        /// <summary>
        /// 序列化 Color
        /// </summary>
        public static string SerializeColor(Color c)
        {
            return Serialize(new ColorDto { r = c.r, g = c.g, b = c.b, a = c.a });
        }

        /// <summary>
        /// 反序列化 Color
        /// </summary>
        public static Color DeserializeColor(string json)
        {
            var dto = Deserialize<ColorDto>(json);
            return dto != null ? new Color(dto.r, dto.g, dto.b, dto.a) : Color.white;
        }

        /// <summary>
        /// 序列化 Bounds
        /// </summary>
        public static string SerializeBounds(Bounds b)
        {
            return Serialize(new BoundsDto
            {
                center = new Vector3Dto { x = b.center.x, y = b.center.y, z = b.center.z },
                extents = new Vector3Dto { x = b.extents.x, y = b.extents.y, z = b.extents.z }
            });
        }

        /// <summary>
        /// 反序列化 Bounds
        /// </summary>
        public static Bounds DeserializeBounds(string json)
        {
            var dto = Deserialize<BoundsDto>(json);
            if (dto == null) return new Bounds();
            return new Bounds(
                new Vector3(dto.center.x, dto.center.y, dto.center.z),
                new Vector3(dto.extents.x, dto.extents.y, dto.extents.z) * 2
            );
        }

        #endregion

        #region 深度复制

        /// <summary>
        /// 使用 JSON 序列化进行深度复制
        /// </summary>
        public static T DeepClone<T>(T obj)
        {
            if (obj == null) return default;
            var json = Serialize(obj);
            return Deserialize<T>(json);
        }

        #endregion

        #region 验证

        /// <summary>
        /// 验证 JSON 字符串是否有效
        /// </summary>
        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;
            try
            {
                JToken.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region DTO 内部类

        [Serializable]
        private class Vector2Dto
        {
            public float x;
            public float y;
        }

        [Serializable]
        private class Vector3Dto
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private class Vector4Dto
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        private class QuaternionDto
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        private class ColorDto
        {
            public float r;
            public float g;
            public float b;
            public float a;
        }

        [Serializable]
        private class BoundsDto
        {
            public Vector3Dto center;
            public Vector3Dto extents;
        }

        #endregion
    }

    #region 自定义 JsonConverter 示例

    /// <summary>
    /// Unity Vector2 的 JsonConverter
    /// </summary>
    public class Vector2JsonConverter : JsonConverter<Vector2>
    {
        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector2.zero;

            var token = JToken.Load(reader);
            return new Vector2(
                token["x"]?.Value<float>() ?? 0f,
                token["y"]?.Value<float>() ?? 0f
            );
        }

        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Unity Vector3 的 JsonConverter
    /// </summary>
    public class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector3.zero;

            var token = JToken.Load(reader);
            return new Vector3(
                token["x"]?.Value<float>() ?? 0f,
                token["y"]?.Value<float>() ?? 0f,
                token["z"]?.Value<float>() ?? 0f
            );
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Unity Quaternion 的 JsonConverter
    /// </summary>
    public class QuaternionJsonConverter : JsonConverter<Quaternion>
    {
        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Quaternion.identity;

            var token = JToken.Load(reader);
            return new Quaternion(
                token["x"]?.Value<float>() ?? 0f,
                token["y"]?.Value<float>() ?? 0f,
                token["z"]?.Value<float>() ?? 0f,
                token["w"]?.Value<float>() ?? 1f
            );
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WritePropertyName("w");
            writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Unity Color 的 JsonConverter
    /// </summary>
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Color.white;

            var token = JToken.Load(reader);

            // 支持 #RRGGBBAA 格式
            var hex = token["hex"]?.Value<string>();
            if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#"))
            {
                return ParseHexColor(hex);
            }

            return new Color(
                token["r"]?.Value<float>() ?? 1f,
                token["g"]?.Value<float>() ?? 1f,
                token["b"]?.Value<float>() ?? 1f,
                token["a"]?.Value<float>() ?? 1f
            );
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(value.r);
            writer.WritePropertyName("g");
            writer.WriteValue(value.g);
            writer.WritePropertyName("b");
            writer.WriteValue(value.b);
            writer.WritePropertyName("a");
            writer.WriteValue(value.a);
            writer.WriteEndObject();
        }

        private static Color ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            byte r, g, b, a = 255;

            if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }
            else if (hex.Length == 8)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
                a = Convert.ToByte(hex.Substring(6, 2), 16);
            }
            else
            {
                return Color.white;
            }

            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }

    #endregion

    #region 高级设置

    /// <summary>
    /// 创建包含 Unity 类型支持的 JsonSerializerSettings
    /// </summary>
    public static JsonSerializerSettings CreateUnitySettings()
    {
        return new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new Vector2JsonConverter(),
                new Vector3JsonConverter(),
                new QuaternionJsonConverter(),
                new ColorJsonConverter()
            }
        };
    }

    /// <summary>
    /// 创建包含 Unity 类型支持的 JsonSerializer
    /// </summary>
    public static JsonSerializer CreateUnitySerializer()
    {
        return JsonSerializer.CreateDefault(CreateUnitySettings());
    }

    #endregion
}
