using System;
using System.Collections.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为上下文接口
    /// 
    /// 注意：完全独立于 Triggering 模块
    /// </summary>
    public interface IBehaviorContext
    {
        /// <summary>
        /// 行为实例ID
        /// </summary>
        long InstanceId { get; }
        
        /// <summary>
        /// 行为类型名称
        /// </summary>
        string BehaviorKind { get; }
        
        /// <summary>
        /// 来源上下文ID（如技能ID、BuffID等）
        /// </summary>
        long SourceContextId { get; }
        
        /// <summary>
        /// 拥有者实体
        /// </summary>
        BehaviorEntityId OwnerId { get; }
        
        /// <summary>
        /// 目标实体
        /// </summary>
        BehaviorEntityId? TargetId { get; }
        
        /// <summary>
        /// 当前帧
        /// </summary>
        long CurrentFrame { get; }
        
        /// <summary>
        /// 已运行时间（秒）
        /// </summary>
        float ElapsedSeconds { get; }
        
        /// <summary>
        /// 持续时间（秒），null表示无限
        /// </summary>
        float? DurationSeconds { get; }
        
        /// <summary>
        /// 当前阶段
        /// </summary>
        BehaviorPhase Phase { get; }
        
        /// <summary>
        /// 世界查询接口
        /// </summary>
        IWorldQuery World { get; }
        
        /// <summary>
        /// 状态存储
        /// </summary>
        IBehaviorState State { get; }
        
        /// <summary>
        /// 配置参数
        /// </summary>
        IReadOnlyDictionary<string, object> Config { get; }
        
        /// <summary>
        /// 获取配置参数
        /// </summary>
        T GetConfig<T>(string key, T defaultValue = default);
        
        /// <summary>
        /// 尝试获取配置参数
        /// </summary>
        bool TryGetConfig<T>(string key, out T value);
        
        /// <summary>
        /// 获取目标位置
        /// </summary>
        Vec3? GetTargetPosition();
    }
    
    /// <summary>
    /// 行为状态存储接口
    /// </summary>
    public interface IBehaviorState
    {
        T Get<T>(string key, T defaultValue = default);
        void Set<T>(string key, T value);
        bool Has(string key);
        void Remove(string key);
        void Clear();
    }
    
    /// <summary>
    /// 行为决策器接口
    /// 
    /// 职责：每帧判断"现在应该做什么"
    /// </summary>
    public interface IBehaviorDecision
    {
        /// <summary>
        /// 决策类型名称（用于调试）
        /// </summary>
        string DecisionType { get; }
        
        /// <summary>
        /// 当前状态名称
        /// </summary>
        string CurrentState { get; }
        
        /// <summary>
        /// 做出决策
        /// </summary>
        DecisionResult Decide(IBehaviorContext context, IWorldQuery world);
    }
    
    /// <summary>
    /// 可组合的决策器接口
    /// </summary>
    public interface ICompositeDecision : IBehaviorDecision
    {
        CompositeDecisionKind CompositeKind { get; }
        IReadOnlyList<IBehaviorDecision> Children { get; }
        void AddChild(IBehaviorDecision child);
        bool RemoveChild(IBehaviorDecision child);
    }
    
    /// <summary>
    /// 组合决策器类型
    /// </summary>
    public enum CompositeDecisionKind
    {
        Selector = 0,  // 选择器：返回第一个成功的
        Sequence = 1  // 顺序器：全部成功才成功
    }
    
    /// <summary>
    /// 决策执行委托
    /// </summary>
    public delegate DecisionResult DecisionDelegate(IBehaviorContext context, IWorldQuery world);
    
    /// <summary>
    /// 简单委托决策器
    /// </summary>
    public class DelegateDecision : IBehaviorDecision
    {
        public string DecisionType { get; }
        public string CurrentState => _state;
        
        private string _state;
        private readonly DecisionDelegate _decide;
        
        public DelegateDecision(string decisionType, DecisionDelegate decide)
        {
            DecisionType = decisionType;
            _decide = decide;
            _state = "Root";
        }
        
        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            var result = _decide(context, world);
            if (result.Kind == DecisionKind.ChangeState && !string.IsNullOrEmpty(result.StateName))
            {
                _state = result.StateName;
            }
            return result;
        }
        
        public void SetState(string state) => _state = state;
    }
    
    /// <summary>
    /// 选择器决策器
    /// </summary>
    public class SelectorDecision : ICompositeDecision
    {
        public string DecisionType => "Selector";
        public CompositeDecisionKind CompositeKind => CompositeDecisionKind.Selector;
        public string CurrentState => _currentState;
        public IReadOnlyList<IBehaviorDecision> Children => _children.AsReadOnly();
        
        private readonly List<IBehaviorDecision> _children = new List<IBehaviorDecision>();
        private string _currentState = "Selector";
        
        public SelectorDecision(params IBehaviorDecision[] children)
        {
            _children.AddRange(children);
        }
        
        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            foreach (var child in _children)
            {
                var result = child.Decide(context, world);
                if (result.Kind != DecisionKind.Continue)
                {
                    if (result.Kind == DecisionKind.ChangeState && !string.IsNullOrEmpty(result.StateName))
                        _currentState = result.StateName;
                    return result;
                }
            }
            return DecisionResult.Continue(_currentState);
        }
        
        public void AddChild(IBehaviorDecision child) => _children.Add(child);
        public bool RemoveChild(IBehaviorDecision child) => _children.Remove(child);
    }
    
    /// <summary>
    /// 顺序决策器
    /// </summary>
    public class SequenceDecision : ICompositeDecision
    {
        public string DecisionType => "Sequence";
        public CompositeDecisionKind CompositeKind => CompositeDecisionKind.Sequence;
        public string CurrentState => _currentState;
        public IReadOnlyList<IBehaviorDecision> Children => _children.AsReadOnly();
        
        private readonly List<IBehaviorDecision> _children = new List<IBehaviorDecision>();
        private int _currentIndex;
        private string _currentState = "Sequence";
        
        public SequenceDecision(params IBehaviorDecision[] children)
        {
            _children.AddRange(children);
            _currentIndex = 0;
        }
        
        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            while (_currentIndex < _children.Count)
            {
                var result = _children[_currentIndex].Decide(context, world);
                
                if (result.Kind == DecisionKind.ChangeState)
                {
                    _currentState = result.StateName;
                    _currentIndex = 0;
                    return result;
                }
                
                if (result.Kind == DecisionKind.Complete || result.Kind == DecisionKind.Interrupt)
                {
                    _currentIndex = 0;
                    return result;
                }
                
                if (result.Kind == DecisionKind.Continue)
                {
                    _currentIndex++;
                    if (_currentIndex >= _children.Count)
                    {
                        _currentIndex = 0;
                        return DecisionResult.Complete();
                    }
                    _currentState = _children[_currentIndex].CurrentState;
                }
            }
            
            _currentIndex = 0;
            return DecisionResult.Complete();
        }
        
        public void AddChild(IBehaviorDecision child) => _children.Add(child);
        public bool RemoveChild(IBehaviorDecision child) => _children.Remove(child);
        public void Reset() => _currentIndex = 0;
    }
}
