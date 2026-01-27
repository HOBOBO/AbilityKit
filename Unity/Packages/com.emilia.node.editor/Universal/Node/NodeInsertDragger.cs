using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit.Editor;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点插入
    /// </summary>
    public class NodeInsertDragger : MouseManipulator
    {
        protected bool isActive;

        protected IEditorEdgeView ghostEdgeInput;
        protected IEditorEdgeView ghostEdgeOutput;

        protected IEditorEdgeView targetEdgeView;
        protected IEditorPortView inputPortView;
        protected IEditorPortView outputPortView;

        protected override void RegisterCallbacksOnTarget()
        {
            IEditorNodeView nodeView = target as IEditorNodeView;
            if (nodeView == null) return;

            nodeView.graphView.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            nodeView.graphView.RegisterCallback<KeyUpEvent>(OnKeyUpEvent);

            nodeView.element.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
            nodeView.graphView.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
            nodeView.graphView.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            IEditorNodeView nodeView = target as IEditorNodeView;
            if (nodeView == null) return;

            nodeView.graphView.UnregisterCallback<KeyDownEvent>(OnKeyDownEvent);
            nodeView.graphView.UnregisterCallback<KeyUpEvent>(OnKeyUpEvent);

            nodeView.element.UnregisterCallback<MouseDownEvent>(OnMouseDownEvent);
            nodeView.graphView.UnregisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
            nodeView.graphView.UnregisterCallback<MouseUpEvent>(OnMouseUpEvent);
        }

        protected void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.shiftKey)
            {
                RemoveGhostEdge();

                targetEdgeView = null;
                inputPortView = null;
                outputPortView = null;
            }
        }

        protected void OnKeyUpEvent(KeyUpEvent evt)
        {
            if (evt.shiftKey == false) HandleEdgeConnection();
        }

        protected void OnMouseDownEvent(MouseDownEvent evt)
        {
            if (evt.shiftKey)
            {
                isActive = false;
                return;
            }

            IEditorNodeView nodeView = target as IEditorNodeView;

            this.isActive = true;

            int portAmount = nodeView.portViews.Count;
            for (int i = 0; i < portAmount; i++)
            {
                IEditorPortView portView = nodeView.portViews[i];

                List<IEditorEdgeView> edges = portView.GetEdges();
                if (edges.Count > 0) isActive = false;
            }
        }

        protected void OnMouseMoveEvent(MouseMoveEvent evt)
        {
            if (evt.shiftKey)
            {
                RemoveGhostEdge();

                targetEdgeView = null;
                inputPortView = null;
                outputPortView = null;
                return;
            }

            HandleEdgeConnection();
        }

        protected void HandleEdgeConnection()
        {
            if (this.isActive == false) return;

            IEditorNodeView nodeView = target as IEditorNodeView;

            Rect nodeRect = nodeView.asset.position;

            List<IEditorEdgeView> edgeViews = nodeView.graphView.graphElements.OfType<IEditorEdgeView>().Where((edge) => {
                if (edge.edgeElement.isGhostEdge) return false;
                if (edge.inputPortView.master == nodeView || edge.outputPortView.master == nodeView) return false;

                Rect edgeRect = edge.edgeElement.edgeControl.layout;
                return edgeRect.Overlaps(nodeRect);
            }).ToList();

            if (edgeViews.Count != 0)
            {
                IEditorEdgeView edgeView = edgeViews.FirstOrDefault();

                if (nodeView.GetCanConnectPort(edgeView, out List<IEditorPortView> canConnectInput, out List<IEditorPortView> canConnectOutput))
                {
                    if (this.ghostEdgeInput == null)
                    {
                        this.ghostEdgeInput = ReflectUtility.CreateInstance(edgeView.GetType()) as IEditorEdgeView;
                        this.ghostEdgeInput.edgeElement.isGhostEdge = true;
                        this.ghostEdgeInput.edgeElement.pickingMode = PickingMode.Ignore;
                        nodeView.graphView.AddElement(this.ghostEdgeInput.edgeElement);
                    }

                    if (this.ghostEdgeOutput == null)
                    {
                        this.ghostEdgeOutput = ReflectUtility.CreateInstance(edgeView.GetType()) as IEditorEdgeView;
                        this.ghostEdgeOutput.edgeElement.isGhostEdge = true;
                        this.ghostEdgeOutput.edgeElement.pickingMode = PickingMode.Ignore;
                        nodeView.graphView.AddElement(this.ghostEdgeOutput.edgeElement);
                    }

                    this.targetEdgeView = edgeView;
                    this.inputPortView = canConnectInput.FirstOrDefault();
                    this.outputPortView = canConnectOutput.FirstOrDefault();

                    this.ghostEdgeInput.inputPortView = this.inputPortView;
                    this.ghostEdgeInput.outputPortView = this.targetEdgeView.outputPortView;

                    this.ghostEdgeOutput.inputPortView = this.targetEdgeView.inputPortView;
                    this.ghostEdgeOutput.outputPortView = this.outputPortView;

                    this.inputPortView.portElement.portCapLit = true;
                    this.outputPortView.portElement.portCapLit = true;

                    this.targetEdgeView.outputPortView.portElement.portCapLit = true;
                    this.targetEdgeView.inputPortView.portElement.portCapLit = true;

                    return;
                }
            }

            RemoveGhostEdge();

            this.targetEdgeView = null;
            this.inputPortView = null;
            this.outputPortView = null;
        }

        protected void OnMouseUpEvent(MouseUpEvent evt)
        {
            if (evt.shiftKey)
            {
                RemoveGhostEdge();

                targetEdgeView = null;
                inputPortView = null;
                outputPortView = null;
                isActive = false;
                return;
            }

            if (isActive == false) return;

            if (this.targetEdgeView != null)
            {
                Undo.IncrementCurrentGroup();

                IEdgeCopyPastePack copyPastePack = targetEdgeView.GetPack() as IEdgeCopyPastePack;

                IEditorNodeView nodeView = target as IEditorNodeView;

                EditorEdgeAsset inputPasteAsset = Object.Instantiate(copyPastePack.copyAsset);
                EditorEdgeAsset outputPasteAsset = Object.Instantiate(copyPastePack.copyAsset);

                inputPasteAsset.name = copyPastePack.copyAsset.name;
                inputPasteAsset.id = Guid.NewGuid().ToString();

                outputPasteAsset.name = copyPastePack.copyAsset.name;
                outputPasteAsset.id = Guid.NewGuid().ToString();

                inputPasteAsset.PasteChild();
                outputPasteAsset.PasteChild();

                inputPasteAsset.inputNodeId = inputPortView.master.asset.id;
                inputPasteAsset.inputPortId = inputPortView.info.id;

                outputPasteAsset.outputNodeId = outputPortView.master.asset.id;
                outputPasteAsset.outputPortId = outputPortView.info.id;

                nodeView.graphView.RegisterCompleteObjectUndo("Graph NodeInsert");
                nodeView.graphView.AddEdge(inputPasteAsset);
                nodeView.graphView.AddEdge(outputPasteAsset);

                Undo.RegisterCreatedObjectUndo(inputPasteAsset, "Graph NodeInsert");
                Undo.RegisterCreatedObjectUndo(outputPasteAsset, "Graph NodeInsert");
                targetEdgeView.Delete();

                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                Undo.IncrementCurrentGroup();
            }

            RemoveGhostEdge();

            targetEdgeView = null;
            inputPortView = null;
            outputPortView = null;

            isActive = false;
        }

        protected void RemoveGhostEdge()
        {
            if (this.ghostEdgeInput != null)
            {
                if (ghostEdgeInput.inputPortView != null) ghostEdgeInput.inputPortView.portElement.portCapLit = false;
                if (ghostEdgeInput.outputPortView != null) ghostEdgeInput.outputPortView.portElement.portCapLit = false;

                this.ghostEdgeInput.edgeElement.RemoveFromHierarchy();
                ghostEdgeInput = null;
            }

            if (this.ghostEdgeOutput != null)
            {
                if (ghostEdgeOutput.inputPortView != null) ghostEdgeOutput.inputPortView.portElement.portCapLit = false;
                if (ghostEdgeOutput.outputPortView != null) ghostEdgeOutput.outputPortView.portElement.portCapLit = false;

                ghostEdgeOutput.edgeElement.RemoveFromHierarchy();
                ghostEdgeOutput = null;
            }
        }
    }
}