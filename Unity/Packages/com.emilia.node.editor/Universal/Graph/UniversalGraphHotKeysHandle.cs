using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用快捷键处理
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalGraphHotKeysHandle : GraphHotKeysHandle
    {
        public override void OnGraphKeyDown(EditorGraphView graphView, KeyDownEvent evt)
        {
            base.OnGraphKeyDown(graphView, evt);
            if (evt.keyCode == KeyCode.S && evt.actionKey)
            {
                graphView.graphOperate.Save();
                evt.StopPropagation();
            }

            if (evt.keyCode == KeyCode.E && evt.actionKey)
            {
                SwitchNodeExpand(graphView);
                evt.StopPropagation();
            }

            OnKeyDownShortcut_Hook(graphView, evt);
        }

        protected void SwitchNodeExpand(EditorGraphView graphView)
        {
            List<IEditorNodeView> editorNodeViews = graphView.graphSelected.selected.OfType<IEditorNodeView>().ToList();
            if (editorNodeViews.Count == 0) return;

            bool allCollapsed = editorNodeViews.All(node => node.expanded == false);
            bool targetState = allCollapsed;

            foreach (IEditorNodeView node in editorNodeViews) node.expanded = targetState;
        }

        protected void OnKeyDownShortcut_Hook(EditorGraphView graphView, KeyDownEvent evt)
        {
            if (! graphView.isReframable || graphView.panel.GetCapturingElement(PointerId.mousePointerId) != null) return;

            EventPropagation eventPropagation = EventPropagation.Continue;
            switch (evt.character)
            {
                case ' ':
                    eventPropagation = graphView.OnInsertNodeKeyDown_Internals(evt);
                    break;
                case '[':
                    eventPropagation = graphView.FramePrev();
                    break;
                case ']':
                    eventPropagation = graphView.FrameNext();
                    break;
                case 'a':
                    eventPropagation = graphView.FrameAll();
                    break;
                case 'o':
                    eventPropagation = graphView.FrameOrigin();
                    break;
            }
            if (eventPropagation != EventPropagation.Stop) return;
            evt.StopPropagation();
            if (evt.imguiEvent != null) evt.imguiEvent.Use();
        }
    }
}