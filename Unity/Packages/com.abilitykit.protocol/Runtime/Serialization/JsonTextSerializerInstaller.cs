#if UNITY_EDITOR || USE_NEWTONSOFT_JSON
using AbilityKit.Protocol.Serialization;
using Newtonsoft.Json;

namespace AbilityKit.Protocol.Serialization
{
    /// <summary>
    /// JSON 文本序列化器安装器
    /// </summary>
    public static class JsonTextSerializerInstaller
    {
        /// <summary>
        /// 安装 JSON 文本序列化器为当前实现
        /// </summary>
        public static void InstallAsCurrent()
        {
            WireSerializer.TextSerializer = new JsonTextSerializer();
        }

        /// <summary>
        /// 使用自定义设置安装 JSON 文本序列化器
        /// </summary>
        public static void InstallAsCurrent(JsonSerializerSettings settings)
        {
            WireSerializer.TextSerializer = new JsonTextSerializer(settings);
        }
    }
}
#endif
