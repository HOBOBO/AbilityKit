#if UNITY_EDITOR && ABILITYKIT_PIPELINE_THIRDPARTY_GRAPH

using System;
using System.Collections.Generic;
using Emilia.Node.Editor;
using Emilia.Node.Universal.Editor;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityPipelineEditorGraphAsset : EditorUniversalGraphAsset
    {
        [NonSerialized, OdinSerialize, HideInInspector]
        private int _configId;

        [NonSerialized, OdinSerialize, HideInInspector]
        private string _configName;

        [NonSerialized, OdinSerialize, HideInInspector]
        private List<string> _nodeIdByPhaseIndex = new List<string>();

        public int ConfigId => _configId;
        public string ConfigName => _configName;
        public IReadOnlyList<string> NodeIdByPhaseIndex => _nodeIdByPhaseIndex;

        public void RebuildFromConfig(IAbilityPipelineConfig config)
        {
            _configId = config != null ? config.ConfigId : 0;
            _configName = config != null ? config.ConfigName : string.Empty;

            // clear nodes/edges/items
            var nodesCopy = new List<EditorNodeAsset>(nodes);
            for (int i = 0; i < nodesCopy.Count; i++)
            {
                RemoveNode(nodesCopy[i]);
            }

            var edgesCopy = new List<EditorEdgeAsset>(edges);
            for (int i = 0; i < edgesCopy.Count; i++)
            {
                RemoveEdge(edgesCopy[i]);
            }

            _nodeIdByPhaseIndex.Clear();

            if (config == null || config.PhaseConfigs == null)
                return;

            const float xStep = 260f;
            const float y = 120f;

            for (int i = 0; i < config.PhaseConfigs.Count; i++)
            {
                var pc = config.PhaseConfigs[i];
                if (pc == null)
                {
                    _nodeIdByPhaseIndex.Add(null);
                    continue;
                }

                var node = ScriptableObject.CreateInstance<AbilityPipelinePhaseNodeAsset>();
                node.id = Guid.NewGuid().ToString();
                node.displayName = pc.PhaseId != null ? pc.PhaseId.ToString() : $"Phase_{i}";
                node.position = new Rect(50f + i * xStep, y, 220f, 140f);

                node.phaseIndex = i;
                node.phaseIdName = pc.PhaseId != null ? pc.PhaseId.ToString() : string.Empty;
                node.phaseType = pc.PhaseType ?? string.Empty;
                node.duration = pc.Duration;

                AddNode(node);
                _nodeIdByPhaseIndex.Add(node.id);

                if (i > 0)
                {
                    var prevNodeId = _nodeIdByPhaseIndex[i - 1];
                    if (!string.IsNullOrEmpty(prevNodeId))
                    {
                        var edge = ScriptableObject.CreateInstance<EditorEdgeAsset>();
                        edge.id = Guid.NewGuid().ToString();
                        edge.outputNodeId = prevNodeId;
                        edge.inputNodeId = node.id;
                        edge.outputPortId = "out";
                        edge.inputPortId = "in";
                        AddEdge(edge);
                    }
                }
            }
        }

        [Serializable]
        public sealed class AbilityPipelinePhaseNodeAsset : UniversalNodeAsset
        {
            [HideInInspector]
            public int phaseIndex;

            [HideInInspector]
            public string phaseIdName;

            [HideInInspector]
            public string phaseType;

            [HideInInspector]
            public float duration;

            protected override string defaultDisplayName => string.IsNullOrEmpty(phaseIdName) ? "Phase" : phaseIdName;
        }
    }
}

#endif
