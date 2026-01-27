using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public class TransparentEditorWindow : EditorWindow
    {
        protected Texture2D backgroundTexture;

        public virtual void OpenInPopup()
        {
            position = GUIHelper.GetEditorWindowRect();
            ShowPopup();
            Focus();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        protected virtual void OnDisable()
        {
            if (this.backgroundTexture == null) return;
            DestroyImmediate(this.backgroundTexture);
            this.backgroundTexture = null;
        }

        protected virtual void OnGUI()
        {
            if (this.backgroundTexture == null) this.backgroundTexture = EditorKit.CaptureScreen(GUIHelper.GetEditorWindowRect());
            else
            {
                Rect windowRect = new Rect(0, 0, position.width, position.height);
                GUI.DrawTexture(windowRect, this.backgroundTexture, ScaleMode.StretchToFill);
                OnImGUI();
            }
        }

        protected virtual void OnImGUI() { }
    }
}