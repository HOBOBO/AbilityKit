using System;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using UnityEngine;

namespace AbilityKit.Triggering.Example
{
    /// <summary>
    /// 触发器 Cue 使用完整示例
    /// 演示如何将逻辑层（Trigger）与表现层（VFX / SFX / UI）分离
    /// </summary>
    public static class TriggerCueExample
    {
        // ========== 事件和 Payload ==========

        public readonly struct DamageEvent
        {
            public readonly int TargetId;
            public readonly double Damage;
            public readonly bool IsCritical;

            public DamageEvent(int targetId, double damage, bool isCritical)
            {
                TargetId = targetId;
                Damage = damage;
                IsCritical = isCritical;
            }
        }

        // ========== 表现层 Cue 实现 ==========

        /// <summary>
        /// 伤害触发器的视觉音效 Cue
        /// 职责：在触发器的各个生命周期节点播放对应的 VFX / SFX / UI 反馈
        /// 特点：不参与任何逻辑判断，仅处理表现
        /// </summary>
        public sealed class DamageTriggerCue : ITriggerCue
        {
            private readonly DamageVfxConfig _vfxConfig;
            private readonly DamageSfxConfig _sfxConfig;

            public DamageTriggerCue(DamageVfxConfig vfxConfig, DamageSfxConfig sfxConfig)
            {
                _vfxConfig = vfxConfig;
                _sfxConfig = sfxConfig;
            }

            public void OnConditionPassed(in TriggerCueContext context)
            {
                Debug.Log($"[Cue] 伤害条件通过，准备播放预警特效 (Target={context.EventId}, Priority={context.Priority})");
                // 示例：播放预警光圈 / 震动 / 预警音效
                // VfxSystem.Play(_vfxConfig.WarningVfx, targetActor);
                // AudioSystem.Play(_sfxConfig.WarningSfx, targetActor);
            }

            public void OnConditionFailed(in TriggerCueContext context)
            {
                Debug.Log($"[Cue] 伤害条件失败，播放落空表情 (TriggerId={context.TriggerId})");
                // 示例：目标闪避表情 / 落空音效
                // EmojiSystem.Play(targetActor, "miss");
                // AudioSystem.Play(_sfxConfig.MissSfx, targetActor);
            }

            public void OnBeforeAction(in TriggerCueContext context, int actionIndex)
            {
                Debug.Log($"[Cue] 即将执行第 {actionIndex} 个动作 (TriggerId={context.TriggerId}, Phase={context.Phase})");
                // 示例：播放前摇动画 / 施法特效
                // AnimatorSystem.SetTrigger(targetActor, "casting");
            }

            public void OnExecuted(in TriggerCueContext context)
            {
                Debug.Log($"[Cue] 伤害触发器执行完成 (TriggerId={context.TriggerId}, ConditionPassed={context.InterruptConditionPassed})");
                // 示例：播放命中特效 / 打击音效 / 飘字
                // context.Args 是 object，强制转换回具体 Payload 类型
                var dmg = (DamageEvent)context.Args;
                var effect = dmg.IsCritical ? _vfxConfig.CriticalHitVfx : _vfxConfig.NormalHitVfx;
                Debug.Log($"[Cue] 播放命中特效: {effect} (Damage={dmg.Damage}, IsCritical={dmg.IsCritical})");
                // VfxSystem.Play(effect, targetActor);
                // AudioSystem.Play(_sfxConfig.HitSfx, targetActor);
                // UISystem.ShowDamageNumber(targetActor, dmg.Damage, dmg.IsCritical);
            }

            public void OnInterrupted(in TriggerCueContext context)
            {
                Debug.Log($"[Cue] 伤害触发器被打断 (TriggerId={context.TriggerId}, Reason={context.InterruptReason}, Source={context.InterruptSourceName})");
                // 示例：播放打断特效 / 取消前摇
                // AnimatorSystem.Cancel(targetActor, "casting");
                // AudioSystem.Play(_sfxConfig.InterruptSfx, targetActor);
            }

            public void OnSkipped(in TriggerCueContext context)
            {
                Debug.Log($"[Cue] 伤害触发器被优先级打断跳过 (TriggerId={context.TriggerId}, InterruptTriggerId={context.InterruptTriggerId}, Reason={context.InterruptReason})");
                // 示例：显示"被高优先级打断"提示
                // UISystem.ShowHint(targetActor, "Blocked by higher priority!");
            }
        }

        // ========== Cue 配置数据 ==========

        public readonly struct DamageVfxConfig
        {
            public readonly string WarningVfx;
            public readonly string NormalHitVfx;
            public readonly string CriticalHitVfx;

            public DamageVfxConfig(string warningVfx, string normalHitVfx, string criticalHitVfx)
            {
                WarningVfx = warningVfx;
                NormalHitVfx = normalHitVfx;
                CriticalHitVfx = criticalHitVfx;
            }
        }

        public readonly struct DamageSfxConfig
        {
            public readonly string WarningSfx;
            public readonly string HitSfx;
            public readonly string CriticalSfx;
            public readonly string MissSfx;
            public readonly string InterruptSfx;

            public DamageSfxConfig(string warningSfx, string hitSfx, string criticalSfx, string missSfx, string interruptSfx)
            {
                WarningSfx = warningSfx;
                HitSfx = hitSfx;
                CriticalSfx = criticalSfx;
                MissSfx = missSfx;
                InterruptSfx = interruptSfx;
            }
        }

        // ========== 使用示例 ==========

        public static void Run()
        {
            // 1. 创建 EventBus 和 TriggerRunner（使用命名参数跳过不需要的可选依赖）
            var bus = new EventBus();
            var runner = new TriggerRunner<DefaultTCtx>(
                bus,
                new FunctionRegistry(),
                new ActionRegistry());

            // 2. 注册伤害触发器，使用 DSL 配置 Cue
            var damageEventId = Eventing.StableStringId.Get("event:damage");
            var eventKey = new EventKey<DamageEvent>(damageEventId);

            var vfxConfig = new DamageVfxConfig("warning_circle.prefab", "hit_normal.prefab", "hit_critical.prefab");
            var sfxConfig = new DamageSfxConfig("sfx_warn.wav", "sfx_hit.wav", "sfx_crit.wav", "sfx_miss.wav", "sfx_interrupt.wav");
            var damageCue = new DamageTriggerCue(vfxConfig, sfxConfig);

            var dslPlan = TriggerPlanDsl.Create<DamageEvent>(phase: 0, priority: 10)
                .WithTriggerId(1001)
                .WithNoCondition()                          // 无条件触发（实际项目中通常用 When()）
                .DoConst(new ActionId(1), 0)              // ActionId=1: ApplyDamage
                .WithCue(damageCue)                        // 绑定表现层 Cue
                .Build();

            runner.RegisterPlan(eventKey, dslPlan);

            Debug.Log("=== 示例：触发伤害事件 ===");

            // 3. 通过 EventBus 派发事件，TriggerRunner 自动调度（触发 Cue 回调）
            var damageEvent = new DamageEvent(targetId: 42, damage: 150.0, isCritical: true);
            bus.Publish(eventKey, damageEvent);
            bus.Flush();

            Debug.Log("=== 示例结束 ===");
        }

        // ========== 使用便捷构造器 ==========

        public static void RunWithConstructor()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<DefaultTCtx>(
                bus,
                new FunctionRegistry(),
                new ActionRegistry());

            var damageEventId = Eventing.StableStringId.Get("event:damage2");
            var eventKey = new EventKey<DamageEvent>(damageEventId);

            var damageCue = new DamageTriggerCue(
                new DamageVfxConfig("vfx/warn", "vfx/hit", "vfx/crit"),
                new DamageSfxConfig("audio/warn", "audio/hit", "audio/crit", "audio/miss", "audio/interrupt")
            );

            // 使用便捷构造器（不带 TriggerId/InterruptPriority）
            var plan = new TriggerPlan<DamageEvent>(
                phase: 0,
                priority: 5,
                triggerId: 1002,
                predicateId: default(FunctionId),
                interruptPriority: 0,
                actions: Array.Empty<ActionCallPlan>(),
                cue: damageCue
            );

            runner.RegisterPlan(eventKey, plan);

            Debug.Log("=== 示例：使用构造器 ===");
            bus.Publish(eventKey, new DamageEvent(99, 200, false));
            bus.Flush();
            Debug.Log("=== 示例结束 ===");
        }
    }
}
