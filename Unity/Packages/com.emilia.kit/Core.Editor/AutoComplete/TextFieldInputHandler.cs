using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    /// <summary>
    /// 文本输入框的输入处理器
    /// </summary>
    public class TextFieldInputHandler
    {
        private int _cursorIndex;
        private int _selectIndex;
        private double _cursorBlinkTime;
        private bool _cursorVisible = true;

        private const float CursorBlinkRate = 0.5f;

        public int CursorIndex
        {
            get => _cursorIndex;
            set => _cursorIndex = value;
        }

        public int SelectIndex
        {
            get => _selectIndex;
            set => _selectIndex = value;
        }

        public bool CursorVisible => _cursorVisible;

        public bool HasSelection => _cursorIndex != _selectIndex;

        public int SelectionStart => Mathf.Min(_cursorIndex, _selectIndex);

        public int SelectionEnd => Mathf.Max(_cursorIndex, _selectIndex);

        /// <summary>
        /// 处理鼠标点击，设置光标位置
        /// </summary>
        public void HandleMouseDown(string text, Rect textFieldRect, Vector2 mousePosition)
        {
            _cursorIndex = GetCursorIndexFromPosition(text, textFieldRect, mousePosition);
            _selectIndex = _cursorIndex;
            ResetCursorBlink();
        }

        /// <summary>
        /// 处理键盘输入，返回处理后的文本
        /// </summary>
        /// <param name="text">当前文本</param>
        /// <param name="e">事件</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理后的文本</returns>
        public string HandleKeyDown(string text, Event e, out bool handled)
        {
            handled = false;
            string newText = text;

            switch (e.keyCode)
            {
                case KeyCode.LeftArrow:
                    if (e.shift)
                    {
                        _cursorIndex = Mathf.Max(0, _cursorIndex - 1);
                    }
                    else
                    {
                        _cursorIndex = Mathf.Max(0, _cursorIndex - 1);
                        _selectIndex = _cursorIndex;
                    }
                    handled = true;
                    break;

                case KeyCode.RightArrow:
                    if (e.shift)
                    {
                        _cursorIndex = Mathf.Min(newText.Length, _cursorIndex + 1);
                    }
                    else
                    {
                        _cursorIndex = Mathf.Min(newText.Length, _cursorIndex + 1);
                        _selectIndex = _cursorIndex;
                    }
                    handled = true;
                    break;

                case KeyCode.Home:
                    _cursorIndex = 0;
                    if (!e.shift) _selectIndex = _cursorIndex;
                    handled = true;
                    break;

                case KeyCode.End:
                    _cursorIndex = newText.Length;
                    if (!e.shift) _selectIndex = _cursorIndex;
                    handled = true;
                    break;

                case KeyCode.Backspace:
                    if (_cursorIndex != _selectIndex)
                    {
                        newText = DeleteSelection(newText);
                    }
                    else if (_cursorIndex > 0)
                    {
                        newText = newText.Remove(_cursorIndex - 1, 1);
                        _cursorIndex--;
                        _selectIndex = _cursorIndex;
                    }
                    handled = true;
                    break;

                case KeyCode.Delete:
                    if (_cursorIndex != _selectIndex)
                    {
                        newText = DeleteSelection(newText);
                    }
                    else if (_cursorIndex < newText.Length)
                    {
                        newText = newText.Remove(_cursorIndex, 1);
                    }
                    handled = true;
                    break;

                case KeyCode.A:
                    if (e.control || e.command)
                    {
                        _selectIndex = 0;
                        _cursorIndex = newText.Length;
                        handled = true;
                    }
                    break;

                case KeyCode.C:
                    if (e.control || e.command)
                    {
                        CopySelection(newText);
                        handled = true;
                    }
                    break;

                case KeyCode.X:
                    if (e.control || e.command)
                    {
                        CopySelection(newText);
                        newText = DeleteSelection(newText);
                        handled = true;
                    }
                    break;

                case KeyCode.V:
                    if (e.control || e.command)
                    {
                        string clipboard = EditorGUIUtility.systemCopyBuffer;
                        if (!string.IsNullOrEmpty(clipboard))
                        {
                            newText = DeleteSelection(newText);
                            newText = newText.Insert(_cursorIndex, clipboard);
                            _cursorIndex += clipboard.Length;
                            _selectIndex = _cursorIndex;
                        }
                        handled = true;
                    }
                    break;
            }

            if (handled)
            {
                ResetCursorBlink();
            }

            return newText;
        }

        /// <summary>
        /// 处理字符输入
        /// </summary>
        public string HandleCharacterInput(string text, char character)
        {
            string newText = DeleteSelection(text);
            newText = newText.Insert(_cursorIndex, character.ToString());
            _cursorIndex++;
            _selectIndex = _cursorIndex;
            ResetCursorBlink();
            return newText;
        }

        /// <summary>
        /// 更新光标闪烁状态
        /// </summary>
        /// <returns>是否需要重绘</returns>
        public bool UpdateCursorBlink()
        {
            double time = EditorApplication.timeSinceStartup;
            if (time - _cursorBlinkTime > CursorBlinkRate)
            {
                _cursorVisible = !_cursorVisible;
                _cursorBlinkTime = time;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 重置光标闪烁
        /// </summary>
        public void ResetCursorBlink()
        {
            _cursorVisible = true;
            _cursorBlinkTime = EditorApplication.timeSinceStartup;
        }

        /// <summary>
        /// 设置光标到文本末尾
        /// </summary>
        public void SetCursorToEnd(string text)
        {
            _cursorIndex = text.Length;
            _selectIndex = _cursorIndex;
        }

        /// <summary>
        /// 删除选中的文本
        /// </summary>
        public string DeleteSelection(string text)
        {
            if (_cursorIndex == _selectIndex) return text;

            int start = Mathf.Min(_cursorIndex, _selectIndex);
            int end = Mathf.Max(_cursorIndex, _selectIndex);
            text = text.Remove(start, end - start);
            _cursorIndex = start;
            _selectIndex = start;
            return text;
        }

        private void CopySelection(string text)
        {
            if (_cursorIndex == _selectIndex) return;

            int start = Mathf.Min(_cursorIndex, _selectIndex);
            int end = Mathf.Max(_cursorIndex, _selectIndex);
            EditorGUIUtility.systemCopyBuffer = text.Substring(start, end - start);
        }

        private int GetCursorIndexFromPosition(string text, Rect textFieldRect, Vector2 mousePos)
        {
            float x = mousePos.x - textFieldRect.x - 2;
            for (int i = 0; i <= text.Length; i++)
            {
                float width = GetTextWidth(text.Substring(0, i));
                if (width >= x)
                {
                    return i > 0 && x < width - GetTextWidth(text.Substring(i - 1, 1)) / 2 ? i - 1 : i;
                }
            }
            return text.Length;
        }

        public static float GetTextWidth(string text)
        {
            return EditorStyles.label.CalcSize(new GUIContent(text)).x;
        }
    }
}
