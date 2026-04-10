using System;
using System.Text;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 默认标签序列化器，基于字符串名称进行序列化。
    /// </summary>
    public sealed class DefaultTagSerializer : ITagSerializer
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static DefaultTagSerializer Instance { get; } = new DefaultTagSerializer();

        private DefaultTagSerializer()
        {
        }

        /// <summary>
        /// 序列化单个标签
        /// </summary>
        public string Serialize(GameplayTag tag)
        {
            if (!tag.IsValid) return string.Empty;
            return GameplayTagManager.Instance.GetName(tag);
        }

        /// <summary>
        /// 反序列化单个标签
        /// </summary>
        public GameplayTag Deserialize(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return default;
            if (GameplayTagManager.Instance.TryGetTag(data, out var tag))
            {
                return tag;
            }
            return GameplayTagManager.Instance.RequestTag(data);
        }

        /// <summary>
        /// 序列化标签容器
        /// </summary>
        public string SerializeContainer(GameplayTagContainer container)
        {
            if (container == null || container.IsEmpty) return "[]";

            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var tag in container)
            {
                if (!first) sb.Append(',');
                sb.Append('"');
                sb.Append(Serialize(tag).Replace("\"", "\\\""));
                sb.Append('"');
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// 反序列化标签容器
        /// </summary>
        public GameplayTagContainer DeserializeContainer(string data)
        {
            var container = new GameplayTagContainer();
            if (string.IsNullOrWhiteSpace(data)) return container;

            var trimmed = data.Trim();
            if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']')) return container;

            var content = trimmed.Substring(1, trimmed.Length - 2);
            if (string.IsNullOrWhiteSpace(content)) return container;

            var names = content.Split(new[] { ',' }, StringSplitOptions.None);
            foreach (var name in names)
            {
                var trimmedName = name.Trim().Trim('"').Replace("\\\"", "\"");
                if (!string.IsNullOrEmpty(trimmedName))
                {
                    container.Add(Deserialize(trimmedName));
                }
            }

            return container;
        }
    }
}
