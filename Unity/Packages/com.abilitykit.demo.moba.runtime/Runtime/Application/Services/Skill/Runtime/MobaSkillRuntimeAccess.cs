using System;
using AbilityKit.Ability.FrameSync;
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
            if (candidate == null || candidate.SkillId != skillId) return false;

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
    }
}
