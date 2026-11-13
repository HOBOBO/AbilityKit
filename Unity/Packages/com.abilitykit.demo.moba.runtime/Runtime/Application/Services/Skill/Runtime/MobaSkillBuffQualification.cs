using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed partial class MobaSkillCastRuntimeService
    {
        private static MobaSkillRuntimeBlackboardKey QualificationKey(int id, string name, MobaSkillRuntimeValueKind kind) =>
            new MobaSkillRuntimeBlackboardKey(id, "skill.buffQualification." + name, kind, ownerModuleId: 1002);
        private static readonly MobaSkillRuntimeBlackboardKey QualificationBuff = QualificationKey(130, "buff", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey QualificationSource = QualificationKey(131, "source", MobaSkillRuntimeValueKind.Int);
        private static readonly MobaSkillRuntimeBlackboardKey QualificationContext = QualificationKey(132, "context", MobaSkillRuntimeValueKind.Long);
        private static readonly MobaSkillRuntimeBlackboardKey QualificationClaimed = QualificationKey(133, "claimed", MobaSkillRuntimeValueKind.Bool);

        public bool TryReserveBuffQualification(in MobaSkillCastRuntimeHandle handle, int buffId, ActorEntity owner)
        {
            if (buffId <= 0 || owner == null || !owner.hasActorId || !owner.hasBuffs || owner.buffs.Active == null ||
                !TryGet(in handle, out var cast) || cast.RootContextId != handle.RootContextId || cast.CasterActorId != owner.actorId.Value ||
                cast.Blackboard.TryGetInt(in QualificationBuff, out _)) return false;
            BuffRuntime selected = null;
            foreach (var buff in owner.buffs.Active)
            {
                if (buff == null || buff.BuffId != buffId || buff.SourceContextId == 0) continue;
                if (selected != null) return false; // Ambiguous qualifications must be resolved by content.
                selected = buff;
            }
            if (selected == null) return false;
            foreach (var pair in _runtimes)
            {
                var other = pair.Value;
                if (other.IsEnded || other.IsEnding || other.CasterActorId != cast.CasterActorId ||
                    other.Blackboard.TryGetBool(in QualificationClaimed, out var claimed) && claimed) continue;
                if (other.Blackboard.TryGetInt(in QualificationBuff, out var id) && id == buffId &&
                    other.Blackboard.TryGetInt(in QualificationSource, out var source) && source == selected.SourceId &&
                    other.Blackboard.TryGetLong(in QualificationContext, out var context) && context == selected.SourceContextId) return false;
            }
            cast.Blackboard.SetInt(in QualificationBuff, buffId);
            cast.Blackboard.SetInt(in QualificationSource, selected.SourceId);
            cast.Blackboard.SetLong(in QualificationContext, selected.SourceContextId);
            return true;
        }

        public bool TryClaimBuffQualification(in MobaSkillCastRuntimeHandle handle, ActorEntity owner,
            out int buffId, out int sourceActorId, out long sourceContextId)
        {
            buffId = 0; sourceActorId = 0; sourceContextId = 0;
            if (owner == null || !owner.hasActorId || !owner.hasBuffs || owner.buffs.Active == null ||
                !TryGet(in handle, out var cast) || cast.RootContextId != handle.RootContextId || cast.IsEnding || cast.IsEnded || cast.CasterActorId != owner.actorId.Value ||
                cast.Blackboard.TryGetBool(in QualificationClaimed, out var claimed) && claimed ||
                !cast.Blackboard.TryGetInt(in QualificationBuff, out buffId) ||
                !cast.Blackboard.TryGetInt(in QualificationSource, out sourceActorId) ||
                !cast.Blackboard.TryGetLong(in QualificationContext, out sourceContextId)) return false;
            foreach (var buff in owner.buffs.Active)
            {
                if (buff == null || buff.BuffId != buffId || buff.SourceId != sourceActorId || buff.SourceContextId != sourceContextId) continue;
                // Latch before remove callbacks or nested effects. No HP-delta requirement: shields still count as contact.
                cast.Blackboard.SetBool(in QualificationClaimed, true);
                return true;
            }
            return false;
        }
    }
}
