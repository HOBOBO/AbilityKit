using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// Scene Gizmo 切换菜单（"Moba/Gizmos/"）。
    /// - 三个独立 Toggle 控制 Attack / Buff / Spawn 可视化
    /// - "Reset" 把所有位恢复为默认
    /// - "Clear Spawn Cache" 强制清空出生点缓存（用于切换战斗后重新捕获）
    /// </summary>
    internal static class SceneGizmoSettingsMenu
    {
        private const string MenuRoot = "Moba/Gizmos/";

        [MenuItem(MenuRoot + "Attack Range", priority = 100)]
        private static void ToggleAttack()
        {
            var enabled = !MobaSceneGizmoSettings.IsAttackEnabled();
            MobaSceneGizmoSettings.SetAttackEnabled(enabled);
            ShowEnabledToast("Attack Range", enabled);
        }

        [MenuItem(MenuRoot + "Attack Range", validate = true)]
        private static bool ValidateAttack()
        {
            Menu.SetChecked(MenuRoot + "Attack Range", MobaSceneGizmoSettings.IsAttackEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Buff Range", priority = 101)]
        private static void ToggleBuff()
        {
            var enabled = !MobaSceneGizmoSettings.IsBuffEnabled();
            MobaSceneGizmoSettings.SetBuffEnabled(enabled);
            ShowEnabledToast("Buff Range", enabled);
        }

        [MenuItem(MenuRoot + "Buff Range", validate = true)]
        private static bool ValidateBuff()
        {
            Menu.SetChecked(MenuRoot + "Buff Range", MobaSceneGizmoSettings.IsBuffEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Spawn Area", priority = 102)]
        private static void ToggleSpawn()
        {
            var enabled = !MobaSceneGizmoSettings.IsSpawnEnabled();
            MobaSceneGizmoSettings.SetSpawnEnabled(enabled);
            ShowEnabledToast("Spawn Area", enabled);
        }

        [MenuItem(MenuRoot + "Spawn Area", validate = true)]
        private static bool ValidateSpawn()
        {
            Menu.SetChecked(MenuRoot + "Spawn Area", MobaSceneGizmoSettings.IsSpawnEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Navigation Grid", priority = 110)]
        private static void ToggleNavGrid()
        {
            var enabled = !MobaSceneGizmoSettings.IsNavigationEnabled();
            MobaSceneGizmoSettings.SetNavigationEnabled(enabled);
            ShowEnabledToast("Navigation Grid", enabled);
        }

        [MenuItem(MenuRoot + "Navigation Grid", validate = true)]
        private static bool ValidateNavGrid()
        {
            Menu.SetChecked(MenuRoot + "Navigation Grid", MobaSceneGizmoSettings.IsNavigationEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Path Lines", priority = 111)]
        private static void TogglePath()
        {
            var enabled = !MobaSceneGizmoSettings.IsPathEnabled();
            MobaSceneGizmoSettings.SetPathEnabled(enabled);
            ShowEnabledToast("Path Lines", enabled);
        }

        [MenuItem(MenuRoot + "Path Lines", validate = true)]
        private static bool ValidatePath()
        {
            Menu.SetChecked(MenuRoot + "Path Lines", MobaSceneGizmoSettings.IsPathEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Reset Defaults", priority = 200)]
        private static void ResetDefaults()
        {
            MobaSceneGizmoSettings.Reset();
            EditorUtility.DisplayDialog("Scene Gizmos", "已恢复默认的 Gizmo 开关（Attack + Buff）。", "OK");
        }

        [MenuItem(MenuRoot + "Clear Spawn Cache", priority = 201)]
        private static void ClearSpawnCacheMenu()
        {
            ActorCombatGizmoDrawer.ClearSpawnCache();
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Scene Gizmos", "已清空 Spawn Cache。下一帧将重新捕获当前 Actor 的位置作为出生点。", "OK");
        }

        private static void ShowEnabledToast(string name, bool enabled)
        {
            UnityEngine.Debug.Log($"[SceneGizmos] {name} = {(enabled ? "ON" : "OFF")}");
            SceneView.RepaintAll();
        }
    }
}