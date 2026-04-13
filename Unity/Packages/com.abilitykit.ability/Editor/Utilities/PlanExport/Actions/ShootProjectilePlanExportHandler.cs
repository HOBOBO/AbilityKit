#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Editor.Utilities
{
    [TriggerPlanExportHandler(order: 10)]
    internal sealed class ShootProjectilePlanExportHandler : ITriggerPlanExportActionHandler
    {
        public bool TryConvertActionNode(ActionEditorConfigBase strongNode, out JsonActionEditorConfig jsonNode)
        {
            jsonNode = null;
            return false;
        }

        public bool TryCompileAction(JsonActionEditorConfig action,
            Dictionary<int, string> stringTable,
            Func<string, int> payloadFieldIdResolver,
            Func<string, ActionId> actionIdResolver,
            out ActionCallPlan[] plans)
        {
            plans = null;
            if (action == null) return false;
            if (!string.Equals(action.TypeValue, TriggerActionTypes.ShootProjectile, StringComparison.Ordinal)) return false;
            if (actionIdResolver == null) return false;

            var id = actionIdResolver(action.TypeValue);
            if (id.Value == 0) return false;

            // 具名参数
            var args = new Dictionary<string, ActionArgValue>();

            if (action.Args != null)
            {
                if (PlanExportArgReadUtil.TryReadDouble(action.Args, "launcherId", out var launcherId))
                    args["launcher_id"] = ActionArgValue.OfConst(launcherId, "launcher_id");
                else if (PlanExportArgReadUtil.TryReadDouble(action.Args, "LauncherId", out var launcherId2))
                    args["launcher_id"] = ActionArgValue.OfConst(launcherId2, "launcher_id");

                if (PlanExportArgReadUtil.TryReadDouble(action.Args, "projectileId", out var projectileId))
                    args["projectile_id"] = ActionArgValue.OfConst(projectileId, "projectile_id");
                else if (PlanExportArgReadUtil.TryReadDouble(action.Args, "ProjectileId", out var projectileId2))
                    args["projectile_id"] = ActionArgValue.OfConst(projectileId2, "projectile_id");
            }

            plans = new[] { ActionCallPlan.WithArgs(id, args) };
            return true;
        }
    }
}
#endif
