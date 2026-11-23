using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaSkillRuntimeAccess
    {
        public static long GetCurrentTimeMs(IFrameTime time)
        {
            if (time == null) return 0L;
            // 整数域取毫秒（FrameTime 内部 raw×1000 右移），不经 float 视图中转，
            // 与累加/对齐路径完全一致（原 Round(Time*1000f) 存在 .5 边界与量化语义弱的问题）。
            if (time is FrameTime frameTime)
            {
                return frameTime.TimeMilliseconds;
            }

            return (long)MathF.Round(time.Time * 1000f);
        }

        public static bool TryGetActiveSkill(
            MobaActorLookupService actors,
            int actorId,
            int skillSlot,
            int skillId,
            out ActiveSkillRuntime runtime)
        {
            runtime = null;
            if (actors == null || actorId <= 0 || skillSlot <= 0 || skillId <= 0) return false;
            if (!actors.TryGetActorEntity(actorId, out var actor) || actor == null) return false;
            if (!actor.hasSkillLoadout || actor.skillLoadout.ActiveSkills == null) return false;

            var index = skillSlot - 1;
            if (index < 0 || index >= actor.skillLoadout.ActiveSkills.Length) return false;

            var candidate = actor.skillLoadout.ActiveSkills[index];
            if (candidate == null || candidate.SkillId <= 0) return false;

            runtime = candidate;
            return true;
        }

        public static bool TrySetActiveSkillCooldown(
            MobaActorLookupService actors,
            int actorId,
            int skillSlot,
            int skillId,
            long cooldownEndTimeMs,
            int cooldownDurationMs)
        {
            if (!TryGetActiveSkill(actors, actorId, skillSlot, skillId, out var runtime)) return false;
            runtime.CooldownEndTimeMs = cooldownEndTimeMs;
            runtime.CooldownDurationMs = Math.Max(0, cooldownDurationMs);
            return true;
        }

        /// <summary>
        /// Restores resources used by skill casts for the local debug reset command.
        /// Health is intentionally excluded so resetting a test cooldown does not
        /// also heal damage that was part of the scenario.
        /// </summary>
        public static bool TryRestoreSkillResources(
            MobaActorLookupService actors,
            int actorId,
            out int restoredCount)
        {
            restoredCount = 0;
            if (actors == null || actorId <= 0 || !actors.TryGetActorEntity(actorId, out var actor) || actor == null ||
                !actor.hasResourceContainer || actor.resourceContainer.Value?.Map == null)
            {
                return false;
            }

            foreach (var pair in actor.resourceContainer.Value.Map)
            {
                if (pair.Key == ResourceType.None || pair.Key == ResourceType.Hp || pair.Value == null ||
                    pair.Value.LastMax <= Fixed64.Zero)
                {
                    continue;
                }

                pair.Value.Current = pair.Value.LastMax;
                restoredCount++;
            }

            if (restoredCount > 0)
            {
                MobaResourceAttributeContextProjector.Refresh(actor);
            }

            return true;
        }
    }
}
