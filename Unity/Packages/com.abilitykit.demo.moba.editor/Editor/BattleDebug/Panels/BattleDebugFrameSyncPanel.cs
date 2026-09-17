using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugFrameSyncContextResolver
    {
        public static bool TryResolve(in BattleDebugContext ctx, out BattleContext context)
        {
            context = null;
            if (ctx.IsOffline || !EditorApplication.isPlaying || ctx.Facade == null ||
                !ctx.Facade.TryGetSession(out var session) || session == null)
                return false;

            var worldId = session.WorldId.ToString();
            return BattleFlowDebugProvider.TryGetContext(worldId, out context) &&
                   context != null && ReferenceEquals(context.Session, session) &&
                   string.Equals(context.Plan.World.WorldId, worldId, System.StringComparison.Ordinal);
        }
    }

    [BattleDebugModule(
        BattleDebugModuleIds.FrameSyncOverview,
        "帧同步",
        Sources = BattleDebugModuleSourceSupport.Live,
        Selections = BattleDebugModuleSelectionSupport.None)]
    internal sealed class BattleDebugFrameSyncPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        public string Name => "帧同步/总览";
        public int Order => 50;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => false;

        public bool IsVisible(in BattleDebugContext ctx)
        {
            return BattleDebugFrameSyncContextResolver.TryResolve(in ctx, out _);
        }

        public void Draw(in BattleDebugContext ctx)
        {
            if (!BattleDebugFrameSyncContextResolver.TryResolve(in ctx, out var flowCtx))
            {
                EditorGUILayout.HelpBox("战斗流程调试数据源为空。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("世界ID", flowCtx.Plan.World.WorldId);
            EditorGUILayout.LabelField("最近帧", flowCtx.LastFrame.ToString());

            EditorGUILayout.LabelField("运行时世界ID", flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId.ToString() : "（无）");

            if (flowCtx.PredictionReconcileControl != null)
            {
                var wid = flowCtx.HasRuntimeWorldId ? flowCtx.RuntimeWorldId : new WorldId(flowCtx.Plan.World.WorldId);

                if (flowCtx.PredictionReconcileControl.TryGetReconcileEnabled(wid, out var enabled))
                {
                    EditorGUILayout.LabelField("对账开关", BattleDebugDisplayText.Bool(enabled));
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("恢复"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.World.WorldId));
                }

                if (GUILayout.Button("关闭对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, false);

                    if (flowCtx.HasRuntimeWorldId)
                    {
                        flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
                    }

                    flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.World.WorldId));
                }

                if (GUILayout.Button("开启对账"))
                {
                    flowCtx.PredictionReconcileControl.SetReconcileEnabled(wid, true);
                }
                EditorGUILayout.EndHorizontal();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var canInject = BattleSessionFeature.TryGetDebugForceClientHashMismatch(flowCtx, out var forced);
            EditorGUILayout.LabelField("强制哈希不一致（调试）",
                canInject ? BattleDebugDisplayText.Bool(forced) : "会话控制不可用");
            EditorGUI.BeginDisabledGroup(!canInject);
            if (GUILayout.Button("切换：强制哈希不一致"))
            {
                if (BattleSessionFeature.TrySetDebugForceClientHashMismatch(flowCtx, !forced))
                {
                    ctx.OnHashMismatchChanged?.Invoke(flowCtx);
                    ResetReconcile(flowCtx);
                }
            }
            EditorGUI.EndDisabledGroup();
#endif

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("对账目标", flowCtx.PredictionReconcileTarget != null ? "已设置" : "为空");

            if (flowCtx.PredictionStats != null)
            {
                var wid = new WorldId(flowCtx.Plan.World.WorldId);
                if (flowCtx.PredictionStats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    EditorGUILayout.LabelField("帧", $"确认={confirmed.Value} 预测={predicted.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("回放超时次数（总）", flowCtx.PredictionStats.TotalReplayTimeout.ToString());
                EditorGUILayout.LabelField("回放超时最近帧", flowCtx.PredictionStats.LastReplayTimeoutFrame.Value.ToString());
                EditorGUILayout.LabelField("因回放超时自动关闭对账（总）", flowCtx.PredictionStats.TotalReconcileAutoDisabledByReplayTimeout.ToString());
                EditorGUILayout.LabelField("因回放超时自动关闭对账最近帧", flowCtx.PredictionStats.LastReconcileAutoDisabledByReplayTimeoutFrame.Value.ToString());
            }
        }

        internal static void ResetReconcile(BattleContext flowCtx)
        {
            if (flowCtx?.PredictionReconcileControl == null) return;
            if (flowCtx.HasRuntimeWorldId)
                flowCtx.PredictionReconcileControl.ResetReconcile(flowCtx.RuntimeWorldId);
            flowCtx.PredictionReconcileControl.ResetReconcile(new WorldId(flowCtx.Plan.World.WorldId));
        }
    }
}
