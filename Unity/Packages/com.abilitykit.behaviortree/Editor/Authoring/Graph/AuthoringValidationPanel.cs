#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 图校验面板：渲染 <see cref="EditorDiagnosticCollection"/>（成功提示或错误列表），
    /// 并把可定位节点的诊断映射回画布上的错误节点着色。只负责展示与通知，不执行分析。
    /// 从 <c>AuthoringGraphWindow</c> 抽出，保持与原内联实现一致的行为。
    /// </summary>
    public sealed class AuthoringValidationPanel : ScrollView
    {
        private readonly IEditorLocalization _localization;
        private readonly Action<IReadOnlyList<string>> _markErrorNodes;
        private readonly Action _clearErrorNodes;

        public AuthoringValidationPanel(
            bool initiallyVisible,
            IEditorLocalization localization,
            Action<IReadOnlyList<string>> markErrorNodes,
            Action clearErrorNodes)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _markErrorNodes = markErrorNodes ?? throw new ArgumentNullException(nameof(markErrorNodes));
            _clearErrorNodes = clearErrorNodes ?? throw new ArgumentNullException(nameof(clearErrorNodes));

            style.display = initiallyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            style.maxHeight = 190f;
            style.paddingTop = 6f;
            style.borderTopWidth = 1f;
            style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
        }

        public void Render(EditorDiagnosticCollection diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            Clear();
            style.display = DisplayStyle.Flex;

            if (!diagnostics.HasErrors)
            {
                Add(new Label(_localization.Get("abilitykit.behaviortree.validation.success"))
                {
                    style = { color = new Color(0.55f, 0.9f, 0.62f), whiteSpace = WhiteSpace.Normal },
                });
                _clearErrorNodes();
                return;
            }

            Add(new Label(_localization.Format("abilitykit.behaviortree.validation.errors", diagnostics.ErrorCount))
            {
                style = { color = new Color(0.95f, 0.5f, 0.45f), whiteSpace = WhiteSpace.Normal },
            });

            foreach (var diagnostic in diagnostics.Items)
            {
                if (!diagnostic.CanLocate)
                {
                    Add(new Label(diagnostic.Message)
                    {
                        style = { whiteSpace = WhiteSpace.Normal },
                    });
                    continue;
                }

                var nodeId = diagnostic.Path.Substring("nodes/".Length);
                var locate = diagnostic.Locate;
                var focusError = new Button(() => locate?.Invoke())
                {
                    text = diagnostic.Message,
                    tooltip = _localization.Format("abilitykit.behaviortree.validation.locate", nodeId),
                };
                focusError.style.unityTextAlign = TextAnchor.MiddleLeft;
                focusError.style.whiteSpace = WhiteSpace.Normal;
                Add(focusError);
            }

            _markErrorNodes(diagnostics.Items
                .Where(item => item.CanLocate)
                .Select(item => item.Path.Substring("nodes/".Length))
                .ToArray());
        }
    }
}
