using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    /// <summary>A role declares eligibility; a live, self-owned Buff is the sole clock and permission source.</summary>
    public sealed class MobaDeathCastBinding
    {
        public int PermissionBuffId { get; set; }
        public List<int> RetainedBuffIds { get; set; } = new List<int>();
        public List<MobaDeathCastSkillBinding> Skills { get; set; } = new List<MobaDeathCastSkillBinding>();

        public void Validate()
        {
            if (PermissionBuffId <= 0 || RetainedBuffIds == null || Skills == null || Skills.Count == 0)
                throw new InvalidOperationException("Death casting requires a permission Buff and explicit skill eligibility.");
            var ids = new HashSet<int>();
            foreach (var id in RetainedBuffIds)
                if (id <= 0 || id == PermissionBuffId || !ids.Add(id))
                    throw new InvalidOperationException("Invalid death casting retained Buff.");
            ids.Clear();
            foreach (var skill in Skills)
                if (skill == null || skill.SkillId <= 0 || skill.MinimumCompletionMs < 0 || !ids.Add(skill.SkillId))
                    throw new InvalidOperationException("Invalid death casting skill eligibility.");
        }

        public MobaDeathCastSkillBinding Skill(int skillId) => Skills.Find(s => s.SkillId == skillId);

        public bool TryGetPermission(global::ActorEntity actor, out BuffRuntime permission)
        {
            permission = null;
            if (actor == null || !actor.hasActorId || !actor.hasBuffs || actor.buffs.Active == null) return false;
            foreach (var buff in actor.buffs.Active)
                if (buff != null && buff.BuffId == PermissionBuffId && buff.SourceId == actor.actorId.Value &&
                    buff.Continuous != null && buff.Continuous.IsActive && buff.Continuous.RemainingRaw > 0)
                {
                    permission = buff;
                    return true;
                }
            return false;
        }

        public bool CanCast(global::ActorEntity actor, int skillId, bool admission, float pendingAdvanceSeconds = 0f)
        {
            var skill = Skill(skillId);
            if (skill == null || !TryGetPermission(actor, out var permission)) return false;
            var remaining = permission.Continuous.RemainingRaw;
            // Strict boundary: expiration wins when submission would land at the deadline.
            var advance = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(Math.Max(0f, pendingAdvanceSeconds)).RawValue;
            var minimum = admission ? Fixed64.FromDouble(skill.MinimumCompletionMs / 1000d).RawValue : 0L;
            return remaining.HasValue && remaining.Value > advance && remaining.Value - advance > minimum;
        }

        public bool PreserveOnDeath(global::ActorEntity actor, BuffRuntime buff)
        {
            return buff != null && actor != null && actor.hasActorId && buff.SourceId == actor.actorId.Value &&
                (buff.BuffId == PermissionBuffId || RetainedBuffIds.Contains(buff.BuffId)) &&
                TryGetPermission(actor, out _);
        }
    }

    public sealed class MobaDeathCastSkillBinding
    {
        public int SkillId { get; set; }
        public int MinimumCompletionMs { get; set; }
    }
}
