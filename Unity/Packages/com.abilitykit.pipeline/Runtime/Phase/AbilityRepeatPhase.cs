using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 重复阶段 - 重复执行子阶段指定次数
    /// </summary>
    public class AbilityRepeatPhase<TCtx> : AbilityDurationalPhaseBase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 重复次数（-1表示无限）
        /// </summary>
        public int RepeatCount { get; set; } = -1;
        
        /// <summary>
        /// 当前重复次数
        /// </summary>
        public int CurrentRepeatIndex { get; private set; }
        
        /// <summary>
        /// 每次重复的间隔时间
        /// </summary>
        public float RepeatInterval { get; set; } = 0f;
        
        /// <summary>
        /// 当前间隔计时
        /// </summary>
        private float _intervalTimer;
        
        /// <summary>
        /// 是否在等待间隔
        /// </summary>
        private bool _isWaitingInterval;
        
        /// <summary>
        /// 要重复执行的阶段
        /// </summary>
        private IAbilityPipelinePhase<TCtx> _repeatPhase;
        
        /// <summary>
        /// 要重复执行的动作
        /// </summary>
        private Action<TCtx, int> _repeatAction;
        
        /// <summary>
        /// 当前正在执行的子阶段
        /// </summary>
        private IAbilityPipelinePhase<TCtx> _currentSubPhase;

        public AbilityRepeatPhase(int repeatCount = -1) : base("Repeat")
        {
            RepeatCount = repeatCount;
        }

        public AbilityRepeatPhase(AbilityPipelinePhaseId phaseId, int repeatCount = -1) : base(phaseId)
        {
            RepeatCount = repeatCount;
        }

        /// <summary>
        /// 设置要重复的阶段
        /// </summary>
        public AbilityRepeatPhase<TCtx> SetRepeatPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            _repeatPhase = phase;
            _repeatAction = null;
            return this;
        }

        /// <summary>
        /// 设置要重复的动作
        /// </summary>
        public AbilityRepeatPhase<TCtx> SetRepeatAction(Action<TCtx, int> action)
        {
            _repeatAction = action;
            _repeatPhase = null;
            return this;
        }

        protected override void OnEnter(TCtx context)
        {
            base.OnEnter(context);
            CurrentRepeatIndex = 0;
            _intervalTimer = 0f;
            _isWaitingInterval = false;
            _currentSubPhase = null;
        }

        protected override void OnExecute(TCtx context)
        {
            ExecuteRepeat(context);
        }

        protected override void OnTick(TCtx context, float deltaTime)
        {
            // 处理正在执行的子阶段
            if (_currentSubPhase != null)
            {
                _currentSubPhase.OnUpdate(context, deltaTime);
                if (_currentSubPhase.IsComplete)
                {
                    _currentSubPhase = null;
                    OnRepeatComplete(context);
                }
                return;
            }

            // 处理间隔等待
            if (_isWaitingInterval)
            {
                _intervalTimer += deltaTime;
                if (_intervalTimer >= RepeatInterval)
                {
                    _isWaitingInterval = false;
                    _intervalTimer = 0f;
                    ExecuteRepeat(context);
                }
            }
        }

        private void ExecuteRepeat(TCtx context)
        {
            // 检查是否达到重复次数
            if (RepeatCount > 0 && CurrentRepeatIndex >= RepeatCount)
            {
                Complete(context);
                return;
            }

            // 执行重复动作或阶段
            if (_repeatAction != null)
            {
                try
                {
                    _repeatAction.Invoke(context, CurrentRepeatIndex);
                }
                catch (Exception e)
                {
                    HandleError(context, e);
                    return;
                }
                OnRepeatComplete(context);
            }
            else if (_repeatPhase != null)
            {
                _repeatPhase.Reset(); // 重置阶段状态以便重复执行
                _repeatPhase.Execute(context);
                
                // 统一模型：检查 IsComplete 判断是否需要等待
                if (!_repeatPhase.IsComplete)
                {
                    _currentSubPhase = _repeatPhase;
                }
                else
                {
                    OnRepeatComplete(context);
                }
            }
            else
            {
                // 没有设置重复内容，直接完成
                Complete(context);
            }
        }

        private void OnRepeatComplete(TCtx context)
        {
            CurrentRepeatIndex++;
            
            // 检查是否达到重复次数
            if (RepeatCount > 0 && CurrentRepeatIndex >= RepeatCount)
            {
                Complete(context);
                return;
            }

            // 开始间隔等待
            if (RepeatInterval > 0)
            {
                _isWaitingInterval = true;
                _intervalTimer = 0f;
            }
            else
            {
                // 无间隔，立即执行下一次
                ExecuteRepeat(context);
            }
        }

        /// <summary>
        /// 创建重复阶段
        /// </summary>
        public static AbilityRepeatPhase<TCtx> Create(int repeatCount = -1)
        {
            return new AbilityRepeatPhase<TCtx>(repeatCount);
        }

        /// <summary>
        /// 创建重复阶段（带动作）
        /// </summary>
        public static AbilityRepeatPhase<TCtx> Create(Action<TCtx, int> action, int repeatCount = -1, float interval = 0f)
        {
            var phase = new AbilityRepeatPhase<TCtx>(repeatCount);
            phase.SetRepeatAction(action);
            phase.RepeatInterval = interval;
            return phase;
        }
    }
}

