using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Abstractions
{
    /// <summary>
    /// 世界的最小运行时契约。实现必须能释放未初始化或部分初始化的实例，且不能重复释放已完成补偿的资源。
    /// </summary>
    public interface IWorld : IDisposable
    {
        /// <summary>
        /// 世界的稳定身份，必须与创建选项中的 ID 一致。
        /// </summary>
        WorldId Id { get; }

        /// <summary>
        /// 用于区分世界实现的类型标识。
        /// </summary>
        string WorldType { get; }

        /// <summary>
        /// 世界初始化完成后提供的服务解析器。
        /// </summary>
        IWorldResolver Services { get; }

        /// <summary>
        /// 完成世界装配；成功后才可登记并接受帧推进。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 按宿主提供的逻辑时间增量推进一帧。
        /// </summary>
        void Tick(float deltaTime);
    }
}
