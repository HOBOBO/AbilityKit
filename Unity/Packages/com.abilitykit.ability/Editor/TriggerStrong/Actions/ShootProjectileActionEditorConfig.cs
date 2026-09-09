using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.ShootProjectile, "发射投射物", "行为/投射物", 0)]
    public sealed class ShootProjectileActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.ShootProjectile;

        [LabelText("发射器 ID")]
        public int LauncherId;

        [LabelText("投射物 ID")]
        public int ProjectileId;

        protected override string GetTitleSuffix()
        {
            return ProjectileId > 0 ? ProjectileId.ToString() : null;
        }

        public override ActionConfigBase ToRuntimeConfig()
        {
            return new ShootProjectileActionConfig
            {
                LauncherId = LauncherId,
                ProjectileId = ProjectileId,
            };
        }
    }
}
