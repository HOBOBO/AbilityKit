using System.Collections;
using System.Collections.Generic;

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
    /// Lifecycle state exposed by the optional action runtime instrumentation layer.
    /// </summary>
    public enum ActionRuntimeStatus
    {
        Inactive,
        Running,
        Success,
        Failure,
        Cancelled
    }

    /// <summary>
    /// Read-only runtime information for one action instance.
    /// </summary>
    public interface IActionRuntimeStateSource
    {
        string RuntimeId { get; }
        string ParentRuntimeId { get; }
        string Name { get; }
        string TypeName { get; }
        ActionRuntimeStatus RuntimeStatus { get; }
        bool IsActive { get; }
        int ExecutionCount { get; }
        float ElapsedTime { get; }
    }

    /// <summary>
    /// Implemented by states that expose their instrumented action tree.
    /// </summary>
    public interface IActionRuntimeStateProvider
    {
        IEnumerable<IActionRuntimeStateSource> GetActionRuntimeStates();
    }

    /// <summary>
    /// A behavior node that owns an ordered list of child behaviors.
    /// </summary>
    public interface ICompositeAction : IAction
    {
        void AddChild(IAction child);
    }

    /// <summary>
    /// A behavior node that wraps exactly one child behavior.
    /// </summary>
    public interface IDecoratorAction : IAction
    {
        void SetChild(IAction child);
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
