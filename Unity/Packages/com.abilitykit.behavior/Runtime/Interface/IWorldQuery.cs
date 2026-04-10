using System.Collections.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 世界查询接口
    /// 提供行为所需的世界状态查询
    /// 
    /// 注意：此接口完全独立于 Triggering 模块
    /// 由业务层实现，整合所需的数据源
    /// </summary>
    public interface IWorldQuery
    {
        /// <summary>
        /// 获取实体位置
        /// </summary>
        Vec3 GetPosition(BehaviorEntityId id);
        
        /// <summary>
        /// 设置实体位置
        /// </summary>
        void SetPosition(BehaviorEntityId id, Vec3 position);
        
        /// <summary>
        /// 获取实体朝向
        /// </summary>
        Vec3 GetForward(BehaviorEntityId id);
        
        /// <summary>
        /// 设置实体朝向
        /// </summary>
        void SetForward(BehaviorEntityId id, Vec3 forward);
        
        /// <summary>
        /// 获取两个实体间的距离
        /// </summary>
        float GetDistance(BehaviorEntityId a, BehaviorEntityId b);
        
        /// <summary>
        /// 获取实体到目标位置的距离
        /// </summary>
        float GetDistanceToPosition(BehaviorEntityId entityId, Vec3 position);
        
        /// <summary>
        /// 检查实体是否存在
        /// </summary>
        bool EntityExists(BehaviorEntityId id);
        
        /// <summary>
        /// 获取数据
        /// </summary>
        T GetData<T>(BehaviorEntityId id, string key, T defaultValue = default);
        
        /// <summary>
        /// 设置数据
        /// </summary>
        void SetData<T>(BehaviorEntityId id, string key, T value);
        
        /// <summary>
        /// 检查是否有数据
        /// </summary>
        bool HasData(BehaviorEntityId id, string key);
    }
}
