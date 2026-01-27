#if UNITY_EDITOR

using System;
using Emilia.Kit;
using Emilia.Node.Editor;
using Emilia.Node.Universal.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [EditorHandle(typeof(AbilityPipelineEditorGraphAsset))]
    public sealed class AbilityPipelineGraphHandle : GraphHandle
    {
        private string _lastFocusedNodeId;

        public override void OnUpdate(EditorGraphView graphView)
        {
            base.OnUpdate(graphView);

            if (!EditorApplication.isPlaying)
                return;

            if (graphView == null || graphView.graphAsset == null)
                return;

            var asset = graphView.graphAsset as AbilityPipelineEditorGraphAsset;
            if (asset == null)
                return;

            var pipeline = AbilityPipelineLiveRegistry.SelectedPipeline;
            if (pipeline == null)
                return;

            var pipelineType = pipeline.GetType();

            int phaseIndex = 0;
            try
            {
                var f = pipelineType.GetField("_currentPhaseIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null)
                {
                    phaseIndex = (int)f.GetValue(pipeline);
                }
            }
            catch
            {
                return;
            }

            if (phaseIndex < 0 || phaseIndex >= asset.NodeIdByPhaseIndex.Count)
                return;

            var nodeId = asset.NodeIdByPhaseIndex[phaseIndex];
            if (string.IsNullOrEmpty(nodeId))
                return;

            if (_lastFocusedNodeId == nodeId)
                return;

            // clear old focus
            if (!string.IsNullOrEmpty(_lastFocusedNodeId) && graphView.graphElementCache.nodeViewById.TryGetValue(_lastFocusedNodeId, out var oldView))
            {
                if (oldView is UniversalEditorNodeView u)
                {
                    u.ClearFocus();
                }
            }

            if (graphView.graphElementCache.nodeViewById.TryGetValue(nodeId, out var view))
            {
                if (view is UniversalEditorNodeView u)
                {
                    u.SetFocus(Color.yellow, timeMs: 300);
                }
            }

            _lastFocusedNodeId = nodeId;
        }

        public override void Dispose(EditorGraphView graphView)
        {
            _lastFocusedNodeId = null;
            base.Dispose(graphView);
        }
    }
}

#endif
