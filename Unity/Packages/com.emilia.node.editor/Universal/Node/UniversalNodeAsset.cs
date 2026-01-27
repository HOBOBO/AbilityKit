using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用节点资产实现
    /// </summary>
    [HideMonoScript, OnValueChanged(nameof(OnValueChanged), true)]
    public class UniversalNodeAsset : EditorNodeAsset, IObjectDescription
    {
        [SerializeField, HideInInspector]
        private string _displayName;

        /// <summary>
        /// 节点名称
        /// </summary>
        public virtual string displayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public override string title
        {
            get
            {
                if (string.IsNullOrEmpty(_displayName)) return defaultDisplayName;
                return _displayName;
            }
        }

        protected virtual string defaultDisplayName => "节点";

        protected virtual void OnValueChanged()
        {
            EditorGraphView graphView = EditorGraphView.GetGraphView(graphAsset);
            if (graphView == null) return;
            UniversalEditorNodeView nodeView = graphView.graphElementCache.nodeViewById.GetValueOrDefault(id) as UniversalEditorNodeView;
            if (nodeView != null) nodeView.OnValueChanged();
        }

        public virtual string description
        {
            get
            {
                if (userData == null) return title;

                EditorGraphView graphView = EditorGraphView.GetGraphView(graphAsset);
                if (graphView == null) return title;

                string userDataDescription = ObjectDescriptionUtility.GetDescription(userData, graphView);
                return title + $"({userDataDescription})";
            }
        }

        public override void OnCustomGUI(Rect rect)
        {
            base.OnCustomGUI(rect);

            bool tipsDisplay = string.IsNullOrEmpty(tips) == false;

            if (tipsDisplay == false) return;

            const int Width = 20;
            const int Height = 20;

            Rect button = new(rect.x + rect.width - Width * 2, rect.y + rect.height / 2f - Height / 2f, Width, Height);

            SdfIcons.DrawIcon(button, SdfIconType.InfoCircleFill, Color.white);

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && button.Contains(evt.mousePosition))
            {
                const float MaxWidth = 350;

                OdinCustomGUI customGUI = OdinCustomGUI.CreateTextGUI(tips, MaxWidth);
                float width = GUI.skin.label.CalcSize(new GUIContent(tips)).x;
                if (width > MaxWidth) width = MaxWidth;
                OdinEditorWindow.InspectObjectInDropDown(customGUI, width + 10);
            }
        }
    }
}