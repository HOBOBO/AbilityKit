using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 调度任务接口。
    /// 表示一个可被调度和管理的任务。
    ///
    /// 设计原则：
    /// - 无 GC：所有成员均为值类型或接口回调
    /// - 可中断：支持通过 RequestCancel 中断
    /// - 可查询：提供状态和进度查询
    /// </summary>
    public interface IScheduledTask
    {
        /// <summary>任务名称</summary>
        string Name { get; set; }

        /// <summary>当前状态</summary>
        TaskState State { get; }

        /// <summary>是否已完成</summary>
        bool IsCompleted { get; }

        /// <summary>是否已取消</summary>
        bool IsCanceled { get; }

        /// <summary>是否已超时</summary>
        bool IsTimedOut { get; }

        /// <summary>取消原因</summary>
        string CancelReason { get; }

        /// <summary>开始时间戳</summary>
        float StartTime { get; set; }

        /// <summary>已用时间（秒）</summary>
        float ElapsedTime { get; }

        /// <summary>持续时间（秒），-1 表示无限</summary>
        float Duration { get; }

        /// <summary>请求取消任务</summary>
        void RequestCancel(string reason = null);

        /// <summary>更新任务进度</summary>
        void Update(float deltaTime);

        /// <summary>立即完成（用于外部控制结束）</summary>
        void Complete();
    }
}
