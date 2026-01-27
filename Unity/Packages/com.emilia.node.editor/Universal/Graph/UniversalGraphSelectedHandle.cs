using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Node.Editor;
using Emilia.Reflection.Editor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用选中处理
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalGraphSelectedHandle : GraphSelectedHandle
    {
        protected EditorGraphView editorGraphView;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            editorGraphView = graphView;
            bool isUseSelection = graphView.window?.GetType() != InspectorWindow_Internals.inspectorWindowType_Internals;
            if (isUseSelection == false) return;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        protected void OnSelectionChanged()
        {
            List<Object> selectedInspectors = new();

            foreach (ISelectable selectable in editorGraphView.selection)
            {
                ISelectedHandle selectableElement = selectable as ISelectedHandle;
                if (selectableElement == null) continue;
                selectedInspectors.AddRange(selectableElement.GetSelectedObjects());
            }

            bool isGraphSelect = Selection.objects.Any(selectedObject => selectedInspectors.Contains(selectedObject));
            if (isGraphSelect == false) editorGraphView.ClearSelection();
        }

        public override void UpdateSelectedInspector(EditorGraphView graphView, List<ISelectedHandle> selection)
        {
            List<Object> selectedInspectors = new();

            foreach (ISelectedHandle selectable in selection) selectedInspectors.AddRange(selectable.GetSelectedObjects());

            UniversalGraphSetting? graphSetting = graphView.GetGraphData<UniversalGraphData>()?.graphSetting;

            bool isForceUseBuiltIn = graphSetting?.forceUseBuiltInInspector ?? false;
            bool isInspectorWindow = graphView.window?.GetType() == InspectorWindow_Internals.inspectorWindowType_Internals;
            bool isUseBuiltIn = isInspectorWindow || isForceUseBuiltIn;

            if (selectedInspectors.Count > 0)
            {
                if (isUseBuiltIn == false)
                {
                    int selectedCount = selectedInspectors.Count;
                    for (int i = 0; i < selectedCount; i++)
                    {
                        Object selectedObject = selectedInspectors[i];
                        if (selectedObject == null) continue;
                        SelectedOwnerUtility.SetSelectedOwner(selectedObject, graphView);
                        SelectedOwnerUtility.Update();
                    }

                    Selection.objects = selectedInspectors.ToArray();
                }
                else
                {
                    InspectorView inspectorView = graphView.graphPanelSystem.GetPanel<InspectorView>();
                    if (inspectorView == null) inspectorView = graphView.graphPanelSystem.OpenFloatPanel<InspectorView>();
                    inspectorView.SetObjects(selectedInspectors);
                }
            }
            else
            {
                if (isUseBuiltIn == false)
                {
                    Selection.objects = null;
                    SelectedOwnerUtility.Update();
                }
                else graphView.graphPanelSystem.ClosePanel<InspectorView>();
            }
        }

        public override void Dispose(EditorGraphView graphView)
        {
            bool isUseSelection = graphView.window?.GetType() != InspectorWindow_Internals.inspectorWindowType_Internals;
            if (isUseSelection) Selection.selectionChanged -= OnSelectionChanged;

            Selection.objects = null;
            SelectedOwnerUtility.Update();
            this.editorGraphView = null;
        }
    }
}