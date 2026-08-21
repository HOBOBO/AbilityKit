using System;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 父节点基类（组合/装饰）。引擎通过 protected internal 协议驱动子节点推进，
    /// 领域包可跨程序集重写这些成员定义自定义组合/装饰节点。
    /// </summary>
    public abstract class BtParentNodeBase : BtNodeBase
    {
        /// <summary>子节点数（来自定义的 ChildIds，Init 时缓存）。</summary>
        protected int ChildCount { get; private set; }

        /// <summary>通用子游标；具体节点可按需使用（随机组合节点改用自身执行序）。</summary>
        protected int RunningIndex { get; set; } = -1;

        public sealed override void OnInit(in BtNodeInitContext context)
        {
            ChildCount = context.ChildCount;
            OnInitParent(context);
        }

        protected virtual void OnInitParent(in BtNodeInitContext context) { }

        /// <summary>是否允许继续执行（下一个）子节点。</summary>
        protected internal abstract bool CanExecute();

        /// <summary>子节点完成时回调（childIndex 为相对子序号）。</summary>
        protected internal abstract void OnChildExecuted(int childIndex, BtNodeState childState);

        /// <summary>并行节点进入新子分支时回调（用于推进子游标）。</summary>
        protected internal virtual void OnChildStart() { }

        /// <summary>条件中断时回调；index 为中断命中的相对子序号。</summary>
        protected internal virtual void OnConditionalAbort(int childIndex) { }

        protected internal virtual bool CanRunParallel() => false;

        /// <summary>父节点对聚合状态的最终改写（并行节点用）。</summary>
        protected internal virtual BtNodeState OverrideState(BtNodeState state) => state;

        /// <summary>引擎查询当前应执行的子节点序号。</summary>
        protected internal virtual int CurrentChildIndex => RunningIndex;

        /// <summary>快照捕获/恢复运行游标。默认对应 <see cref="RunningIndex"/>。</summary>
        protected internal virtual int CaptureRunningIndex() => RunningIndex;
        protected internal virtual void RestoreRunningIndex(int index) => RunningIndex = index;
    }

    /// <summary>
    /// 组合节点基类。abortType 属性在 OnInit 时统一读取（属性名见 <see cref="AbortTypeProperty"/>），
    /// 决定其下条件节点参与条件中断的方式。
    /// </summary>
    public abstract class BtCompositeNode : BtParentNodeBase
    {
        public const string AbortTypeProperty = "abortType";

        public BtAbortType AbortType { get; protected set; }

        protected sealed override void OnInitParent(in BtNodeInitContext context)
        {
            var raw = context.Properties.GetInt64(AbortTypeProperty, (long)BtAbortType.None);
            if (raw is < 0 or > (long)BtAbortType.Both)
                throw new InvalidOperationException(
                    $"BT node '{context.Definition.Id}' has invalid abortType value {raw}.");
            AbortType = (BtAbortType)raw;
            OnCompositeInit(context);
        }

        protected virtual void OnCompositeInit(in BtNodeInitContext context) { }
    }

    /// <summary>装饰节点基类：单子节点；子状态完成后经 <see cref="Decorate"/> 转换。</summary>
    public abstract class BtDecoratorNode : BtParentNodeBase
    {
        /// <summary>装饰器恒定驱动唯一的 0 号子节点（重复执行由 CanExecute/preIndex 防护控制）。</summary>
        protected internal sealed override int CurrentChildIndex => 0;

        public virtual BtNodeState Decorate(BtNodeState state) => state;

        /// <summary>
        /// 抢占钩子：每 tick 在推进子节点前由引擎询问（自栈顶向下的第一个命中者生效）。
        /// 返回 true 表示装饰器自身立即以 state 完成，其上运行中的子树会被中止弹出。
        /// 超时/冷却类装饰器用它实现"子节点 Running 期间主动打断"。
        /// </summary>
        protected internal virtual bool TryTickOverride(BtExecutionContext context, out BtNodeState state)
        {
            state = default;
            return false;
        }
    }
}
