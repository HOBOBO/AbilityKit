using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    /// <summary>
    /// 自动补全弹窗
    /// </summary>
    public class AutoCompletePopup
    {
        private List<string> _suggestions;
        private int _selectedIndex;
        private Vector2 _scrollPosition;
        private Rect _popupRect;
        private bool _isVisible;
        private Action<string> _onSelect;

        private AutoCompleteStyles _styles;

        public bool IsVisible => _isVisible;

        public int SelectedIndex => _selectedIndex;

        public AutoCompletePopup(AutoCompleteStyles styles)
        {
            _styles = styles;
        }

        /// <summary>
        /// 显示弹窗
        /// </summary>
        public void Show(List<string> suggestions, Rect anchorRect, Action<string> onSelect)
        {
            if (suggestions == null || suggestions.Count == 0)
            {
                Hide();
                return;
            }

            _suggestions = suggestions;
            _selectedIndex = 0;
            _scrollPosition = Vector2.zero;
            _onSelect = onSelect;
            _isVisible = true;

            float itemHeight = EditorGUIUtility.singleLineHeight + 2;
            float popupHeight = Mathf.Min(suggestions.Count * itemHeight + 4, 150);
            _popupRect = new Rect(anchorRect.x, anchorRect.yMax + 1, anchorRect.width, popupHeight);
        }

        /// <summary>
        /// 隐藏弹窗
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _suggestions = null;
        }

        /// <summary>
        /// 处理弹窗内的键盘导航
        /// </summary>
        /// <returns>是否已处理事件</returns>
        public bool HandleKeyDown(Event e, out string selectedValue)
        {
            selectedValue = null;

            if (!_isVisible || _suggestions == null || _suggestions.Count == 0)
            {
                return false;
            }

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    _selectedIndex = Mathf.Min(_selectedIndex + 1, _suggestions.Count - 1);
                    return true;

                case KeyCode.UpArrow:
                    _selectedIndex = Mathf.Max(_selectedIndex - 1, 0);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
                    {
                        selectedValue = _suggestions[_selectedIndex];
                    }
                    Hide();
                    return true;

                case KeyCode.Escape:
                    Hide();
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 绘制弹窗
        /// </summary>
        public void Draw()
        {
            if (!_isVisible || _suggestions == null || _suggestions.Count == 0)
            {
                return;
            }

            _styles.Init();

            Event e = Event.current;

            // 点击弹窗外部关闭
            if (e.type == EventType.MouseDown && !_popupRect.Contains(e.mousePosition))
            {
                Hide();
                RequestRepaint();
                return;
            }

            // 绘制背景和边框
            EditorGUI.DrawRect(_popupRect, AutoCompleteStyles.PopupBackgroundColor);
            DrawBorder(_popupRect, AutoCompleteStyles.PopupBorderColor);

            // 绘制列表
            Rect innerRect = new Rect(_popupRect.x + 1, _popupRect.y + 1, _popupRect.width - 2, _popupRect.height - 2);
            GUILayout.BeginArea(innerRect);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < _suggestions.Count; i++)
            {
                var style = i == _selectedIndex ? _styles.SelectedItemStyle : _styles.ItemStyle;
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                // 鼠标悬停高亮
                Rect worldRect = new Rect(rect.x + innerRect.x, rect.y + innerRect.y - _scrollPosition.y, rect.width, rect.height);
                if (worldRect.Contains(e.mousePosition))
                {
                    if (_selectedIndex != i)
                    {
                        _selectedIndex = i;
                        RequestRepaint();
                    }
                    style = _styles.SelectedItemStyle;
                }

                if (GUI.Button(rect, _suggestions[i], style))
                {
                    _onSelect?.Invoke(_suggestions[i]);
                    Hide();
                    break;
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        private static void RequestRepaint()
        {
            if (EditorWindow.focusedWindow != null)
            {
                EditorWindow.focusedWindow.Repaint();
            }
        }
    }
}
