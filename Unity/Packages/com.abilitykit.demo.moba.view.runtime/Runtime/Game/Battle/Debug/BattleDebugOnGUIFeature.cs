using AbilityKit.Demo.Moba;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private const int PauseControlWindowId = 0xB0BA;

        private readonly BattleDebugPublicationOwner _publication = new BattleDebugPublicationOwner();

        public void OnAttach(in GamePhaseContext ctx)
        {
            _publication.Refresh(in ctx);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _publication.Dispose();
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            // 恢复推进由 BattleSessionFeature 的正式生命周期负责；Debug feature 仅展示控制与诊断。
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            // 断线演示入口：多人战斗期间恒显（与 DebugEnabled 无关），镜像 shooter 的
            // Battle Control (Sync Demo) 窗口。本地模式没有房间流控制器，不画。
            DrawPauseControlWindow(ctx);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _publication.Refresh(in ctx);
            if (!ctx.Entry.DebugEnabled) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink == null || sink.CurrentRootPhase != MobaRootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 170, 110), GUI.skin.window);
            if (GUILayout.Button("Exit Battle", GUILayout.Height(34)))
            {
                sink.RequestReturnLobby();
            }

            if (GUILayout.Button("Rebind Views", GUILayout.Height(34)))
            {
                if (ctx.Features.TryGet(out BattleViewFeature view) && view != null)
                {
                    view.RebindAll();
                }
                if (ctx.Features.TryGet(out ConfirmedBattleViewFeature confirmed) && confirmed != null)
                {
                    confirmed.RebindAll();
                }
            }
            GUILayout.EndArea();
#endif
        }

        private static void DrawPauseControlWindow(in GamePhaseContext ctx)
        {
            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink == null || sink.CurrentRootPhase != MobaRootState.Battle)
            {
                return;
            }

            // 仅多人（GatewayRemote）战斗：本地模式同样会进 Battle 根状态，但没有房间流。
            if (!ctx.Entry.TryGet(out MultiplayerRoomFlowController controller) || controller == null)
            {
                return;
            }

            var isPaused = MobaBattlePauseController.IsPaused;
            var hasRecoveryError = !string.IsNullOrEmpty(MobaBattlePauseController.RecoveryError);
            var width = 248f;
            // IMGUI Window 的标题栏和内边距也占高度。暂停态比运行态多一行帧号，
            // 错误态还会多一行诊断；为当前状态只绘制一个主操作按钮，并保留足够高度，
            // 避免 Resume 按钮落到窗口裁剪区域之外。
            var height = isPaused ? (hasRecoveryError ? 190f : 166f) : 138f;
            var rect = new Rect(Screen.width - width - 12f, 12f, width, height);
            var context = ctx;
            GUILayout.Window(PauseControlWindowId, rect, id => DrawPauseControlWindowContent(id, controller, context), "Battle Control (FrameSync Demo)");
        }

        private static void DrawPauseControlWindowContent(int windowId, MultiplayerRoomFlowController controller, in GamePhaseContext ctx)
        {
            var isPaused = MobaBattlePauseController.IsPaused;
            var isRecovering = MobaBattlePauseController.IsRecovering;
            GUILayout.Label($"State: {(isRecovering ? "Reconnecting…" : isPaused ? "Paused (disconnected)" : "Running")}");
            GUILayout.Label($"Room: {controller.CurrentRoomId}");
            if (isPaused)
            {
                GUILayout.Label($"Paused at confirmed frame: {MobaBattlePauseController.PausedAtConfirmedFrame}");
            }

            var recoveryError = MobaBattlePauseController.RecoveryError;
            if (!string.IsNullOrEmpty(recoveryError))
            {
                GUILayout.Label($"Error: {recoveryError}");
            }

            ctx.Entry.TryGet(out BattleContext battleContext);

            // 暂停 = 断开战斗连接模拟断线：帧停推→服务器战斗继续。
            // 恢复 = 同一会话重连 + CatchUp 补帧历史注入 + 追上后重开输入（帧同步语义）。
            // 只绘制当前状态可执行的主操作，避免两个按钮纵向堆叠导致恢复按钮被窗口裁剪。
            if (isPaused)
            {
                GUI.enabled = !isRecovering && battleContext != null && battleContext.Session != null;
                if (GUILayout.Button(
                        isRecovering ? "Resuming…" : "Resume & Catch Up",
                        GUILayout.Height(34f)))
                {
                    MobaBattlePauseController.Resume(battleContext);
                }
            }
            else
            {
                GUI.enabled = !isRecovering && battleContext != null && battleContext.Session != null;
                if (GUILayout.Button("Pause Client", GUILayout.Height(34f)))
                {
                    MobaBattlePauseController.Pause(battleContext);
                }
            }

            GUI.enabled = true;
            GUI.DragWindow();
        }

    }
}
