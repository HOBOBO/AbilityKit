using System;
using AbilityKit.Game.Editor.Gizmos;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 管理 Scene View 的 MOBA 叠加层，复用绘制器的个人设置和缓存，不操作战斗会话。
    /// </summary>
    internal static class BattleDebugSceneOverlayPanel
    {
        private static readonly (GUIContent Label, Func<bool> Read, Action<bool> Write)[] Layers =
        {
            (new GUIContent("碰撞世界"), MobaSceneGizmoSettings.IsCollisionEnabled, MobaSceneGizmoSettings.SetCollisionEnabled),
            (new GUIContent("攻击范围"), MobaSceneGizmoSettings.IsAttackEnabled, MobaSceneGizmoSettings.SetAttackEnabled),
            (new GUIContent("Buff 范围"), MobaSceneGizmoSettings.IsBuffEnabled, MobaSceneGizmoSettings.SetBuffEnabled),
            (new GUIContent("出生区域"), MobaSceneGizmoSettings.IsSpawnEnabled, MobaSceneGizmoSettings.SetSpawnEnabled),
            (new GUIContent("导航网格"), MobaSceneGizmoSettings.IsNavigationEnabled, MobaSceneGizmoSettings.SetNavigationEnabled),
            (new GUIContent("寻路路径"), MobaSceneGizmoSettings.IsPathEnabled, MobaSceneGizmoSettings.SetPathEnabled),
            (new GUIContent("活动 AOE 区域"), MobaSceneGizmoSettings.IsActiveAoeEnabled, MobaSceneGizmoSettings.SetActiveAoeEnabled),
        };

        /// <summary>
        /// 显示个人绘制偏好，只在操作开关或按钮时写入偏好或清理缓存。
        /// </summary>
        internal static void Draw()
        {
            EditorGUILayout.LabelField("场景叠加", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "可在编辑模式调整个人设置。启动战斗后在 Scene View 查看绘制结果；关闭本页不会关闭叠加层。",
                MessageType.Info);

            foreach (var layer in Layers)
            {
                var enabled = layer.Read();
                var next = EditorGUILayout.ToggleLeft(layer.Label, enabled);
                if (next == enabled) continue;

                layer.Write(next);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("恢复默认设置"))
                MobaSceneGizmoSettings.Reset();
            if (GUILayout.Button("清空出生点缓存"))
            {
                ActorCombatGizmoDrawer.ClearSpawnCache();
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox("清空缓存后，绘制器会在下一次采集时把当前角色位置记录为出生点。", MessageType.None);
        }
    }
}
