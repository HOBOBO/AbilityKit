using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    /// <summary>
    /// 在宿主调度线程上管理世界的生命周期，不保证并发访问安全。
    /// </summary>
    public interface IWorldManager
    {
        /// <summary>
        /// 已初始化且尚未摘除的世界，不包含正在创建的实例。
        /// </summary>
        IReadOnlyDictionary<WorldId, IWorld> Worlds { get; }

        /// <summary>
        /// 接管工厂返回的新实例，校验身份并初始化后登记；批量关闭期间拒绝创建。
        /// 接管后的创建失败会尝试释放一次，不会释放工厂误返回的已持有实例。
        /// </summary>
        /// <exception cref="AggregateException">创建后续步骤及补偿释放都失败，按创建异常、释放异常的顺序保留原因。</exception>
        IWorld Create(WorldCreateOptions options);

        /// <summary>
        /// 查找尚未摘除的已登记世界。
        /// </summary>
        bool TryGet(WorldId id, out IWorld world);

        /// <summary>
        /// 先摘除指定世界，再释放；找不到时返回假，释放异常向上传播且不恢复登记。
        /// </summary>
        bool Destroy(WorldId id);

        /// <summary>
        /// 推进已登记世界；世界的帧回调不得修改登记集合。
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 先摘除本次已登记的全部世界，再逐个尝试释放。关闭回调内再次调用会直接返回，结束后可复用。
        /// </summary>
        /// <exception cref="AggregateException">至少一个世界释放失败；每条失败标注对应世界 ID 并保留原异常。</exception>
        void DisposeAll();
    }
}
