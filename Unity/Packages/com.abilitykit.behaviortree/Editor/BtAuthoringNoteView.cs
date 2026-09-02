#nullable enable

using AbilityKit.BehaviorTree.Authoring;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>不参与运行时导出的画布注释；文本、位置和尺寸均写回 authoring 文档。</summary>
    internal sealed class BtAuthoringNoteView : Node
    {
        private readonly IBtAuthoringGraphHost _host;
        private string? _beforeEditSnapshot;

        public BtAuthoringNoteData Data { get; }

        public BtAuthoringNoteView(BtAuthoringNoteData data, IBtAuthoringGraphHost host)
        {
            Data = data;
            _host = host;
            title = "注释";
            capabilities &= ~Capabilities.Copiable;
            SetPosition(new Rect(
                data.X,
                data.Y,
                Mathf.Max(data.Width, 200f),
                Mathf.Max(data.Height, 120f)));
            style.minWidth = 200f;
            style.minHeight = 120f;

            inputContainer.style.display = DisplayStyle.None;
            outputContainer.style.display = DisplayStyle.None;
            titleContainer.style.backgroundColor = new Color(0.58f, 0.46f, 0.12f);
            titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;
            extensionContainer.style.backgroundColor = new Color(0.22f, 0.20f, 0.12f);
            extensionContainer.style.paddingLeft = 8f;
            extensionContainer.style.paddingRight = 8f;
            extensionContainer.style.paddingTop = 6f;
            extensionContainer.style.paddingBottom = 8f;

            var textField = new TextField
            {
                value = string.IsNullOrWhiteSpace(data.Text) ? "在此输入说明..." : data.Text,
                multiline = true,
                isDelayed = true,
                tooltip = "仅保存在编辑文档中，不会导出到运行时",
            };
            textField.style.flexGrow = 1f;
            textField.style.minHeight = 70f;
            textField.RegisterCallback<FocusInEvent>(_ =>
                _beforeEditSnapshot = BtAuthoringJson.Save(_host.Document));
            textField.RegisterValueChangedCallback(evt =>
            {
                var next = evt.newValue ?? "";
                if (string.Equals(Data.Text, next, System.StringComparison.Ordinal)) return;
                _host.RecordChange(_beforeEditSnapshot ?? BtAuthoringJson.Save(_host.Document));
                Data.Text = next;
                _beforeEditSnapshot = null;
            });
            extensionContainer.Add(textField);
            RefreshExpandedState();
        }
    }
}
