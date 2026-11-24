using System;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 会话出生服务接口。
    ///
    /// 设计：
    /// - 从 SessionCoordinator 中抽象玩家出生逻辑。
    /// - 由游戏项目（moba.runtime）实现，用于创建实体。
    /// - 保持 Coordinator 包与具体游戏无关。
    /// </summary>
    public interface ISpawnService : IService
    {
        /// <summary>
        /// 根据出生数据创建玩家实体。
        /// </summary>
        /// <param name="spawns">来自主机的玩家出生数据</param>
        /// <returns>如果成功创建出生实体，则返回 true</returns>
        bool CreateSpawns(PlayerSpawnData[] spawns);
    }
}
