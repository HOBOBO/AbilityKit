using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    /// <summary>
    /// 自动补全组件的样式管理
    /// </summary>
    public class AutoCompleteStyles
    {
        private GUIStyle _textFieldStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _selectedItemStyle;

        public GUIStyle TextFieldStyle => _textFieldStyle;
        public GUIStyle ItemStyle => _itemStyle;
        public GUIStyle SelectedItemStyle => _selectedItemStyle;

        public void Init()
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(EditorStyles.textField);
            }

            if (_itemStyle == null)
            {
                _itemStyle = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(6, 6, 2, 2),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft
                };
                _itemStyle.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.9f, 0.9f, 0.9f)
                    : new Color(0.1f, 0.1f, 0.1f);
            }

            if (_selectedItemStyle == null)
            {
                _selectedItemStyle = new GUIStyle(_itemStyle);
                _selectedItemStyle.normal.background = CreateColorTexture(
                    EditorGUIUtility.isProSkin
                        ? new Color(0.24f, 0.48f, 0.9f)
                        : new Color(0.3f, 0.5f, 0.85f)
                );
                _selectedItemStyle.normal.textColor = Color.white;
            }
        }

        public static Color PopupBackgroundColor =>
            EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(0.95f, 0.95f, 0.95f);

        public static Color PopupBorderColor =>
            EditorGUIUtility.isProSkin
                ? new Color(0.1f, 0.1f, 0.1f)
                : new Color(0.6f, 0.6f, 0.6f);

        public static Color SelectionColor => new Color(0.24f, 0.48f, 0.9f, 0.5f);

        public static Color CursorColor =>
            EditorGUIUtility.isProSkin ? Color.white : Color.black;

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}