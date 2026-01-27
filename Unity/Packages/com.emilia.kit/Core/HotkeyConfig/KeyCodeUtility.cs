#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class KeyCodeUtility
    {
        private struct KeycodeLeftRightInfo
        {
            public KeyCode leftKeyCode;
            public KeyCode rightKeyCode;

            public KeycodeLeftRightInfo(KeyCode leftKeyCode, KeyCode rightKeyCode)
            {
                this.leftKeyCode = leftKeyCode;
                this.rightKeyCode = rightKeyCode;
            }
        }

        private struct KeycodeStringInfo
        {
            public string keycodeString;
            public KeyCode keyCode;

            public KeycodeStringInfo(string keycodeString, KeyCode keyCode)
            {
                this.keycodeString = keycodeString;
                this.keyCode = keyCode;
            }
        }

        private static List<KeycodeLeftRightInfo> keycodeLeftRightInfos = new List<KeycodeLeftRightInfo> {
            new(KeyCode.LeftAlt, KeyCode.RightAlt),
            new(KeyCode.LeftAlt, KeyCode.AltGr),
            new(KeyCode.LeftControl, KeyCode.RightControl),
            new(KeyCode.LeftCommand, KeyCode.RightCommand),
            new(KeyCode.LeftShift, KeyCode.RightShift),
            new(KeyCode.LeftApple, KeyCode.RightApple),
            new(KeyCode.LeftWindows, KeyCode.RightWindows),
            new(KeyCode.Alpha0, KeyCode.Keypad0),
            new(KeyCode.Alpha1, KeyCode.Keypad1),
            new(KeyCode.Alpha2, KeyCode.Keypad2),
            new(KeyCode.Alpha3, KeyCode.Keypad3),
            new(KeyCode.Alpha4, KeyCode.Keypad4),
            new(KeyCode.Alpha5, KeyCode.Keypad5),
            new(KeyCode.Alpha6, KeyCode.Keypad6),
            new(KeyCode.Alpha7, KeyCode.Keypad7),
            new(KeyCode.Alpha8, KeyCode.Keypad8),
            new(KeyCode.Alpha9, KeyCode.Keypad9),
            new(KeyCode.Period, KeyCode.KeypadPeriod),
            new(KeyCode.Slash, KeyCode.KeypadDivide),
            new(KeyCode.Asterisk, KeyCode.KeypadMultiply),
            new(KeyCode.Minus, KeyCode.KeypadMinus),
            new(KeyCode.Plus, KeyCode.KeypadPlus),
            new(KeyCode.Equals, KeyCode.KeypadEquals),
            new(KeyCode.Return, KeyCode.KeypadEnter),
        };

        private static List<KeycodeStringInfo> keycodeStringInfos = new List<KeycodeStringInfo> {
            // 控制键
            new("CTRL", KeyCode.LeftControl),
            new("CONTROL", KeyCode.LeftControl),

            new("COMMAND", KeyCode.LeftCommand),
            new("CMD", KeyCode.LeftCommand),

            new("ALT", KeyCode.LeftAlt),
            new("ALTERNATE", KeyCode.LeftAlt),

            new("SHIFT", KeyCode.LeftShift),
            new("SHFT", KeyCode.LeftShift),

            // 删除键
            new("BACKSPACE", KeyCode.Backspace),
            new("BACK", KeyCode.Backspace),
            new("BSPACE", KeyCode.Backspace),

            new("DELETE", KeyCode.Delete),
            new("DEL", KeyCode.Delete),

            // 其他功能键
            new("TAB", KeyCode.Tab),
            new("TABULATION", KeyCode.Tab),

            new("CLEAR", KeyCode.Clear),

            new("ENTER", KeyCode.Return),
            new("RETURN", KeyCode.Return),

            new("PAUSE", KeyCode.Pause),

            new(" ", KeyCode.Space),
            new("SPACE", KeyCode.Space),

            // 符号键
            new("PERIOD", KeyCode.Period),
            new(".", KeyCode.Period),

            new("+", KeyCode.Plus),
            new("PLUS", KeyCode.Plus),

            new("-", KeyCode.Minus),
            new("MINUS", KeyCode.Minus),

            new("=", KeyCode.Equals),
            new("EQUAL", KeyCode.Equals),
            new("EQUALS", KeyCode.Equals),

            // 方向键
            new("UP", KeyCode.UpArrow),
            new("UPARROW", KeyCode.UpArrow),

            new("DOWN", KeyCode.DownArrow),
            new("DOWNARROW", KeyCode.DownArrow),

            new("LEFT", KeyCode.LeftArrow),
            new("LEFTARROW", KeyCode.LeftArrow),

            new("RIGHT", KeyCode.RightArrow),
            new("RIGHTARROW", KeyCode.RightArrow),

            // 导航键
            new("INSERT", KeyCode.Insert),
            new("INS", KeyCode.Insert),

            new("HOME", KeyCode.Home),
            new("END", KeyCode.End),

            new("PAGEUP", KeyCode.PageUp),
            new("PGUP", KeyCode.PageUp),

            new("PAGEDOWN", KeyCode.PageDown),
            new("PGDOWN", KeyCode.PageDown),
            new("PGDN", KeyCode.PageDown),

            // 功能键
            new("F1", KeyCode.F1),
            new("F2", KeyCode.F2),
            new("F3", KeyCode.F3),
            new("F4", KeyCode.F4),
            new("F5", KeyCode.F5),
            new("F6", KeyCode.F6),
            new("F7", KeyCode.F7),
            new("F8", KeyCode.F8),
            new("F9", KeyCode.F9),
            new("F10", KeyCode.F10),
            new("F11", KeyCode.F11),
            new("F12", KeyCode.F12),
            new("F13", KeyCode.F13),
            new("F14", KeyCode.F14),
            new("F15", KeyCode.F15),

            // 数字键
            new("0", KeyCode.Alpha0),
            new("1", KeyCode.Alpha1),
            new("2", KeyCode.Alpha2),
            new("3", KeyCode.Alpha3),
            new("4", KeyCode.Alpha4),
            new("5", KeyCode.Alpha5),
            new("6", KeyCode.Alpha6),
            new("7", KeyCode.Alpha7),
            new("8", KeyCode.Alpha8),
            new("9", KeyCode.Alpha9),

            // 特殊符号
            new("!", KeyCode.Exclaim),
            new("EXCLAIM", KeyCode.Exclaim),
            new("EXCLAMATION", KeyCode.Exclaim),
            new("EXCLAMATIONMARK", KeyCode.Exclaim),

            new("\"", KeyCode.DoubleQuote),
            new("DOUBLEQUOTE", KeyCode.DoubleQuote),

            new("#", KeyCode.Hash),
            new("HASH", KeyCode.Hash),

            new("$", KeyCode.Dollar),
            new("DOLLAR", KeyCode.Dollar),

            new("AMPERSAND", KeyCode.Ampersand),
            new("&", KeyCode.Ampersand),

            new("'", KeyCode.Quote),
            new("QUOTE", KeyCode.Quote),

            new("(", KeyCode.LeftParen),
            new("LEFTPAREN", KeyCode.LeftParen),
            new("LEFTPARENTHESIS", KeyCode.LeftParen),

            new(")", KeyCode.RightParen),
            new("RIGHTPAREN", KeyCode.RightParen),
            new("RIGHTPARENTHESIS", KeyCode.RightParen),

            new("*", KeyCode.Asterisk),
            new("ASTERISK", KeyCode.Asterisk),

            new("/", KeyCode.Slash),
            new("SLASH", KeyCode.Slash),

            new(":", KeyCode.Colon),
            new("COLON", KeyCode.Colon),

            new(";", KeyCode.Semicolon),
            new("SEMICOLON", KeyCode.Semicolon),

            new("<", KeyCode.Less),
            new("LESS", KeyCode.Less),
            new("LESSTHAN", KeyCode.Less),

            new(">", KeyCode.Greater),
            new("GREATER", KeyCode.Greater),
            new("GREATERTHAN", KeyCode.Greater),

            new("?", KeyCode.Question),
            new("QUESTION", KeyCode.Question),

            new("@", KeyCode.At),
            new("AT", KeyCode.At),

            new("[", KeyCode.LeftBracket),
            new("LEFTBRACKET", KeyCode.LeftBracket),

            new("\\", KeyCode.Backslash),
            new("BACKSLASH", KeyCode.Backslash),

            new("]", KeyCode.RightBracket),
            new("RIGHTBRACKET", KeyCode.RightBracket),

            new("^", KeyCode.Caret),
            new("CARET", KeyCode.Caret),

            new("_", KeyCode.Underscore),
            new("UNDERSCORE", KeyCode.Underscore),

            new("`", KeyCode.BackQuote),
            new("BACKQUOTE", KeyCode.BackQuote),

            // 字母键
            new("A", KeyCode.A),
            new("B", KeyCode.B),
            new("C", KeyCode.C),
            new("D", KeyCode.D),
            new("E", KeyCode.E),
            new("F", KeyCode.F),
            new("G", KeyCode.G),
            new("H", KeyCode.H),
            new("I", KeyCode.I),
            new("J", KeyCode.J),
            new("K", KeyCode.K),
            new("L", KeyCode.L),
            new("M", KeyCode.M),
            new("N", KeyCode.N),
            new("O", KeyCode.O),
            new("P", KeyCode.P),
            new("Q", KeyCode.Q),
            new("R", KeyCode.R),
            new("S", KeyCode.S),
            new("T", KeyCode.T),
            new("U", KeyCode.U),
            new("V", KeyCode.V),
            new("W", KeyCode.W),
            new("X", KeyCode.X),
            new("Y", KeyCode.Y),
            new("Z", KeyCode.Z),

            // 锁定键
            new("NUMLOCK", KeyCode.Numlock),
            new("CAPSLOCK", KeyCode.CapsLock),
            new("CAPS", KeyCode.CapsLock),
            new("SCROLLLOCK", KeyCode.ScrollLock),

            // 系统键
            new("APPLE", KeyCode.LeftApple),
            new("WINDOWS", KeyCode.LeftWindows),
            new("HELP", KeyCode.Help),
            new("PRINT", KeyCode.Print),
            new("SYSREQ", KeyCode.SysReq),
            new("BREAK", KeyCode.Break),
            new("MENU", KeyCode.Menu),

            // 鼠标键
            new("MOUSE0", KeyCode.Mouse0),
            new("MOUSE1", KeyCode.Mouse1),
            new("MOUSE2", KeyCode.Mouse2),
            new("MOUSE3", KeyCode.Mouse3),
            new("MOUSE4", KeyCode.Mouse4),
            new("MOUSE5", KeyCode.Mouse5),
            new("MOUSE6", KeyCode.Mouse6),

            // 逗号
            new("COMMA", KeyCode.Comma),
            new(",", KeyCode.Comma),
        };

        private static Dictionary<KeyCode, KeyCode> leftRightKeycodeMap = new();
        private static Dictionary<KeyCode, KeyCode> rightLeftKeycodeMap = new();

        private static Dictionary<string, KeyCode> stringKeycodeMap = new();
        private static Dictionary<KeyCode, string> keycodeStringMap = new();

        static KeyCodeUtility()
        {
            for (int i = 0; i < keycodeLeftRightInfos.Count; i++)
            {
                KeycodeLeftRightInfo info = keycodeLeftRightInfos[i];
                leftRightKeycodeMap[info.leftKeyCode] = info.rightKeyCode;
                rightLeftKeycodeMap[info.rightKeyCode] = info.leftKeyCode;
            }

            for (int i = 0; i < keycodeStringInfos.Count; i++)
            {
                KeycodeStringInfo info = keycodeStringInfos[i];
                stringKeycodeMap[info.keycodeString] = info.keyCode;
                keycodeStringMap[info.keyCode] = info.keycodeString;
            }
        }

        public static KeyCode GetLeftKeyCode(KeyCode keyCode) => rightLeftKeycodeMap.GetValueOrDefault(keyCode, KeyCode.None);
        public static KeyCode GetRightKeyCode(KeyCode keyCode) => leftRightKeycodeMap.GetValueOrDefault(keyCode, KeyCode.None);

        public static KeyCode GetKeyCode(string keyString)
        {
            if (string.IsNullOrEmpty(keyString)) return KeyCode.None;

            keyString = keyString.ToUpper();
            return stringKeycodeMap.GetValueOrDefault(keyString, KeyCode.None);
        }
        
        public static string GetKeyString(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None) return string.Empty;
            return keycodeStringMap.GetValueOrDefault(keyCode, keyCode.ToString());
        }
        
        public static bool IsShift(KeyCode keyCode) => keyCode == KeyCode.LeftShift || keyCode == KeyCode.RightShift;
        public static bool IsControl(KeyCode keyCode) => keyCode == KeyCode.LeftControl || keyCode == KeyCode.RightControl;
        public static bool IsAlt(KeyCode keyCode) => keyCode == KeyCode.LeftAlt || keyCode == KeyCode.RightAlt || keyCode == KeyCode.AltGr;
    }
}
#endif