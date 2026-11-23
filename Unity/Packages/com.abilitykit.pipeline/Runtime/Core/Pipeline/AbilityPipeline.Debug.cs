using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    public abstract partial class AbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <inheritdoc />
        public IReadOnlyList<PipelinePhaseDebugNode> CaptureDebugStructure()
        {
            return CaptureDebugGraph().Roots;
        }

        /// <inheritdoc />
        public PipelineDebugGraphSnapshot CaptureDebugGraph()
        {
            var roots = new PipelinePhaseDebugNode[_phases.Count];
            var edges = new List<PipelinePhaseDebugEdge>(_phases.Count * 2);
            for (int i = 0; i < _phases.Count; i++)
            {
                roots[i] = CapturePhaseNode(_phases[i], i.ToString(), edges);
                if (i > 0)
                {
                    edges.Add(new PipelinePhaseDebugEdge(
                        roots[i - 1].NodeKey,
                        roots[i].NodeKey,
                        EPipelineDebugEdgeKind.Flow));
                }
            }
            return new PipelineDebugGraphSnapshot(roots, edges, ComputeStructureId(roots, edges));
        }

        private static PipelinePhaseDebugNode CapturePhaseNode(
            IAbilityPipelinePhase<TCtx> phase,
            string nodeKey,
            ICollection<PipelinePhaseDebugEdge> edges)
        {
            var subPhases = phase.SubPhases;
            int childCount = subPhases?.Count ?? 0;
            var children = childCount == 0
                ? Array.Empty<PipelinePhaseDebugNode>()
                : new PipelinePhaseDebugNode[childCount];
            EPipelineDebugNodeKind kind = GetDebugNodeKind(phase);

            for (int i = 0; i < childCount; i++)
            {
                string childKey = nodeKey + "/" + i;
                children[i] = CapturePhaseNode(subPhases![i], childKey, edges);
                edges.Add(new PipelinePhaseDebugEdge(
                    nodeKey,
                    childKey,
                    GetChildEdgeKind(kind),
                    GetChildEdgeLabel(phase, kind, i),
                    i));
            }

            return new PipelinePhaseDebugNode(
                nodeKey,
                phase.PhaseId,
                phase.GetType().Name,
                kind,
                GetDebugSummary(phase, kind),
                children);
        }

        private static EPipelineDebugNodeKind GetDebugNodeKind(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase is AbilityConditionalPhase<TCtx>) return EPipelineDebugNodeKind.Conditional;
            if (phase is AbilityRacePhase<TCtx>) return EPipelineDebugNodeKind.Race;
            if (phase is AbilityParallelPhase<TCtx>) return EPipelineDebugNodeKind.Parallel;
            if (phase is AbilitySequencePhase<TCtx>) return EPipelineDebugNodeKind.Sequence;
            if (phase is AbilityGatePhase<TCtx>) return EPipelineDebugNodeKind.Gate;
            return phase.IsComposite ? EPipelineDebugNodeKind.Composite : EPipelineDebugNodeKind.Phase;
        }

        private static EPipelineDebugEdgeKind GetChildEdgeKind(EPipelineDebugNodeKind kind)
        {
            return kind switch
            {
                EPipelineDebugNodeKind.Sequence => EPipelineDebugEdgeKind.Sequence,
                EPipelineDebugNodeKind.Parallel => EPipelineDebugEdgeKind.Parallel,
                EPipelineDebugNodeKind.Race => EPipelineDebugEdgeKind.Race,
                EPipelineDebugNodeKind.Conditional => EPipelineDebugEdgeKind.Condition,
                _ => EPipelineDebugEdgeKind.Child
            };
        }

        private static string GetChildEdgeLabel(
            IAbilityPipelinePhase<TCtx> phase,
            EPipelineDebugNodeKind kind,
            int childIndex)
        {
            if (kind == EPipelineDebugNodeKind.Sequence) return (childIndex + 1).ToString();
            if (kind == EPipelineDebugNodeKind.Parallel) return "Parallel";
            if (kind == EPipelineDebugNodeKind.Race) return "Race";
            if (phase is AbilityConditionalPhase<TCtx> conditional && childIndex < conditional.Branches.Count)
            {
                var condition = conditional.Branches[childIndex].Condition;
                string name = GetDebugConditionName(condition);
                return name == "Else" ? name : name + " · " + condition.CheckStrategy;
            }
            return string.Empty;
        }

        private static string GetDebugConditionName(IAbilityConditionNode condition)
        {
            Type type = condition.GetType();
            if (type.DeclaringType == typeof(PipelineGraph) && type.Name == "AlwaysConditionNode") return "Else";

            try
            {
                string? displayName = condition.ToString();
                if (!string.IsNullOrWhiteSpace(displayName)
                    && displayName != type.FullName
                    && displayName != type.Name)
                {
                    return displayName;
                }
            }
            catch
            {
            }
            return type.Name;
        }

        private static string GetDebugSummary(IAbilityPipelinePhase<TCtx> phase, EPipelineDebugNodeKind kind)
        {
            return kind switch
            {
                EPipelineDebugNodeKind.Sequence => "Sequential children",
                EPipelineDebugNodeKind.Parallel => "Concurrent children",
                EPipelineDebugNodeKind.Race => "First completed child wins",
                EPipelineDebugNodeKind.Conditional when phase is AbilityConditionalPhase<TCtx> conditional =>
                    "No match: " + conditional.NoConditionBehavior,
                EPipelineDebugNodeKind.Gate when phase is AbilityGatePhase<TCtx> gate =>
                    GetDebugConditionName(gate.Condition) + " · " + gate.Condition.CheckStrategy,
                EPipelineDebugNodeKind.Composite => "Composite phase",
                _ => string.Empty
            };
        }

        private static string ComputeStructureId(
            IReadOnlyList<PipelinePhaseDebugNode> roots,
            IReadOnlyList<PipelinePhaseDebugEdge> edges)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < roots.Count; i++) AppendNodeHash(roots[i], ref hash);
            for (int i = 0; i < edges.Count; i++)
            {
                AppendHash(edges[i].SourceNodeKey, ref hash);
                AppendHash(edges[i].TargetNodeKey, ref hash);
                AppendHash(edges[i].Kind.ToString(), ref hash);
                AppendHash(edges[i].Label, ref hash);
            }
            return hash.ToString("X16");
        }

        private static void AppendNodeHash(PipelinePhaseDebugNode node, ref ulong hash)
        {
            AppendHash(node.NodeKey, ref hash);
            AppendHash(node.PhaseId.ToString(), ref hash);
            AppendHash(node.PhaseType, ref hash);
            AppendHash(node.Kind.ToString(), ref hash);
            for (int i = 0; i < node.Children.Count; i++) AppendNodeHash(node.Children[i], ref hash);
        }

        private static void AppendHash(string value, ref ulong hash)
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 1099511628211UL;
            }
            hash ^= 0xff;
            hash *= 1099511628211UL;
        }
    }
}
