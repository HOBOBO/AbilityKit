using System;
using System.Collections.Generic;
using AbilityKit.Diagnostics.DebugDraw;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Diagnostics.Editor.DebugDraw
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
            if (!s_contributors.Contains(contributor)) s_contributors.Add(contributor);
        }

        public static void Unregister(IDebugDrawContributor contributor)
        {
            if (contributor != null) s_contributors.Remove(contributor);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            s_forceRepaintFrames = 5;
            SceneView.RepaintAll();
        }

        private static void OnEditorUpdate()
        {
            var shouldDraw = EditorApplication.isPlaying &&
                             DebugDrawEditorSettings.Enabled &&
                             DebugDrawEditorSettings.EnabledMask.Value != 0;

            if (shouldDraw != s_lastShouldDraw)
            {
                s_lastShouldDraw = shouldDraw;
                s_forceRepaintFrames = 5;
                SceneView.RepaintAll();
                return;
            }

            if (shouldDraw)
            {
                SceneView.RepaintAll();
                return;
            }

            if (s_forceRepaintFrames > 0)
            {
                s_forceRepaintFrames--;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            if (!DebugDrawEditorSettings.Enabled || !EditorApplication.isPlaying) return;

            var enabled = DebugDrawEditorSettings.EnabledMask;
            if (enabled.Value == 0) return;

            var context = new DebugDrawContext(enabled);
            for (var i = 0; i < s_contributors.Count; i++)
            {
                var contributor = s_contributors[i];
                if (contributor == null || (contributor.Mask.Value & enabled.Value) == 0) continue;

                try
                {
                    using (new Handles.DrawingScope(Color.white, Matrix4x4.identity))
                    {
                        contributor.Draw(in context, s_draw);
                    }
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }
    }
}
