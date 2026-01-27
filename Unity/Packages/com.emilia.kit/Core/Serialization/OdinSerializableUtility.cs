using System;
using System.Text;
using Sirenix.Serialization;

namespace Emilia.Kit
{
    public static class OdinSerializableUtility
    {
        public static byte[] ToByte<T>(T value) => TagSerializationUtility.IgnoreTagSerializeValue(value, DataFormat.Binary, SerializeTagDefine.DefaultIgnoreTag);

        public static T FromByte<T>(byte[] bytes)
        {
            try
            {
                return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary);
            }
            catch
            {
                return default;
            }
        }

        public static string ToByteString<T>(T value)
        {
            byte[] bytes = TagSerializationUtility.IgnoreTagSerializeValue(value, DataFormat.Binary, SerializeTagDefine.DefaultIgnoreTag);
            return Convert.ToBase64String(bytes);
        }

        public static T FromByteString<T>(string byteString)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(byteString);
                return FromByte<T>(bytes);
            }
            catch
            {
                return default;
            }
        }

        public static byte[] ToJsonBytes<T>(T value) => TagSerializationUtility.IgnoreTagSerializeValue(value, DataFormat.JSON, SerializeTagDefine.DefaultIgnoreTag);

        public static T FromJsonBytes<T>(byte[] bytes)
        {
            try
            {
                return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.JSON);
            }
            catch
            {
                return default;
            }
        }

        public static string ToJsonString<T>(T value)
        {
            byte[] bytes = TagSerializationUtility.IgnoreTagSerializeValue(value, DataFormat.JSON, SerializeTagDefine.DefaultIgnoreTag);
            return Encoding.UTF8.GetString(bytes);
        }

        public static T FromJsonString<T>(string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                return FromJsonBytes<T>(bytes);
            }
            catch
            {
                return default;
            }
        }
    }
}