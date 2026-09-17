using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services
{
    internal readonly struct SkillCastPreparationInput
    {
        public SkillCastPreparationInput(int actorId, int skillId, int slot, in Vec3 aimPos,
            in Vec3 aimDir, bool hasAim, int targetActorId, long diagnosticCommandId = 0L)
        {
            ActorId = actorId;
            SkillId = skillId;
            Slot = slot;
            AimPos = aimPos;
            AimDir = aimDir;
            HasAim = hasAim;
            TargetActorId = targetActorId;
            DiagnosticCommandId = diagnosticCommandId;
        }

        public int ActorId { get; }
        public int SkillId { get; }
        public int Slot { get; }
        public Vec3 AimPos { get; }
        public Vec3 AimDir { get; }
        public bool HasAim { get; }
        public int TargetActorId { get; }
        public long DiagnosticCommandId { get; }
    }
}
