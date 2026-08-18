using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Console.Battle.Input
{
    /// <summary>
    /// 输入特征接口
    /// </summary>
    public interface IInputFeature
    {
        /// <summary>
        /// 本地玩家 ID
        /// </summary>
        int LocalActorId { get; }

        void ApplyIntent(in MobaActorIntent intent);

        /// <summary>
        /// 设置移动输入
        /// </summary>
        void SetMoveInput(float dx, float dz);

        /// <summary>
        /// 点击技能
        /// </summary>
        void ClickSkill(int slot);

        /// <summary>
        /// 瞄准技能
        /// </summary>
        void AimSkill(int slot, float dx, float dz);

        /// <summary>
        /// 释放技能瞄准
        /// </summary>
        void ReleaseSkillAim(int slot, float dx, float dz);
    }
}
