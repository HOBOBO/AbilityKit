using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 层级面板
    /// </summary>
    public class LayerView : GraphPanel
    {
        public static readonly GUIStyle BreadCrumbLeft = "GUIEditor.BreadcrumbLeft";
        public static readonly GUIStyle BreadCrumbMid = "GUIEditor.BreadcrumbMid";
        public static readonly GUIStyle BreadCrumbLeftBg = "GUIEditor.BreadcrumbLeftBackground";
        public static readonly GUIStyle BreadCrumbMidBg = "GUIEditor.BreadcrumbMidBackground";

        public LayerView()
        {
            name = nameof(LayerView);
            Add(new IMGUIContainer(OnImGUI));
        }

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            if (parentView != null) parentView.canResizable = false;
        }

        protected void OnImGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            Stack<IHierarchyAsset> hierarchyAssets = new();

            IHierarchyAsset current = graphView.graphAsset;
            while (current != null)
            {
                hierarchyAssets.Push(current);
                current = current.parent;
            }

            int i = 0;
            while (hierarchyAssets.Count > 0)
            {
                IHierarchyAsset hierarchyAsset = hierarchyAssets.Pop();

                GUIStyle style1 = i == 0 ? BreadCrumbLeft : BreadCrumbMid;
                GUIStyle style2 = i == 0 ? BreadCrumbLeftBg : BreadCrumbMidBg;

                string label = hierarchyAsset.ToString();
                GUIContent guiContent = new(label);
                Rect rect = GetLayoutRect(guiContent, style1);
                if (Event.current.type == EventType.Repaint) style2.Draw(rect, GUIContent.none, 0);

                if (GUI.Button(rect, guiContent, style1))
                {
                    EditorGraphAsset currentGraphAsset = hierarchyAsset as EditorGraphAsset;
                    if (currentGraphAsset != null && graphView.graphAsset != currentGraphAsset) graphView.Reload(currentGraphAsset);
                }

                i++;
            }

            GUILayout.EndHorizontal();
        }

        protected Rect GetLayoutRect(GUIContent content, GUIStyle style)
        {
            Texture image = content.image;
            content.image = null;
            Vector2 vector = style.CalcSize(content);
            content.image = image;
            if (image != null) vector.x += vector.y;
            GUILayoutOption[] options = {GUILayout.MaxWidth(vector.x)};
            return GUILayoutUtility.GetRect(content, style, options);
        }
    }
}