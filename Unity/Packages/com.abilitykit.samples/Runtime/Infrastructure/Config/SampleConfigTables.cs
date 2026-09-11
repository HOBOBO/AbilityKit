#nullable enable

using System;
using Newtonsoft.Json;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 绀轰緥閰嶇疆琛ㄥ畾涔?    /// </summary>
    public static class SampleConfigTables
    {
        /// <summary>
        /// 鐘舵€佹満閰嶇疆琛ㄥ畾涔?        /// </summary>
        public static readonly ConfigTableDefinition StateMachineTable =
            new ConfigTableDefinition("stateMachine", typeof(StateMachineDto[]), typeof(StateMachineEntry));

        /// <summary>
        /// 技能配置表定义
        /// </summary>
        public static readonly ConfigTableDefinition AbilityTable =
            new ConfigTableDefinition("abilities", typeof(AbilityDto[]), typeof(AbilityEntry));

        /// <summary>
        /// 行为树配置表定义
        /// </summary>
        public static readonly ConfigTableDefinition BehaviorTreeTable =
            new ConfigTableDefinition("behaviorTree", typeof(BehaviorTreeDto[]), typeof(BehaviorTreeEntry));

        /// <summary>
        /// 鎴樻枟閰嶇疆琛ㄥ畾涔?        /// </summary>
        public static readonly ConfigTableDefinition BattleTable =
            new ConfigTableDefinition("battle", typeof(BattleDto[]), typeof(BattleEntry));

        /// <summary>
        /// 绠＄嚎閰嶇疆琛ㄥ畾涔?        /// </summary>
        public static readonly ConfigTableDefinition PipelineTable =
            new ConfigTableDefinition("pipeline", typeof(PipelineDto[]), typeof(PipelineEntry));
    }

    #region DTOs

    /// <summary>
    /// 状态机 DTO
    /// </summary>
    public class StateMachineDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InitialState { get; set; }
        public StateDto[] States { get; set; }
        public TransitionDto[] Transitions { get; set; }
    }

    public class StateDto
    {
        public string Name { get; set; }
        public string OnEnter { get; set; }
        public string OnLogic { get; set; }
        public string OnExit { get; set; }
        public string[] Transitions { get; set; }
    }

    public class TransitionDto
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Condition { get; set; }
    }

    /// <summary>
    /// 鎶€鑳?DTO
    /// </summary>
    public class AbilityDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public float Cooldown { get; set; }
        public float ManaCost { get; set; }
        public PhaseDto[] Phases { get; set; }
        public string[] Conditions { get; set; }
    }

    public class PhaseDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float Duration { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// 琛屼负鏍?DTO
    /// </summary>
    public class BehaviorTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public BehaviorNodeDto Root { get; set; }
    }

    public class BehaviorNodeDto
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public BehaviorNodeDto[] Children { get; set; }
    }

    /// <summary>
    /// 战斗 DTO
    /// </summary>
    public class BattleDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public EntityDto[] Allies { get; set; }
        public EntityDto[] Enemies { get; set; }
    }

    public class EntityDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public float Health { get; set; }
        public float Attack { get; set; }
        public float Defense { get; set; }
        public float Speed { get; set; }
    }

    /// <summary>
    /// 管线 DTO
    /// </summary>
    public class PipelineDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PhaseDto[] Phases { get; set; }
    }

    #endregion

    #region Entries

    /// <summary>
    /// 状态机 Entry
    /// </summary>
    public class StateMachineEntry
    {
        public int Id { get; }
        public string Name { get; }
        public string InitialState { get; }
        public StateEntry[] States { get; }
        public TransitionEntry[] Transitions { get; }

        public StateMachineEntry(StateMachineDto dto)
        {
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            InitialState = dto.InitialState ?? string.Empty;
            States = Array.ConvertAll(dto.States ?? Array.Empty<StateDto>(), s => new StateEntry(s));
            Transitions = Array.ConvertAll(dto.Transitions ?? Array.Empty<TransitionDto>(), t => new TransitionEntry(t));
        }
    }

    public class StateEntry
    {
        public string Name { get; }
        public string OnEnter { get; }
        public string OnLogic { get; }
        public string OnExit { get; }
        public string[] Transitions { get; }

        public StateEntry(StateDto dto)
        {
            Name = dto.Name ?? string.Empty;
            OnEnter = dto.OnEnter ?? string.Empty;
            OnLogic = dto.OnLogic ?? string.Empty;
            OnExit = dto.OnExit ?? string.Empty;
            Transitions = dto.Transitions ?? Array.Empty<string>();
        }
    }

    public class TransitionEntry
    {
        public string From { get; }
        public string To { get; }
        public string Condition { get; }

        public TransitionEntry(TransitionDto dto)
        {
            From = dto.From ?? string.Empty;
            To = dto.To ?? string.Empty;
            Condition = dto.Condition ?? string.Empty;
        }
    }

    /// <summary>
    /// 鎶€鑳?Entry
    /// </summary>
    public class AbilityEntry
    {
        public int Id { get; }
        public string Name { get; }
        public float Cooldown { get; }
        public float ManaCost { get; }
        public PhaseEntry[] Phases { get; }
        public string[] Conditions { get; }

        public AbilityEntry(AbilityDto dto)
        {
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            Cooldown = dto.Cooldown;
            ManaCost = dto.ManaCost;
            Phases = Array.ConvertAll(dto.Phases ?? Array.Empty<PhaseDto>(), p => new PhaseEntry(p));
            Conditions = dto.Conditions ?? Array.Empty<string>();
        }
    }

    public class PhaseEntry
    {
        public string Name { get; }
        public string Type { get; }
        public float Duration { get; }
        public string Description { get; }

        public PhaseEntry(PhaseDto dto)
        {
            Name = dto.Name ?? string.Empty;
            Type = dto.Type ?? string.Empty;
            Duration = dto.Duration;
            Description = dto.Description ?? string.Empty;
        }
    }

    /// <summary>
    /// 琛屼负鏍?Entry
    /// </summary>
    public class BehaviorTreeEntry
    {
        public int Id { get; }
        public string Name { get; }
        public BehaviorNodeEntry Root { get; }

        public BehaviorTreeEntry(BehaviorTreeDto dto)
        {
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            Root = dto.Root != null ? new BehaviorNodeEntry(dto.Root) : null;
        }
    }

    public class BehaviorNodeEntry
    {
        public string Type { get; }
        public string Name { get; }
        public BehaviorNodeEntry[] Children { get; }

        public BehaviorNodeEntry(BehaviorNodeDto dto)
        {
            Type = dto.Type ?? string.Empty;
            Name = dto.Name ?? string.Empty;
            Children = Array.ConvertAll(dto.Children ?? Array.Empty<BehaviorNodeDto>(), c => new BehaviorNodeEntry(c));
        }
    }

    /// <summary>
    /// 战斗 Entry
    /// </summary>
    public class BattleEntry
    {
        public int Id { get; }
        public string Name { get; }
        public EntityEntry[] Allies { get; }
        public EntityEntry[] Enemies { get; }

        public BattleEntry(BattleDto dto)
        {
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            Allies = Array.ConvertAll(dto.Allies ?? Array.Empty<EntityDto>(), e => new EntityEntry(e));
            Enemies = Array.ConvertAll(dto.Enemies ?? Array.Empty<EntityDto>(), e => new EntityEntry(e));
        }
    }

    public class EntityEntry
    {
        public string Id { get; }
        public string Name { get; }
        public float Health { get; }
        public float Attack { get; }
        public float Defense { get; }
        public float Speed { get; }

        public EntityEntry(EntityDto dto)
        {
            Id = dto.Id ?? string.Empty;
            Name = dto.Name ?? string.Empty;
            Health = dto.Health;
            Attack = dto.Attack;
            Defense = dto.Defense;
            Speed = dto.Speed;
        }
    }

    /// <summary>
    /// 管线 Entry
    /// </summary>
    public class PipelineEntry
    {
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public PhaseEntry[] Phases { get; }

        public PipelineEntry(PipelineDto dto)
        {
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            Description = dto.Description ?? string.Empty;
            Phases = Array.ConvertAll(dto.Phases ?? Array.Empty<PhaseDto>(), p => new PhaseEntry(p));
        }
    }

    #endregion
}
