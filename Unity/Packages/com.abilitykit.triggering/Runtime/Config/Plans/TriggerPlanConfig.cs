using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Cue;
using AbilityKit.Triggering.Runtime.Config.Predicates;
using AbilityKit.Triggering.Runtime.Config.Schedule;

namespace AbilityKit.Triggering.Runtime.Config.Plans
{
    /// <summary>
    /// 触发器计划配置实现（静态配置数据）
    /// </summary>
    [Serializable]
    public class TriggerPlanConfig : ITriggerPlanConfig
    {
        public int TriggerId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; }
        public int Phase { get; set; }
        public int Priority { get; set; }
        public int InterruptPriority { get; set; }
        public IPredicateConfig Predicate { get; set; }
        public List<ActionCallConfig> Actions { get; set; }
        public ScheduleConfig Schedule { get; set; }
        public CueConfig Cue { get; set; }

        IReadOnlyList<IActionCallConfig> ITriggerPlanConfig.Actions => 
            Actions != null ? Actions.ConvertAll<IActionCallConfig>(a => a) : null;
        IScheduleConfig ITriggerPlanConfig.Schedule => Schedule;
        ICueConfig ITriggerPlanConfig.Cue => Cue;

        public static TriggerPlanConfig Create(
            int triggerId,
            int eventId,
            int phase = 0,
            int priority = 0,
            int interruptPriority = 0)
        {
            return new TriggerPlanConfig
            {
                TriggerId = triggerId,
                EventId = eventId,
                Phase = phase,
                Priority = priority,
                InterruptPriority = interruptPriority,
                Predicate = NonePredicateConfig.Instance,
                Actions = new List<ActionCallConfig>(),
                Schedule = ScheduleConfig.Transient,
                Cue = CueConfig.None
            };
        }

        public TriggerPlanConfig WithPredicate(IPredicateConfig predicate)
        {
            Predicate = predicate;
            return this;
        }

        public TriggerPlanConfig WithActions(params ActionCallConfig[] actions)
        {
            Actions = new List<ActionCallConfig>(actions);
            return this;
        }

        public TriggerPlanConfig WithSchedule(ScheduleConfig schedule)
        {
            Schedule = schedule;
            return this;
        }

        public TriggerPlanConfig WithCue(CueConfig cue)
        {
            Cue = cue;
            return this;
        }
    }

    /// <summary>
    /// 空条件配置（单例）
    /// </summary>
    [Serializable]
    public class NonePredicateConfig : IPredicateConfig
    {
        public static NonePredicateConfig Instance { get; } = new NonePredicateConfig();
        private NonePredicateConfig() { }
        
        public bool IsEmpty => true;
        public EPredicateKind Kind => EPredicateKind.None;
    }
}