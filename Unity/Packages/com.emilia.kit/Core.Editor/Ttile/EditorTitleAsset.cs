#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit
{
    [CustomEditor(typeof(TitleAsset), true), CanEditMultipleObjects]
    public class EditorTitleAsset : OdinEditor
    {
        private GUIStyle headerStyle;

        protected override void OnHeaderGUI()
        {
            if (this.headerStyle == null) InitStyle();
            TitleAsset titleAsset = target as TitleAsset;

            GUILayout.BeginVertical();

            string title = string.Empty;

            try
            {
                title = titleAsset.title;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToUnityLogString());
            }

            GUILayout.Box(new GUIContent(title), headerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(60));
            GUILayout.Space(5);

            GUILayout.EndVertical();

            Rect rect = GUILayoutUtility.GetLastRect();
            titleAsset.OnCustomGUI(rect);
        }

        protected void InitStyle()
        {
            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.fontSize = 36;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = Color.white;
            headerStyle.hover.textColor = Color.white;
            headerStyle.active.textColor = Color.white;
        }
    }
}
#endif