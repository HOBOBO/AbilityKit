#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.Core
{
    public interface IEditorContribution
    {
        string Id { get; }
        int Order { get; }
    }

    public sealed class EditorMenuContribution : IEditorContribution
    {
        public EditorMenuContribution(string id, string path, Action open, int order = 0)
            : this(id, path, open, showInHub: false, order: order)
        {
        }

        /// <summary>
        /// 登记菜单能力；只有可安全打开的全局导航才应显式准入 Hub。
        /// </summary>
        public EditorMenuContribution(string id, string path, Action open, bool showInHub, int order = 0)
        {
            Id = RequireValue(id, nameof(id));
            Path = RequireValue(path, nameof(path));
            Open = open ?? throw new ArgumentNullException(nameof(open));
            ShowInHub = showInHub;
            Order = order;
        }

        public string Id { get; }
        public string Path { get; }
        public Action Open { get; }
        public int Order { get; }

        /// <summary>
        /// 是否显示为 Hub 的可打开入口；不影响原 Unity 菜单。
        /// </summary>
        public bool ShowInHub { get; }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
            return value;
        }
    }

    /// <summary>
    /// Describes a composable panel while allowing each domain editor to retain its own
    /// UI Toolkit, IMGUI, GraphView, or custom canvas implementation.
    /// </summary>
    public sealed class EditorPanelContribution : IEditorContribution
    {
        public EditorPanelContribution(
            string id,
            string titleKey,
            Func<VisualElement> createVisualElement = null,
            Action<Rect> drawImGui = null,
            int order = 0)
            : this(id, titleKey, showInHub: false, createVisualElement: createVisualElement, drawImGui: drawImGui, order: order)
        {
        }

        /// <summary>
        /// 登记可组合面板；默认导航只展示显式准入且打开不改资产的面板。
        /// </summary>
        public EditorPanelContribution(
            string id,
            string titleKey,
            bool showInHub,
            Func<VisualElement> createVisualElement = null,
            Action<Rect> drawImGui = null,
            int order = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A panel id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey)) throw new ArgumentException("A title key is required.", nameof(titleKey));
            if (createVisualElement == null && drawImGui == null)
            {
                throw new ArgumentException("A UI Toolkit factory or IMGUI callback is required.");
            }

            Id = id;
            TitleKey = titleKey;
            CreateVisualElement = createVisualElement;
            DrawImGui = drawImGui;
            ShowInHub = showInHub;
            Order = order;
        }

        public string Id { get; }
        public string TitleKey { get; }
        public Func<VisualElement> CreateVisualElement { get; }
        public Action<Rect> DrawImGui { get; }
        public int Order { get; }

        /// <summary>
        /// 是否显示在 Hub；不限制领域工作台自行复用面板。
        /// </summary>
        public bool ShowInHub { get; }
        public bool SupportsUiToolkit => CreateVisualElement != null;
        public bool SupportsImGui => DrawImGui != null;
    }
}
#endif
