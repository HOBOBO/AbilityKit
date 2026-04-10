using System;
using System.Collections.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为阶段
    /// </summary>
    public enum BehaviorPhase
    {
        Created = 0,
        Running = 1,
        Paused = 2,
        Completed = 3,
        Interrupted = 4
    }
    
    /// <summary>
    /// 决策结果类型
    /// </summary>
    public enum DecisionKind
    {
        Continue = 0,
        ChangeState = 1,
        Complete = 2,
        Interrupt = 3
    }
    
    /// <summary>
    /// 行为实体标识
    /// </summary>
    public readonly struct BehaviorEntityId
    {
        public readonly long Value;
        public static BehaviorEntityId Invalid => new BehaviorEntityId(-1);
        public bool IsValid => Value >= 0;
        
        public BehaviorEntityId(long value) => Value = value;
        public static implicit operator long(BehaviorEntityId id) => id.Value;
        public static implicit operator BehaviorEntityId(long value) => new BehaviorEntityId(value);
        public override string ToString() => Value.ToString();
        public override bool Equals(object obj) => obj is BehaviorEntityId other && Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(BehaviorEntityId a, BehaviorEntityId b) => a.Value == b.Value;
        public static bool operator !=(BehaviorEntityId a, BehaviorEntityId b) => a.Value != b.Value;
    }
    
    /// <summary>
    /// 决策结果
    /// </summary>
    public readonly struct DecisionResult
    {
        public DecisionKind Kind { get; }
        public string StateName { get; }
        public string InterruptReason { get; }
        public IReadOnlyDictionary<string, object> Params { get; }
        
        internal DecisionResult(DecisionKind kind, string stateName, string interruptReason, 
            IReadOnlyDictionary<string, object> @params)
        {
            Kind = kind;
            StateName = stateName;
            InterruptReason = interruptReason;
            Params = @params;
        }
        
        public static DecisionResult Continue(string state = "Running") => 
            new DecisionResult(DecisionKind.Continue, state, null, EmptyParams);
        
        public static DecisionResult ChangeState(string stateName) => 
            new DecisionResult(DecisionKind.ChangeState, stateName, null, EmptyParams);
        
        public static DecisionResult Complete() => 
            new DecisionResult(DecisionKind.Complete, null, null, EmptyParams);
        
        public static DecisionResult Interrupt(string reason) => 
            new DecisionResult(DecisionKind.Interrupt, null, reason, EmptyParams);
        
        internal static DecisionResult WithParams(DecisionResult original, IReadOnlyDictionary<string, object> @params) =>
            new DecisionResult(original.Kind, original.StateName, original.InterruptReason, @params);
        
        public T GetParam<T>(string key, T defaultValue = default)
        {
            if (Params == null || !Params.TryGetValue(key, out var value) || value == null)
                return defaultValue;
            if (value is T typed) return typed;
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return defaultValue; }
        }
        
        public Vec3? GetMoveTarget()
        {
            if (Params.TryGetValue("MoveTarget", out var v) && v is Vec3 vec) return vec;
            return null;
        }
        
        public BehaviorEntityId? GetMoveTargetEntity()
        {
            if (Params.TryGetValue("MoveTargetEntity", out var v))
            {
                if (v is BehaviorEntityId id) return id;
                if (v is long l) return new BehaviorEntityId(l);
            }
            return null;
        }
        
        public float GetMoveSpeed(float defaultValue = 0f)
        {
            if (Params.TryGetValue("MoveSpeed", out var v))
            {
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is int i) return i;
            }
            return defaultValue;
        }
        
        private static readonly IReadOnlyDictionary<string, object> EmptyParams = 
            new Dictionary<string, object>();
    }
    
    /// <summary>
    /// 行为结束原因
    /// </summary>
    public enum BehaviorEndReason
    {
        Completed,
        Interrupted,
        SourceEnded,
        CleanedUp
    }
}
