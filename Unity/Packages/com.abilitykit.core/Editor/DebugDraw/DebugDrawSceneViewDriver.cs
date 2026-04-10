using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.DebugDraw;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Core.Editor.DebugDraw
{
    [InitializeOnLoad]
    public static class DebugDrawSceneViewDriver
    {
        private static readonly List<IDebugDrawContributor> s_contributors = new List<IDebugDrawContributor>(32);
        private static readonly HandlesDebugDraw s_draw = new HandlesDebugDraw();

        private static bool s_lastShouldDraw;
        private static int s_forceRepaintFrames;

        static DebugDrawSceneViewDriver()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Register(IDebugDrawContributor contributor)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            if (s_contributors.Contains(contributor)) return;
            s_contributors.Add(contributor);
        }

        public static void Unregister(IDebugDrawContributor contributor)
        {
            if (contributor == null) return;
            s_contributors.Remove(contributor);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Ensure the SceneView is repainted when entering/exiting play mode so no stale
            // handles remain on screen.
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
                // While enabled, repaint every editor update to avoid any perceived "stale" frames.
                SceneView.RepaintAll();
                return;
            }

            // After disabling, repaint a few frames to ensure the last drawn overlay is cleared.
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

            for (int i = 0; i < s_contributors.Count; i++)
            {
                var c = s_contributors[i];
                if (c == null) continue;
                if ((c.Mask.Value & enabled.Value) == 0) continue;

                try
                {
                    c.Draw(in ctx, s_draw);
                }
                catch
                {
                }
            }
        }
    }
}
