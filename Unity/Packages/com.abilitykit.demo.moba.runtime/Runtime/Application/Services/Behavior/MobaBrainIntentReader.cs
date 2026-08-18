using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    public static class MobaBrainIntentReader
    {
        public static MobaActorIntent Read(IBehaviorOutput output)
        {
            var intent = MobaActorIntent.Hold;
            if (output == null) return intent;

            var movement = output.Movement;
            if (movement.HasValue && movement.Value.TargetPosition.HasValue)
            {
                var target = movement.Value.TargetPosition.Value;
                intent = MobaActorIntent.MoveTo(in target);
            }

            var events = output.PendingEvents;
            for (var i = 0; events != null && i < events.Count; i++)
            {
                var evt = events[i];
                if (!string.Equals(evt.EventId, MobaBrainExecutor.SkillCastEventId, StringComparison.Ordinal))
                    continue;
                var payload = evt.Payload;
                if (payload == null || !TryGet(payload, MobaBrainExecutor.SkillSlotParam, out int slot) || slot <= 0)
                    continue;
                TryGet(payload, MobaBrainExecutor.SkillIdParam, out int skillId);
                TryGet(payload, MobaBrainExecutor.TargetActorIdParam, out int targetActorId);
                TryGet(payload, MobaBrainExecutor.AimPositionParam, out Vec3 aimPosition);
                if (!TryGet(payload, MobaBrainExecutor.AimDirectionParam, out Vec3 aimDirection))
                    aimDirection = Vec3.Forward;
                return intent.WithCast(slot, skillId, targetActorId, in aimPosition, in aimDirection);
            }

            return intent;
        }

        private static bool TryGet<T>(IReadOnlyDictionary<string, object> payload, string key, out T value)
        {
            if (payload.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }
}
