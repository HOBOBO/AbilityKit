namespace AbilityKit.Game.Flow
{
    public interface IBattleHudInputSink
    {
        void BeginHudMove();

        void EndHudMove();

        void SetHudMove(float dx, float dz);

        void SubmitHudSkillClick(int slot);

        void SetHudSkillAim(int slot, float dx, float dz, bool aiming);

        void CancelHudSkillAim();

        void SubmitHudSkillAim(int slot, float dx, float dz, bool includeAimPosition = true);
    }
}
