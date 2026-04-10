using System;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签序列化器接口，用于持久化标签数据。
    /// </summary>
    public interface ITagSerializer
    {
        /// <summary>
        /// 序列化单个标签
        /// </summary>
        string Serialize(GameplayTag tag);

        /// <summary>
        /// 反序列化单个标签
        /// </summary>
        GameplayTag Deserialize(string data);

        /// <summary>
        /// 序列化标签容器
        /// </summary>
        string SerializeContainer(GameplayTagContainer container);

        /// <summary>
        /// 反序列化标签容器
        /// </summary>
        GameplayTagContainer DeserializeContainer(string data);
    }
}
