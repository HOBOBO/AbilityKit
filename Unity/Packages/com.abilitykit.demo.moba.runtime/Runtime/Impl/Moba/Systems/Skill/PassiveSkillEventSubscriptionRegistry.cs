using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    internal static class PassiveSkillEventSubscriptionRegistry
    {
        public static bool TrySubscribe(
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            string eventId,
            Action<SkillCastContext> onSkillCast,
            out IDisposable sub)
        {
            sub = null;
            if (eventBus == null) return false;
            if (string.IsNullOrEmpty(eventId)) return false;

            // 当前仅支持技能事件：payload 为 SkillCastContext。
            if (eventId.StartsWith("skill.", StringComparison.Ordinal))
            {
                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<SkillCastContext>(eid);
                sub = eventBus.Subscribe(key, args => onSkillCast?.Invoke(args));
                return true;
            }

            Log.Warning($"[PassiveSkillEventSubscriptionRegistry] Unsupported eventId (no payload mapping): {eventId}");
            return false;
        }
    }
}
