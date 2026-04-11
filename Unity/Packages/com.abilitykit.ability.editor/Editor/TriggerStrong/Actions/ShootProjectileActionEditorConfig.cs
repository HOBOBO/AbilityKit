using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.ShootProjectile, "发射子弹", "行为/Projectile", 0)]
    public sealed class ShootProjectileActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.ShootProjectile;

        [LabelText("发射器Id")]
        public int LauncherId;

        [LabelText("子弹Id")]
        public int ProjectileId;

        protected override string GetTitleSuffix()
        {
            return ProjectileId > 0 ? ProjectileId.ToString() : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ShootProjectileActionConfig
            {
                LauncherId = LauncherId,
                ProjectileId = ProjectileId,
            };
        }
    }
}
