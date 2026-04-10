using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 简单一次性任务接口。
    /// 用于不需要完整状态管理的场景。
    /// </summary>
    public interface ISimpleTask
    {
        /// <summary>是否已完成</summary>
        bool IsDone { get; }

        /// <summary>执行任务</summary>
        void Execute();

        /// <summary>更新任务</summary>
        void Update(float deltaTime);
    }
}
