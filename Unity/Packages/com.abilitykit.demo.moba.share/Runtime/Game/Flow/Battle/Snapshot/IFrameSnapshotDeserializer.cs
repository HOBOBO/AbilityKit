using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 帧快照反序列化器接口
    /// 定义将原始快照数据转换为平台无关格式的契约
    /// 
    /// 这是 Share 层定义的核心接口，由各个平台实现
    /// 负责将协议层的二进制/结构化数据转换为 Share 层的 FrameSnapshotData
    /// </summary>
    public interface IFrameSnapshotDeserializer
    {
        /// <summary>
        /// 反序列化进入游戏快照
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeEnterGame(byte[] rawData, out EnterGameData result);

        /// <summary>
        /// 反序列化角色变换快照
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeActorTransform(byte[] rawData, out ActorTransformData[] result);

        /// <summary>
        /// 反序列化弹道事件快照
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeProjectileEvent(byte[] rawData, out ProjectileEventData[] result);

        /// <summary>
        /// 反序列化区域事件快照
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeAreaEvent(byte[] rawData, out AreaEventData[] result);

        /// <summary>
        /// 反序列化伤害事件快照
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeDamageEvent(byte[] rawData, out DamageEventData[] result);

        /// <summary>
        /// 反序列化状态哈希
        /// </summary>
        /// <param name="rawData">原始数据</param>
        /// <param name="result">解析结果</param>
        /// <returns>是否成功</returns>
        bool TryDeserializeStateHash(byte[] rawData, out StateHashData result);
    }

    /// <summary>
    /// 帧快照组装器接口
    /// 定义组装完整帧快照的契约
    /// </summary>
    public interface IFrameSnapshotAssembler
    {
        /// <summary>
        /// 开始组装新帧
        /// </summary>
        /// <param name="frameIndex">帧索引</param>
        void BeginFrame(int frameIndex);

        /// <summary>
        /// 设置进入游戏数据
        /// </summary>
        void SetEnterGame(in EnterGameData data);

        /// <summary>
        /// 添加角色变换数据
        /// </summary>
        void AddActorTransform(in ActorTransformData data);

        /// <summary>
        /// 添加弹道事件数据
        /// </summary>
        void AddProjectileEvent(in ProjectileEventData data);

        /// <summary>
        /// 添加区域事件数据
        /// </summary>
        void AddAreaEvent(in AreaEventData data);

        /// <summary>
        /// 添加伤害事件数据
        /// </summary>
        void AddDamageEvent(in DamageEventData data);

        /// <summary>
        /// 设置状态哈希
        /// </summary>
        void SetStateHash(in StateHashData data);

        /// <summary>
        /// 完成组装，获取完整快照
        /// </summary>
        /// <param name="snapshot">输出快照</param>
        /// <returns>是否有有效数据</returns>
        bool TryFinishFrame(out FrameSnapshotData snapshot);

        /// <summary>
        /// 重置组装器
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 帧快照分发器接口
    /// 定义分发快照事件的契约
    /// </summary>
    public interface IFrameSnapshotDispatcher
    {
        /// <summary>
        /// 分发进入游戏事件
        /// </summary>
        void DispatchEnterGame(int frameIndex, in EnterGameData data);

        /// <summary>
        /// 分发角色变换事件
        /// </summary>
        void DispatchActorTransform(int frameIndex, in ActorTransformData[] data);

        /// <summary>
        /// 分发弹道事件
        /// </summary>
        void DispatchProjectileEvent(int frameIndex, in ProjectileEventData[] data);

        /// <summary>
        /// 分发区域事件
        /// </summary>
        void DispatchAreaEvent(int frameIndex, in AreaEventData[] data);

        /// <summary>
        /// 分发伤害事件
        /// </summary>
        void DispatchDamageEvent(int frameIndex, in DamageEventData[] data);
 
        /// <summary>
        /// 分发表现 Cue 事件
        /// </summary>
        void DispatchPresentationCue(int frameIndex, in PresentationCueData[] data);
 
        /// <summary>
        /// 分发状态哈希
        /// </summary>
        void DispatchStateHash(int frameIndex, in StateHashData data);

        /// <summary>
        /// Dispatches platform-neutral actor removal data.
        /// </summary>
        void DispatchActorDespawn(int frameIndex, in ActorDespawnData[] data);

        /// <summary>
        /// Dispatches platform-neutral skill presentation states.
        /// </summary>
        void DispatchSkillState(int frameIndex, in SkillStateData[] data);
    }
}
