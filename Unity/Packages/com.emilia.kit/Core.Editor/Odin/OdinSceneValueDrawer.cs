using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Emilia.Kit.Editor
{
    public abstract class OdinSceneValueDrawer<T> : OdinValueDrawer<T>, IDisposable
    {
        protected override void Initialize()
        {
            base.Initialize();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        protected virtual void OnSceneGUI(SceneView sceneView) { }

        public virtual void Dispose()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
}