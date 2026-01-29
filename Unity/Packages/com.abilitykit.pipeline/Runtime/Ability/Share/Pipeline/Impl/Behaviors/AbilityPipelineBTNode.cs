namespace AbilityKit.Ability
{
    /// <summary>
    /// 行为树节点状态
    /// </summary>
    public enum EBTNodeStatus
    {
        /// <summary>
        /// 运行中
        /// </summary>
        Running,
        /// <summary>
        /// 成功
        /// </summary>
        Success,
        /// <summary>
        /// 失败
        /// </summary>
        Failure
    }

    /// <summary>
    /// 行为树节点接口
    /// </summary>
    public interface IBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        EBTNodeStatus Tick(TCtx context, float deltaTime);
        void Reset();
    }

    /// <summary>
    /// 管线行为树节点基类
    /// 用于将管线阶段包装成行为树节点
    /// </summary>
    public abstract class AbilityPipelineBTNode<TCtx> : IBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        protected EBTNodeStatus _status = EBTNodeStatus.Running;
        protected bool _isStarted = false;

        /// <summary>
        /// 每帧执行
        /// </summary>
        public virtual EBTNodeStatus Tick(TCtx context, float deltaTime)
        {
            if (!_isStarted)
            {
                OnStart(context);
                _isStarted = true;
            }

            _status = OnUpdate(context, deltaTime);

            if (_status != EBTNodeStatus.Running)
            {
                OnEnd(context, _status == EBTNodeStatus.Success);
            }

            return _status;
        }

        /// <summary>
        /// 重置节点
        /// </summary>
        public virtual void Reset()
        {
            _status = EBTNodeStatus.Running;
            _isStarted = false;
        }

        /// <summary>
        /// 节点开始时调用
        /// </summary>
        protected virtual void OnStart(TCtx context) { }

        /// <summary>
        /// 每帧更新，返回节点状态
        /// </summary>
        protected abstract EBTNodeStatus OnUpdate(TCtx context, float deltaTime);

        /// <summary>
        /// 节点结束时调用
        /// </summary>
        protected virtual void OnEnd(TCtx context, bool success) { }
    }

    /// <summary>
    /// 管线阶段包装节点
    /// 将 IAbilityPipelinePhase 包装成行为树节点
    /// </summary>
    public class AbilityPhaseBTNode<TCtx> : AbilityPipelineBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly IAbilityPipelinePhase<TCtx> _phase;
        private IDurationalPhase<TCtx> _durationalPhase;

        public AbilityPhaseBTNode(IAbilityPipelinePhase<TCtx> phase)
        {
            _phase = phase;
            _durationalPhase = phase as IDurationalPhase<TCtx>;
        }

        protected override void OnStart(TCtx context)
        {
            base.OnStart(context);
            _phase.Execute(context);
        }

        protected override EBTNodeStatus OnUpdate(TCtx context, float deltaTime)
        {
            // 非持续性阶段，执行完立即成功
            if (_durationalPhase == null)
            {
                return EBTNodeStatus.Success;
            }

            // 持续性阶段，等待完成
            _durationalPhase.OnUpdate(context, deltaTime);

            if (_durationalPhase.IsComplete)
            {
                return EBTNodeStatus.Success;
            }

            return EBTNodeStatus.Running;
        }

        public override void Reset()
        {
            base.Reset();
            // 如果阶段支持重置，可以在这里调用
        }
    }

    /// <summary>
    /// 行为树序列节点
    /// 按顺序执行子节点，全部成功则成功，任一失败则失败
    /// </summary>
    public class BTSequenceNode<TCtx> : AbilityPipelineBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly IBTNode<TCtx>[] _children;
        private int _currentIndex = 0;

        public BTSequenceNode(params IBTNode<TCtx>[] children)
        {
            _children = children;
        }

        protected override void OnStart(TCtx context)
        {
            base.OnStart(context);
            _currentIndex = 0;
        }

        protected override EBTNodeStatus OnUpdate(TCtx context, float deltaTime)
        {
            while (_currentIndex < _children.Length)
            {
                var status = _children[_currentIndex].Tick(context, deltaTime);

                if (status == EBTNodeStatus.Running)
                    return EBTNodeStatus.Running;

                if (status == EBTNodeStatus.Failure)
                    return EBTNodeStatus.Failure;

                _currentIndex++;
            }

            return EBTNodeStatus.Success;
        }

        public override void Reset()
        {
            base.Reset();
            _currentIndex = 0;
            foreach (var child in _children)
            {
                child.Reset();
            }
        }
    }

    /// <summary>
    /// 行为树选择节点
    /// 按顺序执行子节点，任一成功则成功，全部失败则失败
    /// </summary>
    public class BTSelectorNode<TCtx> : AbilityPipelineBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly IBTNode<TCtx>[] _children;
        private int _currentIndex = 0;

        public BTSelectorNode(params IBTNode<TCtx>[] children)
        {
            _children = children;
        }

        protected override void OnStart(TCtx context)
        {
            base.OnStart(context);
            _currentIndex = 0;
        }

        protected override EBTNodeStatus OnUpdate(TCtx context, float deltaTime)
        {
            while (_currentIndex < _children.Length)
            {
                var status = _children[_currentIndex].Tick(context, deltaTime);

                if (status == EBTNodeStatus.Running)
                    return EBTNodeStatus.Running;

                if (status == EBTNodeStatus.Success)
                    return EBTNodeStatus.Success;

                _currentIndex++;
            }

            return EBTNodeStatus.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            _currentIndex = 0;
            foreach (var child in _children)
            {
                child.Reset();
            }
        }
    }

    /// <summary>
    /// 行为树并行节点
    /// 同时执行所有子节点
    /// </summary>
    public class BTParallelNode<TCtx> : AbilityPipelineBTNode<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 并行策略
        /// </summary>
        public enum ParallelPolicy
        {
            /// <summary>
            /// 全部成功才成功
            /// </summary>
            RequireAll,
            /// <summary>
            /// 任一成功即成功
            /// </summary>
            RequireOne
        }

        private readonly IBTNode<TCtx>[] _children;
        private readonly ParallelPolicy _successPolicy;
        private readonly bool[] _completed;

        public BTParallelNode(ParallelPolicy policy, params IBTNode<TCtx>[] children)
        {
            _children = children;
            _successPolicy = policy;
            _completed = new bool[children.Length];
        }

        protected override void OnStart(TCtx context)
        {
            base.OnStart(context);
            for (int i = 0; i < _completed.Length; i++)
            {
                _completed[i] = false;
            }
        }

        protected override EBTNodeStatus OnUpdate(TCtx context, float deltaTime)
        {
            int successCount = 0;
            int failureCount = 0;
            int runningCount = 0;

            for (int i = 0; i < _children.Length; i++)
            {
                if (_completed[i])
                {
                    successCount++;
                    continue;
                }

                var status = _children[i].Tick(context, deltaTime);
                
                switch (status)
                {
                    case EBTNodeStatus.Success:
                        _completed[i] = true;
                        successCount++;
                        if (_successPolicy == ParallelPolicy.RequireOne)
                            return EBTNodeStatus.Success;
                        break;
                    case EBTNodeStatus.Failure:
                        failureCount++;
                        if (_successPolicy == ParallelPolicy.RequireAll)
                            return EBTNodeStatus.Failure;
                        break;
                    case EBTNodeStatus.Running:
                        runningCount++;
                        break;
                }
            }

            if (runningCount > 0)
                return EBTNodeStatus.Running;

            if (_successPolicy == ParallelPolicy.RequireAll)
                return successCount == _children.Length ? EBTNodeStatus.Success : EBTNodeStatus.Failure;
            else
                return successCount > 0 ? EBTNodeStatus.Success : EBTNodeStatus.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            for (int i = 0; i < _completed.Length; i++)
            {
                _completed[i] = false;
                _children[i].Reset();
            }
        }
    }
}