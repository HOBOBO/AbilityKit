using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 可持续对象注册表接口。
    /// </summary>
    public interface IDurableRegistry
    {
        /// <summary>
        /// 注册可持续对象
        /// </summary>
        void Register(IDurable durable);

        /// <summary>
        /// 取消注册可持续对象
        /// </summary>
        bool Unregister(IDurable durable);

        /// <summary>
        /// 获取指定拥有者的所有可持续对象
        /// </summary>
        IReadOnlyList<IDurable> GetByOwner(int ownerId);
    }
}
