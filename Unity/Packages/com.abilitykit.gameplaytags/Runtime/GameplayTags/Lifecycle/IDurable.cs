using System;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 可持续对象接口，用于管理带有生命周期效果的标签。
    /// </summary>
    public interface IDurable
    {
        /// <summary>
        /// 拥有者 ID
        /// </summary>
        int OwnerId { get; }

        /// <summary>
        /// 类型标识
        /// </summary>
        string Kind { get; }

        /// <summary>
        /// 是否已暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 是否已停止
        /// </summary>
        bool IsStopped { get; }

        /// <summary>
        /// 是否已移除
        /// </summary>
        bool IsRemoved { get; }

        /// <summary>
        /// 暂停
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止
        /// </summary>
        void Stop();

        /// <summary>
        /// 移除
        /// </summary>
        void Remove();
    }
}
