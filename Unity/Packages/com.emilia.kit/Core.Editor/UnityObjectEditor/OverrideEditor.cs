using Emilia.Reflection.Editor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public abstract class OverrideEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _baseEditor;

        protected UnityEditor.Editor baseEditor
        {
            get => this._baseEditor ? this._baseEditor : this._baseEditor = GetBaseEditor();
            set => this._baseEditor = value;
        }

        protected abstract UnityEditor.Editor GetBaseEditor();

        public override void OnInspectorGUI()
        {
            baseEditor.OnInspectorGUI();
        }

        public override bool HasPreviewGUI() => baseEditor.HasPreviewGUI();

        public override GUIContent GetPreviewTitle() => baseEditor.GetPreviewTitle();

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height) => baseEditor.RenderStaticPreview(assetPath, subAssets, width, height);

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            baseEditor.OnPreviewGUI(r, background);
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            baseEditor.OnInteractivePreviewGUI(r, background);
        }

        public override void OnPreviewSettings()
        {
            baseEditor.OnPreviewSettings();
        }

        public override string GetInfoString() => baseEditor.GetInfoString();

        public override void ReloadPreviewInstances()
        {
            baseEditor.ReloadPreviewInstances();
        }

        protected override void OnHeaderGUI()
        {
            baseEditor.OnHeaderGUI_Internals();
        }

        public override bool RequiresConstantRepaint() => baseEditor.RequiresConstantRepaint();

        public override bool UseDefaultMargins() => baseEditor.UseDefaultMargins();
        
        protected virtual void OnDestroy()
        {
            if (_baseEditor != null)
            {
                try
                {
                    DestroyImmediate(_baseEditor);
                }
                catch { }

                _baseEditor = null;
            }
        }
    }
}