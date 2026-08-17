namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleSkillAimSubmitInput
    {
        public readonly int Slot;
        public readonly float AimPosX;
        public readonly float AimPosY;
        public readonly float AimPosZ;
        public readonly float AimDirX;
        public readonly float AimDirY;
        public readonly float AimDirZ;

        public BattleSkillAimSubmitInput(
            int slot,
            float aimPosX,
            float aimPosY,
            float aimPosZ,
            float aimDirX,
            float aimDirY,
            float aimDirZ)
        {
            Slot = slot;
            AimPosX = aimPosX;
            AimPosY = aimPosY;
            AimPosZ = aimPosZ;
            AimDirX = aimDirX;
            AimDirY = aimDirY;
            AimDirZ = aimDirZ;
        }
    }

    internal static class BattleHudInputSource
    {
        public static bool TryReadMove(
            IBattleHudInputReadPort input,
            out float dx,
            out float dz)
        {
            if (input != null) return input.TryReadMove(out dx, out dz);

            dx = 0f;
            dz = 0f;
            return false;
        }

        public static bool TryConsumeSkillClick(
            IBattleHudInputReadPort input,
            out int slot)
        {
            if (input != null) return input.TryConsumeSkillClick(out slot);

            slot = 0;
            return false;
        }

        public static bool TryConsumeSkillAimSubmit(
            IBattleHudInputReadPort input,
            out BattleSkillAimSubmitInput submitted)
        {
            if (input != null) return input.TryConsumeSkillAimSubmit(out submitted);

            submitted = default;
            return false;
        }
    }
}
