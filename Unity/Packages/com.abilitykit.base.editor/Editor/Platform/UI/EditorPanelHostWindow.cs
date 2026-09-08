#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// A generic host that assembles one or more <see cref="EditorPanelContribution"/>s into a
    /// single tabbed window. This restores — in the Platform's own terms — the composability the
    /// retired PlugableWindow/plugin framework used to provide: a window shell assembles
    /// independent panels, each of which can be authored as UI Toolkit or IMGUI.
    /// </summary>
    public sealed class EditorPanelHostWindow : EditorWindow
    {
        private IReadOnlyList<EditorPanelContribution> _panels = Array.Empty<EditorPanelContribution>();
        private string _selectedId = string.Empty;

        public static void Show(string title, IEnumerable<EditorPanelContribution> panels)
        {
            if (panels == null) throw new ArgumentNullException(nameof(panels));
            var list = panels.Where(panel => panel != null).ToList();

            var window = GetWindow<EditorPanelHostWindow>(true, string.IsNullOrWhiteSpace(title) ? "Panels" : title);
            window._panels = list;
            window._selectedId = string.Empty;
            window.Build();
            window.Show();
        }

        private void CreateGUI()
        {
            Build();
        }

        private void Build()
        {
            rootVisualElement.Clear();
            if (_panels.Count == 0) return;

            if (string.IsNullOrEmpty(_selectedId) || _panels.All(panel => panel.Id != _selectedId))
                _selectedId = _panels[0].Id;

            var localization = AbilityKitEditorPlatform.Localization;

            if (_panels.Count > 1)
            {
                var toolbar = new Toolbar();
                foreach (var panel in _panels)
                {
                    var selected = string.Equals(panel.Id, _selectedId, StringComparison.Ordinal);
                    toolbar.Add(new Button(() => Select(panel.Id))
                    {
                        text = localization.Get(panel.TitleKey),
                        tooltip = panel.Id
                    });
                }
                rootVisualElement.Add(toolbar);
            }

            var current = _panels.First(panel => panel.Id == _selectedId);
            var content = CreatePanelElement(current);
            content.style.flexGrow = 1f;
            rootVisualElement.Add(content);
        }

        private void Select(string id)
        {
            if (string.Equals(_selectedId, id, StringComparison.Ordinal)) return;
            _selectedId = id;
            Build();
        }

        private static VisualElement CreatePanelElement(EditorPanelContribution panel)
        {
            if (panel.SupportsUiToolkit)
            {
                return panel.CreateVisualElement() ?? new Label("Panel returned no element.");
            }

            var container = new IMGUIContainer();
            container.onGUIHandler = () => panel.DrawImGui(container.contentRect);
            return container;
        }
    }
}
#endif
