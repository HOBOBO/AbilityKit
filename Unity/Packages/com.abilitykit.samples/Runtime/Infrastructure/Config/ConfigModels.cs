#nullable enable

using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    #region 閫氱敤閰嶇疆妯″瀷

    /// <summary>
    /// 通用键值对配置
    /// </summary>
    public class KeyValueConfig
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// 鍛藉悕鐨勬暟鍊奸厤缃?
    /// </summary>
    public class NamedValue
    {
        public string Name { get; set; }
        public float Value { get; set; }
    }

    /// <summary>
    /// 甯﹀弬鏁扮殑鍛藉悕鐨勬暟鍊奸厤缃?
    /// </summary>
    public class NamedValueEx : NamedValue
    {
        public Dictionary<string, float> Params { get; set; } = new();
    }

    #endregion

    #region 鐘舵€佹満閰嶇疆

    /// <summary>
    /// 鐘舵€侀厤缃?
    /// </summary>
    public class StateConfig
    {
        public string Name { get; set; }
        public string OnEnter { get; set; }
        public string OnLogic { get; set; }
        public string OnExit { get; set; }
        public List<string> Transitions { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// 鐘舵€佽浆鎹㈤厤缃?
    /// </summary>
    public class TransitionConfig
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Condition { get; set; }
        public float Priority { get; set; }
    }

    /// <summary>
    /// 状态机配置
    /// </summary>
    public class StateMachineConfig
    {
        public string Name { get; set; }
        public string InitialState { get; set; }
        public List<StateConfig> States { get; set; } = new();
        public List<TransitionConfig> Transitions { get; set; } = new();
    }

    #endregion

    #region HFSM 状态机配置

    /// <summary>
    /// HFSM 参数配置模型
    /// </summary>
    public class HFSMParameterConfig
    {
        public string Type { get; set; } = "Float";
        public float DefaultValue { get; set; } = 0f;
        public float MinValue { get; set; } = 0f;
        public float MaxValue { get; set; } = 100f;
    }

    /// <summary>
    /// HFSM 状态配置
    /// </summary>
    public class HFSMStateConfig
    {
        public string Id { get; set; }
        public string Type { get; set; } = "State";
        public string Description { get; set; }
        public bool NeedsExitTime { get; set; } = false;
        public HFSMActionConfig OnEnter { get; set; }
        public HFSMActionConfig OnLogic { get; set; }
        public HFSMActionConfig OnExit { get; set; }
    }

    /// <summary>
    /// HFSM 转换条件配置
    /// </summary>
    public class HFSMConditionConfig
    {
        public string Type { get; set; }
        public string ParameterName { get; set; }
        public string Operator { get; set; } = "Equal";
        public float Value { get; set; } = 0f;
        public bool BoolValue { get; set; } = false;
    }

    /// <summary>
    /// HFSM 转换配置
    /// </summary>
    public class HFSMTransitionConfig
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Description { get; set; }
        public bool ForceInstantly { get; set; } = false;
        public HFSMConditionConfig Condition { get; set; }
    }

    /// <summary>
    /// HFSM 状态回调配置
    /// </summary>
    public class HFSMActionConfig
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public float Duration { get; set; } = 0f;
    }

    /// <summary>
    /// HFSM 状态机配置
    /// </summary>
    public class HFSMConfig
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string InitialState { get; set; }
        public Dictionary<string, HFSMParameterConfig> Parameters { get; set; } = new();
        public List<HFSMStateConfig> States { get; set; } = new();
        public List<HFSMTransitionConfig> Transitions { get; set; } = new();
    }

    #endregion

    #region 持续行为配置

    /// <summary>
    /// 持续行为效果配置
    /// </summary>
    public class OngoingEffectConfig
    {
        public string Type { get; set; }
        public string Target { get; set; }
        public float Value { get; set; } = 0f;
    }

    /// <summary>
    /// 持续行为配置
    /// </summary>
    public class OngoingBehaviorConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public float Duration { get; set; } = -1f;
        public float TickInterval { get; set; } = 0f;
        public string StackPolicy { get; set; } = "Refresh";
        public int MaxStacks { get; set; } = 1;
        public List<OngoingEffectConfig> Effects { get; set; } = new();
    }

    /// <summary>
    /// 持续行为集合配置
    /// </summary>
    public class OngoingBehaviorSetConfig
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string TargetFsm { get; set; }
        public List<OngoingBehaviorConfig> Behaviors { get; set; } = new();
    }

    #endregion

    #region 鎶€鑳?绠＄嚎閰嶇疆

    /// <summary>
    /// 鎶€鑳介樁娈甸厤缃?
    /// </summary>
    public class PhaseConfig
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float Duration { get; set; }
        public List<PhaseConfig> Children { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// 鎶€鑳介厤缃?
    /// </summary>
    public class AbilityConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float Cooldown { get; set; }
        public float ManaCost { get; set; }
        public List<PhaseConfig> Phases { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
    }

    #endregion

    #region 琛屼负鏍戦厤缃?

    /// <summary>
    /// 行为节点配置
    /// </summary>
    public class BehaviorNodeConfig
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<BehaviorNodeConfig> Children { get; set; } = new();
    }

    /// <summary>
    /// 琛屼负鏍戦厤缃?
    /// </summary>
    public class BehaviorTreeConfig
    {
        public string Name { get; set; }
        public BehaviorNodeConfig Root { get; set; }
        public Dictionary<string, object> Blackboard { get; set; } = new();
    }

    #endregion

    #region 鎴樻枟瀹炰綋閰嶇疆

    /// <summary>
    /// 战斗实体配置
    /// </summary>
    public class BattleEntityConfig
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float Health { get; set; }
        public float Attack { get; set; }
        public float Defense { get; set; }
        public float Speed { get; set; }
        public Dictionary<string, float> Attributes { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// 战斗配置
    /// </summary>
    public class BattleConfig
    {
        public string Name { get; set; }
        public List<BattleEntityConfig> Allies { get; set; } = new();
        public List<BattleEntityConfig> Enemies { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    #endregion
}
