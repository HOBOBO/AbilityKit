using UnityEditor;
using UnityEngine;


namespace AbilityKit.HFSM.Editor
{

    /// <summary>
    /// 提供编辑器输入对话框辅助入口。
    /// </summary>
    public static class EditorInputDialog
    {
        /// <summary>
        /// 显示带文本输入框的模态对话框。
        /// </summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="message">对话框消息。</param>
        /// <param name="defaultValue">默认输入值。</param>
        /// <param name="onConfirm">确认时接收输入值的回调。</param>
        public static void Show(string title, string message, string defaultValue, System.Action<string> onConfirm)
        {
            InputDialogueWindow.Show(title, message, defaultValue, onConfirm);
        }

        /// <summary>
        /// 显示确认对话框。
        /// </summary>
        public static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, message, "是", "否");
        }

        /// <summary>
        /// 显示消息对话框。
        /// </summary>
        public static void ShowMessage(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "确定");
        }
    }
}
