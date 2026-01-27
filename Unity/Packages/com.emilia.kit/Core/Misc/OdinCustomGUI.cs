#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Kit
{
    [Serializable]
    public class OdinCustomGUI
    {
        private Action _onGUI;

        public OdinCustomGUI(Action onGUI)
        {
            _onGUI = onGUI;
        }

        [OnInspectorGUI]
        public void OnGUI()
        {
            this._onGUI?.Invoke();
        }

        private static GUIStyle textGUIStyle;

        public static OdinCustomGUI CreateTextGUI(string text, float maxWidth = 300)
        {
            OdinCustomGUI odinCustomGUI = new OdinCustomGUI(() => {

                if (textGUIStyle == null)
                {
                    textGUIStyle = new GUIStyle(GUI.skin.label);
                    textGUIStyle.wordWrap = true;
                }

                GUILayout.Label(string.IsNullOrEmpty(text) ? string.Empty : text, textGUIStyle, GUILayout.MaxWidth(maxWidth));
            });

            return odinCustomGUI;
        }
    }
}
#endif