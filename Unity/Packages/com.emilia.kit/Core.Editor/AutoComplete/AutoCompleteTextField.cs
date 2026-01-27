using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    /// <summary>
    /// 带自动补全功能的文本输入框
    /// </summary>
    public class AutoCompleteTextField
    {
        private readonly AutoCompleteStyles _styles;
        private readonly TextFieldInputHandler _inputHandler;
        private readonly AutoCompletePopup _popup;

        private string _lastText;
        private string _pendingText;

        /// <summary>
        /// 弹窗是否显示中
        /// </summary>
        public bool IsPopupVisible => _popup.IsVisible;

        public AutoCompleteTextField()
        {
            _styles = new AutoCompleteStyles();
            _inputHandler = new TextFieldInputHandler();
            _popup = new AutoCompletePopup(_styles);
        }

        /// <summary>
        /// 绘制带自动补全的文本输入框
        /// </summary>
        public string Draw(Rect position, string text, Func<string, IEnumerable<string>> getSuggestions,
            GUIContent label = null)
        {
            _styles.Init();

            int controlId = GUIUtility.GetControlID(FocusType.Keyboard);

            // 计算文本框位置
            Rect textFieldRect = position;
            if (label != null)
            {
                textFieldRect = EditorGUI.PrefixLabel(position, controlId, label);
            }

            // 检查是否有待应用的文本
            if (_pendingText != null)
            {
                text = _pendingText;
                _pendingText = null;
                _lastText = text;
                _popup.Hide();
                _inputHandler.SetCursorToEnd(text);
                GUI.changed = true;
            }

            Event e = Event.current;
            string newText = text ?? "";

            // 处理鼠标点击
            if (e.type == EventType.MouseDown && textFieldRect.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = controlId;
                _inputHandler.HandleMouseDown(newText, textFieldRect, e.mousePosition);
                e.Use();
            }

            bool hasFocus = GUIUtility.keyboardControl == controlId;

            // 处理键盘输入
            if (hasFocus && e.type == EventType.KeyDown)
            {
                bool handled = false;

                // 先让弹窗处理导航键
                if (_popup.IsVisible)
                {
                    handled = _popup.HandleKeyDown(e, out string selectedValue);
                    if (selectedValue != null)
                    {
                        newText = selectedValue;
                        _inputHandler.SetCursorToEnd(newText);
                        _lastText = newText;
                    }
                }

                // 弹窗未处理则由输入处理器处理
                if (!handled)
                {
                    newText = _inputHandler.HandleKeyDown(newText, e, out handled);
                }

                if (handled)
                {
                    e.Use();
                }
            }

            // 处理字符输入
            if (hasFocus && e.type == EventType.KeyDown && e.character != 0 && !char.IsControl(e.character))
            {
                newText = _inputHandler.HandleCharacterInput(newText, e.character);
                e.Use();
            }

            // 绘制文本框
            if (e.type == EventType.Repaint)
            {
                DrawTextField(textFieldRect, newText, controlId, hasFocus, e);
            }

            // 更新光标闪烁
            if (hasFocus && _inputHandler.UpdateCursorBlink())
            {
                RequestRepaint();
            }

            // 文本变化时更新弹窗
            if (newText != _lastText)
            {
                _lastText = newText;
                UpdatePopup(newText, textFieldRect, getSuggestions);
                GUI.changed = true;
            }

            return newText;
        }

        /// <summary>
        /// 在 OnGUI 的最后调用此方法来绘制弹窗
        /// </summary>
        public void DrawPopup()
        {
            _popup.Draw();
        }

        /// <summary>
        /// 绘制带自动补全的文本输入框（Layout版本）
        /// </summary>
        public string DrawLayout(string text, Func<string, IEnumerable<string>> getSuggestions,
            GUIContent label = null, params GUILayoutOption[] options)
        {
            Rect rect = EditorGUILayout.GetControlRect(label != null, EditorGUIUtility.singleLineHeight, options);
            return Draw(rect, text, getSuggestions, label);
        }

        /// <summary>
        /// 绘制带自动补全的文本输入框（Layout版本，字符串标签）
        /// </summary>
        public string DrawLayout(string label, string text, Func<string, IEnumerable<string>> getSuggestions,
            params GUILayoutOption[] options)
        {
            return DrawLayout(text, getSuggestions, new GUIContent(label), options);
        }

        /// <summary>
        /// 关闭弹窗
        /// </summary>
        public void ClosePopup()
        {
            _popup.Hide();
        }

        private void DrawTextField(Rect textFieldRect, string text, int controlId, bool hasFocus, Event e)
        {
            // 绘制背景
            _styles.TextFieldStyle.Draw(textFieldRect, GUIContent.none, controlId, false, textFieldRect.Contains(e.mousePosition));

            Rect textRect = new Rect(textFieldRect.x + 2, textFieldRect.y, textFieldRect.width - 4, textFieldRect.height);

            // 绘制选择高亮
            if (hasFocus && _inputHandler.HasSelection)
            {
                float startX = TextFieldInputHandler.GetTextWidth(text.Substring(0, _inputHandler.SelectionStart)) + textRect.x;
                float endX = TextFieldInputHandler.GetTextWidth(text.Substring(0, _inputHandler.SelectionEnd)) + textRect.x;
                Rect selectionRect = new Rect(startX, textRect.y + 2, endX - startX, textRect.height - 4);
                EditorGUI.DrawRect(selectionRect, AutoCompleteStyles.SelectionColor);
            }

            // 绘制文本
            GUI.Label(textRect, text, EditorStyles.label);

            // 绘制光标
            if (hasFocus && _inputHandler.CursorVisible)
            {
                float cursorX = TextFieldInputHandler.GetTextWidth(text.Substring(0, _inputHandler.CursorIndex)) + textRect.x;
                Rect cursorRect = new Rect(cursorX, textRect.y + 2, 1, textRect.height - 4);
                EditorGUI.DrawRect(cursorRect, AutoCompleteStyles.CursorColor);
            }
        }

        private void UpdatePopup(string text, Rect textFieldRect, Func<string, IEnumerable<string>> getSuggestions)
        {
            if (string.IsNullOrEmpty(text))
            {
                _popup.Hide();
                return;
            }

            List<string> suggestions = getSuggestions?.Invoke(text)?.ToList();
            if (suggestions != null && suggestions.Count > 0)
            {
                _popup.Show(suggestions, textFieldRect, selected =>
                {
                    _pendingText = selected;
                    RequestRepaint();
                });
            }
            else
            {
                _popup.Hide();
            }
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
