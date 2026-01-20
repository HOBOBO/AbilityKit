using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.ShootProjectile, "发射子弹", "行为/Projectile", 0)]
    public sealed class ShootProjectileActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.ShootProjectile;

        [LabelText("发射器类型")]
        public ProjectileEmitterType EmitterType = ProjectileEmitterType.Linear;

        [LabelText("子弹模板Id")]
        public int ProjectileCode;

        [LabelText("速度")]
        public float Speed = 10f;

        [LabelText("生命周期(帧)")]
        public int LifetimeFrames = 30;

        [LabelText("最大距离(0表示不用)")]
        public float MaxDistance = 0f;

        protected override string GetTitleSuffix()
        {
            return ProjectileCode > 0 ? ProjectileCode.ToString() : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ShootProjectileActionConfig
            {
                EmitterType = EmitterType,
                ProjectileCode = ProjectileCode,
                Speed = Speed,
                LifetimeFrames = LifetimeFrames,
                MaxDistance = MaxDistance,
            };
        }
    }
}
