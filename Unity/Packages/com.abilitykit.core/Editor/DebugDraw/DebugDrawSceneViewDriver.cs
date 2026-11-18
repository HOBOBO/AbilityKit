using System;
using System.Collections.Generic;
using AbilityKit.Core.Debugging;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Core.Editor.Debugging
{
    /// <summary>
    /// 在编辑器初始化时订阅 SceneView、编辑器更新和播放状态事件的旧绘制驱动。
    /// 仅兼容至下一主版本，新代码迁往 Diagnostics.Editor.DebugDraw 对应入口。
    /// </summary>
    /// <remarks>
    /// 通过先退订再订阅当前回调避免同一回调重复挂接，不提供公开停止入口。
    /// 登记不跨域持久化，播放状态通知不清空登记；调用方负责在生命周期结束时注销。
    /// 仅供编辑器主线程使用，不提供并发、快照或通用重入隔离。
    /// </remarks>
    [InitializeOnLoad]
    [Obsolete("Use AbilityKit.Diagnostics.Editor.DebugDraw.DebugDrawSceneViewDriver before the next major version.")]
    public static class DebugDrawSceneViewDriver
    {
        private static readonly List<IDebugDrawContributor> s_contributors = new List<IDebugDrawContributor>(32);
        private static readonly HandlesDebugDraw s_draw = new HandlesDebugDraw();

        private static bool s_lastShouldDraw;
        private static int s_forceRepaintFrames;

        static DebugDrawSceneViewDriver()
        {
            /*
             * 仅重挂当前静态回调；不建立可释放订阅，也不移除其他编辑器扩展的处理器。
             */
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// 拒绝空引用，并按默认相等比较去重后追加内容提供方。
        /// 首次访问类型会执行静态事件订阅；登记本身不立即请求重绘。
        /// </summary>
        /// <param name="contributor">由调用方持有生命周期的内容提供方。</param>
        public static void Register(IDebugDrawContributor contributor)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            if (s_contributors.Contains(contributor)) return;
            s_contributors.Add(contributor);
        }

        /// <summary>
        /// 空引用时无动作，否则按默认相等比较移除首个匹配项，不释放对象。
        /// 绘制遍历实时列表，回调中移除当前项可能跳过后移项，追加项可能进入同轮。
        /// </summary>
        /// <param name="contributor">待注销的内容提供方。</param>
        public static void Unregister(IDebugDrawContributor contributor)
        {
            if (contributor == null) return;
            s_contributors.Remove(contributor);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            /*
             * 任意播放状态通知均请求立即重绘，并预留关闭状态下的后续重绘次数。
             * 此处不清除内容提供方登记或重置设置。
             */
            s_forceRepaintFrames = 5;
            SceneView.RepaintAll();
        }

        private static void OnEditorUpdate()
        {
            var shouldDraw = EditorApplication.isPlaying && DebugDrawEditorSettings.Enabled && DebugDrawEditorSettings.EnabledMask.Value != 0;

            if (shouldDraw != s_lastShouldDraw)
            {
                s_lastShouldDraw = shouldDraw;
                s_forceRepaintFrames = 5;
                SceneView.RepaintAll();
                return;
            }

            if (shouldDraw)
            {
                /*
                 * 启用时每次编辑器更新均重绘，即使尚未登记内容提供方。
                 */
                SceneView.RepaintAll();
                return;
            }

            /*
             * 关闭后继续有限次数重绘，使上一轮覆盖层有机会被清除。
             */
            if (s_forceRepaintFrames > 0)
            {
                s_forceRepaintFrames--;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            if (!DebugDrawEditorSettings.Enabled) return;
            if (!EditorApplication.isPlaying) return;

            var enabled = DebugDrawEditorSettings.EnabledMask;
            if (enabled.Value == 0) return;

            var ctx = new DebugDrawContext(enabled);

            /*
             * 本轮捕获设置，但登记列表和每项分类仍实时读取；回调变更不提供快照语义。
             */
            for (int i = 0; i < s_contributors.Count; i++)
            {
                var c = s_contributors[i];
                if (c == null) continue;
                if ((c.Mask.Value & enabled.Value) == 0) continue;

                try
                {
                    /*
                     * 每个内容提供方在白色、单位矩阵作用域中绘制，退出或抛错时由作用域恢复状态。
                     * 分类属性读取在该异常隔离范围之外，其异常会中断本轮遍历。
                     */
                    using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
                    {
                        c.Draw(in ctx, s_draw);
                    }
                }
                catch
                {
                    /*
                     * 保留旧编辑器回调的异常隔离，不让单个绘制失败阻断后续项。
                     * 此兼容入口不记录异常；Diagnostics 正式驱动另有日志行为。
                     */
                }
            }
        }
    }
}
