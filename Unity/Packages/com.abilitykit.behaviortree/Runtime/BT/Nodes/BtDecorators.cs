using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>反转节点：子 Success -> Failure，子 Failure -> Success。</summary>
    public sealed class BtInverterNode : BtDecoratorNode
    {
        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;

        public override BtNodeState Decorate(BtNodeState state) => state switch
        {
            BtNodeState.Failure => BtNodeState.Success,
            BtNodeState.Success => BtNodeState.Failure,
            _ => state,
        };
    }

    /// <summary>强制成功：子 Failure 也返回 Success（Running 透传）。</summary>
    public sealed class BtForceSuccessNode : BtDecoratorNode
    {
        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;

        public override BtNodeState Decorate(BtNodeState state)
            => state == BtNodeState.Failure ? BtNodeState.Success : state;
    }

    /// <summary>强制失败：子 Success 也返回 Failure（Running 透传）。</summary>
    public sealed class BtForceFailureNode : BtDecoratorNode
    {
        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;

        public override BtNodeState Decorate(BtNodeState state)
            => state == BtNodeState.Success ? BtNodeState.Failure : state;
    }

    /// <summary>
    /// 重复节点：子节点完整执行 count 次（每次完成下个 tick 重新开始）。
    /// count = -1 表示永久重复；子节点 Failure 立即以 Failure 完成。
    /// </summary>
    public sealed class BtRepeaterNode : BtDecoratorNode, IBtNodeStateful
    {
        public const string CountProperty = "count";

        private long _count = 1;
        private long _completed;

        protected override void OnInitParent(in BtNodeInitContext context)
        {
            _count = context.Properties.GetInt64(CountProperty, 1);
        }

        public override void OnStart(BtExecutionContext context)
        {
            _completed = 0;
        }

        protected internal override bool CanExecute()
            => _count < 0 || _completed < _count;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            _completed++;
            State = childState != BtNodeState.Success
                ? childState
                : (CanExecute() ? BtNodeState.Running : BtNodeState.Success);
        }

        public string CaptureState() => _completed.ToString();

        public void RestoreState(string payload)
        {
            _completed = long.Parse(payload);
        }
    }

    /// <summary>
    /// 重试节点：子节点 Failure 时下个 tick 重试，最多重试 count 次；
    /// 子节点 Success 立即 Success；重试耗尽返回 Failure。count = -1 表示无限重试。
    /// </summary>
    public sealed class BtRetryNode : BtDecoratorNode, IBtNodeStateful
    {
        public const string CountProperty = "count";

        private long _count = 1;
        private long _attempts;

        protected override void OnInitParent(in BtNodeInitContext context)
        {
            _count = context.Properties.GetInt64(CountProperty, 1);
        }

        public override void OnStart(BtExecutionContext context)
        {
            _attempts = 0;
        }

        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            if (childState == BtNodeState.Success)
            {
                State = BtNodeState.Success;
                return;
            }

            _attempts++;
            State = _count >= 0 && _attempts > _count ? BtNodeState.Failure : BtNodeState.Running;
        }

        public string CaptureState() => _attempts.ToString();

        public void RestoreState(string payload)
        {
            _attempts = long.Parse(payload);
        }
    }

    /// <summary>
    /// 超时节点：子树运行超过 durationSeconds 后抢占——子树被中止弹出，装饰器以 Failure 完成。
    /// 通过装饰器抢占钩子实现（子节点 Running 期间生效），时钟来自宿主注入的 tick 时间。
    /// </summary>
    public sealed class BtTimeoutNode : BtDecoratorNode, IBtNodeStateful
    {
        public const string DurationSecondsProperty = "durationSeconds";

        private Fixed64 _duration = Fixed64.One;
        private Fixed64 _deadline;

        protected override void OnInitParent(in BtNodeInitContext context)
        {
            _duration = context.Properties.GetFixed64(DurationSecondsProperty, Fixed64.One);
        }

        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;

        public override void OnStart(BtExecutionContext context)
        {
            _deadline = context.Time + _duration;
        }

        protected internal override bool TryTickOverride(BtExecutionContext context, out BtNodeState state)
        {
            if (State == BtNodeState.Running && context.Time >= _deadline)
            {
                state = BtNodeState.Failure;
                return true;
            }

            state = default;
            return false;
        }

        public string CaptureState() => _deadline.RawValue.ToString();

        public void RestoreState(string payload)
        {
            _deadline = Fixed64.FromRaw(long.Parse(payload));
        }
    }

    /// <summary>
    /// 冷却节点：子树完成后进入冷却期，冷却期内再次进入立即以配置结果完成（不执行子树）。
    /// 冷却基于宿主注入的 tick 时间。
    /// </summary>
    public sealed class BtCooldownNode : BtDecoratorNode, IBtNodeStateful
    {
        public const string CooldownSecondsProperty = "cooldownSeconds";
        public const string ResultOnCooldownProperty = "resultOnCooldown"; // 0=Failure, 1=Success

        private Fixed64 _cooldown = Fixed64.One;
        private BtNodeState _result = BtNodeState.Failure;
        private Fixed64 _readyAt;
        private bool _gateFired;

        protected override void OnInitParent(in BtNodeInitContext context)
        {
            _cooldown = context.Properties.GetFixed64(CooldownSecondsProperty, Fixed64.One);
            _result = context.Properties.GetInt64(ResultOnCooldownProperty, 0) == 1
                ? BtNodeState.Success
                : BtNodeState.Failure;
        }

        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;

        protected internal override bool TryTickOverride(BtExecutionContext context, out BtNodeState state)
        {
            if (State == BtNodeState.Running && context.Time < _readyAt)
            {
                _gateFired = true;
                state = _result;
                return true;
            }

            state = default;
            return false;
        }

        public override void OnStop(BtExecutionContext context)
        {
            // 门控弹栈（冷却期内直接完成）不代表子树执行过，不重置冷却计时
            if (_gateFired)
            {
                _gateFired = false;
                return;
            }

            _readyAt = context.Time + _cooldown;
        }

        public string CaptureState() => _readyAt.RawValue.ToString();

        public void RestoreState(string payload)
        {
            _readyAt = Fixed64.FromRaw(long.Parse(payload));
        }
    }

    /// <summary>
    /// 一次节点：子树只在树生命周期内完整执行一次；此后再次进入立即以配置结果完成。
    /// 标记跨组合节点重入保留，树 Enable 重置。
    /// </summary>
    public sealed class BtOnceNode : BtDecoratorNode, IBtNodeStateful
    {
        public const string ResultAfterFirstProperty = "resultAfterFirst"; // 0=Failure, 1=Success

        private BtNodeState _result = BtNodeState.Failure;
        private bool _executed;

        protected override void OnInitParent(in BtNodeInitContext context)
        {
            _result = context.Properties.GetInt64(ResultAfterFirstProperty, 0) == 1
                ? BtNodeState.Success
                : BtNodeState.Failure;
        }

        protected internal override bool CanExecute()
            => State is BtNodeState.Inactive or BtNodeState.Running;

        protected internal override bool TryTickOverride(BtExecutionContext context, out BtNodeState state)
        {
            if (_executed)
            {
                state = _result;
                return true;
            }

            state = default;
            return false;
        }

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
        {
            _executed = true;
            State = childState;
        }

        public string CaptureState() => _executed ? "1" : "0";

        public void RestoreState(string payload)
        {
            _executed = payload == "1";
        }
    }

    /// <summary>直到成功：子节点 Failure 时下个 tick 重试，Success 时以 Success 完成。</summary>
    public sealed class BtUntilSuccessNode : BtDecoratorNode
    {
        protected internal override bool CanExecute() => State != BtNodeState.Success;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;
    }

    /// <summary>直到失败：子节点 Success 时下个 tick 重试，Failure 时以 Failure 完成。</summary>
    public sealed class BtUntilFailureNode : BtDecoratorNode
    {
        protected internal override bool CanExecute() => State != BtNodeState.Failure;

        protected internal override void OnChildExecuted(int childIndex, BtNodeState childState)
            => State = childState;
    }
}
