#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    [Serializable]
    public class HotkeyConfig
    {
        public KeyCode keyCode;
        public bool isCtrl;
        public bool isAlt;
        public bool isShift;

        public bool Check(Event evt) => evt.keyCode == keyCode && evt.control == isCtrl && evt.alt == isAlt && evt.shift == isShift;

        public override string ToString() => (isCtrl ? "Ctrl + " : "") + (isAlt ? "Alt + " : "") + (isShift ? "Shift + " : "") + keyCode;

        public static HotkeyConfig Create(string key, char split = '+')
        {
            HotkeyConfig config = new HotkeyConfig();
            key = key.Replace(" ", "");
            string[] parts = key.Split(split);
            for (var i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                KeyCode keyCode = KeyCodeUtility.GetKeyCode(part);
                if (KeyCodeUtility.IsShift(keyCode))
                {
                    config.isShift = true;
                }
                else if (KeyCodeUtility.IsControl(keyCode))
                {
                    config.isCtrl = true;
                }
                else if (KeyCodeUtility.IsAlt(keyCode))
                {
                    config.isAlt = true;
                }
                else if (keyCode != KeyCode.None)
                {
                    config.keyCode = keyCode;
                }
            }
            return config;
        }
    }

    public class HotkeyConfigDrawer : OdinValueDrawer<HotkeyConfig>
    {
        private bool isModificationMode;

        protected override void Initialize()
        {
            base.Initialize();
            isModificationMode = false;
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            GUILayout.BeginHorizontal();

            if (label != null) GUILayout.Label(label);

            HotkeyConfig config = ValueEntry.SmartValue;

            GUI.enabled = false;
            GUILayout.TextField(config.ToString(), GUILayout.Width(200));
            GUI.enabled = true;

            GUI.color = isModificationMode ? Color.red : Color.white;

            string buttonLabel = isModificationMode ? "按下任意键" : "修改快捷键";
            if (GUILayout.Button(buttonLabel, GUILayout.Width(100))) isModificationMode = ! isModificationMode;

            if (this.isModificationMode)
            {
                Event evt = Event.current;
                bool ignoreKey = IgnoreKeyCode(evt.keyCode);

                if (evt.type == EventType.KeyDown && ignoreKey == false)
                {
                    config.keyCode = evt.keyCode;
                    config.isCtrl = evt.control;
                    config.isAlt = evt.alt;
                    config.isShift = evt.shift;

                    isModificationMode = false;
                    evt.Use();
                }
            }

            GUI.color = Color.white;

            GUILayout.EndHorizontal();
        }

        private bool IgnoreKeyCode(KeyCode keyCode) => keyCode == KeyCode.None || KeyCodeUtility.IsShift(keyCode) || KeyCodeUtility.IsControl(keyCode) || KeyCodeUtility.IsAlt(keyCode);
    }
}
#endif