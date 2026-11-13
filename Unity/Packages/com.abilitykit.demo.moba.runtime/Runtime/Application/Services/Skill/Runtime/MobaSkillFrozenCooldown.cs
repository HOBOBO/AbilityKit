using System;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed partial class MobaSkillCastRuntimeService
    {
        private static readonly MobaSkillRuntimeBlackboardKey FrozenCooldown =
            new MobaSkillRuntimeBlackboardKey(128, "skill.cooldown.effectiveTotalMs", MobaSkillRuntimeValueKind.Int, ownerModuleId: 1002);

        public void FreezeEffectiveCooldown(in MobaSkillCastRuntimeHandle handle, int cooldownMs)
        {
            if (cooldownMs < 0 || !TryGet(in handle, out var cast) || cast.RootContextId != handle.RootContextId ||
                cast.Blackboard.TryGetInt(in FrozenCooldown, out _))
                throw new InvalidOperationException("Effective cooldown must be frozen exactly once on a live cast.");
            cast.Blackboard.SetInt(in FrozenCooldown, cooldownMs);
        }

        public bool TryResolveCooldownRefund(in MobaSkillCastRuntimeHandle handle, int owner, int skill, int slot,
            double ratio, out int reductionMs)
        {
            reductionMs = 0;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio < 0 || ratio > 1 ||
                !TryGet(in handle, out var cast) || cast.RootContextId != handle.RootContextId ||
                cast.CasterActorId != owner || cast.SkillId != skill || cast.SkillSlot != slot ||
                !cast.Blackboard.TryGetInt(in FrozenCooldown, out var total)) return false;
            reductionMs = (int)Math.Round(total * ratio, MidpointRounding.AwayFromZero);
            return true;
        }
    }
}
