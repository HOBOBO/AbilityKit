using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit.Editor;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 可附加面板
    /// </summary>
    public class AppendPanel : GraphPanel
    {
        protected struct AppendPanelInfo : IEquatable<AppendPanelInfo>
        {
            public IGraphPanel graphPanel;
            public string displayName;

            public bool Equals(AppendPanelInfo other) => Equals(this.graphPanel, other.graphPanel) && this.displayName == other.displayName;
            public override bool Equals(object obj) => obj is AppendPanelInfo other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(this.graphPanel, this.displayName);
        }

        /// <summary>
        /// 左边距
        /// </summary>
        public float leftMargin = 5;
        
        /// <summary>
        /// 右边距
        /// </summary>
        public float rightMargin = 5f;

        protected IGraphPanel selectedPanel;
        protected List<AppendPanelInfo> graphPanels = new();

        protected IMGUIContainer toggleContainer;

        public AppendPanel()
        {
            name = nameof(AppendPanel);

            toggleContainer = new IMGUIContainer(OnToggleGUI);
            toggleContainer.name = $"{nameof(AppendPanel)}-Toolbar";

            Add(this.toggleContainer);

            RegisterCallback<GeometryChangedEvent>((_) => { this.toggleContainer.style.width = layout.width; });
        }

        /// <summary>
        /// 设置边距
        /// </summary>
        public void SetMargins(float size)
        {
            this.leftMargin = size;
            this.rightMargin = size;
        }

        /// <summary>
        /// 添加面板
        /// </summary>
        public void AddGraphPanel<T>(string displayName) where T : IGraphPanel
        {
            IGraphPanel graphPanel = ReflectUtility.CreateInstance<T>() as IGraphPanel;
            if (graphPanel == null) return;

            AppendPanelInfo panelInfo = new() {
                graphPanel = graphPanel,
                displayName = displayName
            };

            graphPanels.Add(panelInfo);

            if (graphPanels.Count > 0) SwitchPanel(graphPanels.FirstOrDefault().graphPanel);

        }

        /// <summary>
        /// 移除面板
        /// </summary>
        public void RemoveGraphPanel<T>() where T : IGraphPanel
        {
            IGraphPanel graphPanel = ReflectUtility.CreateInstance<T>();
            if (graphPanel == null) return;

            AppendPanelInfo panelInfo = graphPanels.FirstOrDefault(p => p.graphPanel == graphPanel);
            if (panelInfo.graphPanel == null) return;

            graphPanels.Remove(panelInfo);

            if (selectedPanel == panelInfo.graphPanel)
            {
                selectedPanel.Dispose();
                selectedPanel.rootView.RemoveFromHierarchy();
                selectedPanel = null;
            }

            if (graphPanels.Count > 0) SwitchPanel(graphPanels.FirstOrDefault().graphPanel);

        }

        protected void SwitchPanel(IGraphPanel panel)
        {
            if (this.selectedPanel != null)
            {
                selectedPanel.Dispose();
                selectedPanel.rootView.RemoveFromHierarchy();
            }

            panel.Initialize(graphView);
            Add(panel.rootView);

            selectedPanel = panel;
        }

        protected void OnToggleGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Space(this.leftMargin);

            int appendPanelCount = graphPanels.Count;
            for (int i = 0; i < appendPanelCount; i++)
            {
                AppendPanelInfo panelInfo = graphPanels[i];
                bool isSelected = selectedPanel == panelInfo.graphPanel;

                if (GUILayout.Toggle(isSelected, panelInfo.displayName, EditorStyles.toolbarButton))
                {
                    if (isSelected) continue;
                    SwitchPanel(panelInfo.graphPanel);
                }
            }

            GUILayout.Space(this.rightMargin);

            GUILayout.EndHorizontal();
        }
    }
}