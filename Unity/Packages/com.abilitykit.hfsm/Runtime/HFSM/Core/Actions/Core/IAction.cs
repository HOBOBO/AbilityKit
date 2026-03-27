using System.Collections;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 所有行为的基类接口
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// 行为名称（用于编辑器显示）
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 执行行为，返回执行状态
        /// </summary>
        BehaviorStatus Execute(BehaviorContext context);

        /// <summary>
        /// 重置行为状态（当行为所属状态进入时调用）
        /// </summary>
        void Reset();

        /// <summary>
        /// 强制终止行为（当行为所属状态退出时调用）
        /// </summary>
        void ForceEnd();
    }

    /// <summary>
    /// 支持协程的行为基类
    /// </summary>
    public interface IYieldAction : IAction
    {
        /// <summary>
        /// 获取协程枚举器
        /// </summary>
        IEnumerator GetYieldEnumerator(BehaviorContext context);

        /// <summary>
        /// 当前是否正在等待协程完成
        /// </summary>
        bool IsWaitingForCoroutine { get; }
    }
}
