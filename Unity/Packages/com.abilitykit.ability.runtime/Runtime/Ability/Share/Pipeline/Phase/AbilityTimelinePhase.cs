// using System;
// using System.Collections.Generic;

// namespace AbilityKit.Ability
// {
//     /// <summary>
//     /// 时间轴阶段 - 在指定时间点触发事件
//     /// </summary>
//     public class AbilityTimelinePhase : AbilityDurationalPhaseBase
//     {
//         /// <summary>
//         /// 时间轴事件
//         /// </summary>
//         public struct TimelineEvent
//         {
//             /// <summary>
//             /// 触发时间点
//             /// </summary>
//             public float TriggerTime;
            
//             /// <summary>
//             /// 事件回调
//             /// </summary>
//             public Action<IAbilityPipelineContext> Callback;
            
//             /// <summary>
//             /// 事件名称（用于调试）
//             /// </summary>
//             public string EventName;
            
//             /// <summary>
//             /// 是否已触发
//             /// </summary>
//             public bool IsTriggered;
//         }

//         private List<TimelineEvent> _events = new();
//         private int _nextEventIndex = 0;
        
//         /// <summary>
//         /// 是否循环播放
//         /// </summary>
//         public bool IsLoop { get; set; }
        
//         /// <summary>
//         /// 时间缩放
//         /// </summary>
//         public float TimeScale { get; set; } = 1f;
        
//         /// <summary>
//         /// 当前时间轴时间
//         /// </summary>
//         public float CurrentTime => _elapsedTime * TimeScale;

//         public AbilityTimelinePhase(float duration) : base("Timeline")
//         {
//             Duration = duration;
//         }

//         public AbilityTimelinePhase(AbilityPipelinePhaseId phaseId, float duration) : base(phaseId)
//         {
//             Duration = duration;
//         }

//         /// <summary>
//         /// 添加时间轴事件
//         /// </summary>
//         /// <param name="triggerTime">触发时间</param>
//         /// <param name="callback">回调</param>
//         /// <param name="eventName">事件名称</param>
//         public AbilityTimelinePhase AddEvent(float triggerTime, Action<IAbilityPipelineContext> callback, string eventName = null)
//         {
//             _events.Add(new TimelineEvent
//             {
//                 TriggerTime = triggerTime,
//                 Callback = callback,
//                 EventName = eventName,
//                 IsTriggered = false
//             });
            
//             // 按时间排序
//             _events.Sort((a, b) => a.TriggerTime.CompareTo(b.TriggerTime));
//             return this;
//         }

//         /// <summary>
//         /// 添加时间轴阶段（在指定时间执行另一个阶段）
//         /// </summary>
//         public AbilityTimelinePhase AddPhaseAt(float triggerTime, IAbilityPipelinePhase phase)
//         {
//             return AddEvent(triggerTime, ctx => phase.Execute(ctx), phase.PhaseId?.ToString());
//         }

//         protected override void OnEnter(IAbilityPipelineContext context)
//         {
//             base.OnEnter(context);
//             _nextEventIndex = 0;
            
//             // 重置所有事件状态
//             for (int i = 0; i < _events.Count; i++)
//             {
//                 var evt = _events[i];
//                 evt.IsTriggered = false;
//                 _events[i] = evt;
//             }
//         }

//         protected override void OnExecute(IAbilityPipelineContext context)
//         {
//             // 初始执行时检查是否有0时刻的事件
//             TriggerPendingEvents(context);
//         }

//         protected override void OnTick(IAbilityPipelineContext context, float deltaTime)
//         {
//             TriggerPendingEvents(context);
//         }

//         private void TriggerPendingEvents(IAbilityPipelineContext context)
//         {
//             float currentTime = CurrentTime;
            
//             while (_nextEventIndex < _events.Count)
//             {
//                 var evt = _events[_nextEventIndex];
//                 if (evt.TriggerTime <= currentTime)
//                 {
//                     // 标记为已触发
//                     evt.IsTriggered = true;
//                     _events[_nextEventIndex] = evt;
                    
//                     // 执行回调
//                     try
//                     {
//                         evt.Callback?.Invoke(context);
//                     }
//                     catch (Exception e)
//                     {
//                         //LogUtil.LogError($"[Timeline] Event '{evt.EventName}' at {evt.TriggerTime}s error: {e.Message}");
//                     }
                    
//                     _nextEventIndex++;
//                 }
//                 else
//                 {
//                     break;
//                 }
//             }
//         }

//         protected override void Complete(IAbilityPipelineContext context)
//         {
//             if (IsLoop && !context.IsAborted)
//             {
//                 // 循环模式：重置时间和事件
//                 _elapsedTime = 0f;
//                 _nextEventIndex = 0;
//                 for (int i = 0; i < _events.Count; i++)
//                 {
//                     var evt = _events[i];
//                     evt.IsTriggered = false;
//                     _events[i] = evt;
//                 }
//             }
//             else
//             {
//                 base.Complete(context);
//             }
//         }

//         /// <summary>
//         /// 跳转到指定时间
//         /// </summary>
//         public void SeekTo(float time, IAbilityPipelineContext context)
//         {
//             _elapsedTime = time / TimeScale;
            
//             // 重新计算需要触发的事件
//             _nextEventIndex = 0;
//             for (int i = 0; i < _events.Count; i++)
//             {
//                 var evt = _events[i];
//                 if (evt.TriggerTime <= time)
//                 {
//                     evt.IsTriggered = true;
//                     _nextEventIndex = i + 1;
//                 }
//                 else
//                 {
//                     evt.IsTriggered = false;
//                 }
//                 _events[i] = evt;
//             }
//         }

//         /// <summary>
//         /// 创建时间轴阶段
//         /// </summary>
//         public static AbilityTimelinePhase Create(float duration)
//         {
//             return new AbilityTimelinePhase(duration);
//         }
//     }
// }

