using System;

namespace AbilityKit.Triggering.Runtime.Abstractions
{
    /// <summary>
    /// 触发器配置接口（只读，可序列化）
    /// 包内定义，包外实现具体序列化逻辑
    /// </summary>
    public interface ITriggerConfig
    {
        /// <summary>触发器 ID</summary>
        int TriggerId { get; }

        /// <summary>是否启用</summary>
        bool IsEnabled { get; }

        /// <summary>优先级（数值越小越优先）</summary>
        int Priority { get; }

        /// <summary>调度阶段</summary>
        int Phase { get; }

        /// <summary>导出当前状态（用于网络同步/快照）</summary>
        /// <returns>序列化的状态字节数组</returns>
        byte[] ExportState();

        /// <summary>导入状态（用于网络同步/回滚）</summary>
        /// <param name="data">序列化的状态数据</param>
        /// <param name="isAuthoritative">是否权威数据（服务端）</param>
        void ImportState(byte[] data, bool isAuthoritative);
    }
}
