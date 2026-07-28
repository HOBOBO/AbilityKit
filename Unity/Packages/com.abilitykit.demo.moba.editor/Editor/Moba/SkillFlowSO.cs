using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SkillFlow", fileName = "SkillFlowCO")]
    public sealed class SkillFlowSO : MobaConfigTableAssetSO
    {
        public SkillFlowDef[] dataList;
        public SkillFlowDTO[] legacyDataList;

        public override string FileWithoutExt => MobaConfigPaths.SkillFlowsFile;
        public override Type EntryType => typeof(SkillFlowDTO);
        public override IEnumerable GetEntries()
        {
            if ((dataList == null || dataList.Length == 0) && legacyDataList != null && legacyDataList.Length > 0)
            {
                return legacyDataList;
            }
            if (dataList == null || dataList.Length == 0) return Array.Empty<SkillFlowDTO>();
            var list = new SkillFlowDTO[dataList.Length];
            for (int i = 0; i < dataList.Length; i++)
            {
                list[i] = dataList[i] != null ? dataList[i].ToDto() : null;
            }
            return list;
        }
    }

    internal readonly struct SkillFlowInspectorSelection
    {
        public SkillFlowInspectorSelection(
            SkillFlowSO asset,
            int flowId,
            string phaseId,
            SkillPhaseDef phase,
            string serializedPropertyPath,
            long revision)
        {
            Asset = asset;
            FlowId = flowId;
            PhaseId = phaseId ?? string.Empty;
            Phase = phase;
            SerializedPropertyPath = serializedPropertyPath ?? string.Empty;
            Revision = revision;
        }

        public SkillFlowSO Asset { get; }
        public int FlowId { get; }
        public string PhaseId { get; }
        public SkillPhaseDef Phase { get; }
        public string SerializedPropertyPath { get; }
        public long Revision { get; }
        public bool IsValid => Asset != null && FlowId > 0;
    }

    internal static class SkillFlowInspectorSelectionState
    {
        private static long _revision;
        private static SkillFlowInspectorSelection _current;

        public static SkillFlowInspectorSelection Current => _current;

        public static bool TrySelect(
            SkillFlowSO asset,
            int flowId,
            string phaseId,
            out SkillFlowInspectorSelection selection,
            out string error)
        {
            selection = default;
            if (asset == null)
            {
                error = "SkillFlow asset is missing.";
                return false;
            }

            var flows = asset.dataList;
            if (flows == null)
            {
                error = $"SkillFlow #{flowId} is not present in {asset.name}.";
                return false;
            }

            for (var flowIndex = 0; flowIndex < flows.Length; flowIndex++)
            {
                var flow = flows[flowIndex];
                if (flow == null || flow.Id != flowId) continue;

                var flowPath = $"dataList.Array.data[{flowIndex}]";
                var normalizedPhaseId = phaseId ?? string.Empty;
                if (string.IsNullOrEmpty(normalizedPhaseId))
                {
                    return Commit(asset, flowId, string.Empty, null, flowPath, out selection, out error);
                }

                var visited = new HashSet<SkillPhaseDef>();
                if (TryFindPhase(
                        flow.Phases,
                        normalizedPhaseId,
                        flowPath + ".Phases",
                        visited,
                        out var phase,
                        out var phasePath))
                {
                    return Commit(asset, flowId, normalizedPhaseId, phase, phasePath, out selection, out error);
                }

                error = $"Phase '{normalizedPhaseId}' is not present in SkillFlow #{flowId}.";
                return false;
            }

            error = $"SkillFlow #{flowId} is not present in {asset.name}.";
            return false;
        }

        private static bool Commit(
            SkillFlowSO asset,
            int flowId,
            string phaseId,
            SkillPhaseDef phase,
            string path,
            out SkillFlowInspectorSelection selection,
            out string error)
        {
            selection = new SkillFlowInspectorSelection(
                asset,
                flowId,
                phaseId,
                phase,
                path,
                ++_revision);
            _current = selection;
            error = string.Empty;
            return true;
        }

        private static bool TryFindPhase(
            IReadOnlyList<SkillPhaseDef> phases,
            string phaseId,
            string listPath,
            ISet<SkillPhaseDef> visited,
            out SkillPhaseDef match,
            out string path)
        {
            if (phases != null)
            {
                for (var i = 0; i < phases.Count; i++)
                {
                    var phase = phases[i];
                    var phasePath = $"{listPath}.Array.data[{i}]";
                    if (TryFindPhaseNode(
                            phase,
                            phaseId,
                            phasePath,
                            visited,
                            out match,
                            out path))
                    {
                        return true;
                    }
                }
            }

            match = null;
            path = string.Empty;
            return false;
        }

        private static bool TryFindPhaseNode(
            SkillPhaseDef phase,
            string phaseId,
            string phasePath,
            ISet<SkillPhaseDef> visited,
            out SkillPhaseDef match,
            out string path)
        {
            if (phase == null || !visited.Add(phase))
            {
                match = null;
                path = string.Empty;
                return false;
            }

            if (string.Equals(phase.PhaseId, phaseId, StringComparison.Ordinal))
            {
                match = phase;
                path = phasePath;
                return true;
            }

            if (phase is SkillCompositePhaseDef composite &&
                TryFindPhase(
                    composite.Children,
                    phaseId,
                    phasePath + ".Children",
                    visited,
                    out match,
                    out path))
            {
                return true;
            }

            if (phase is SkillRepeatPhaseDef repeat &&
                TryFindPhaseNode(
                    repeat.Phase,
                    phaseId,
                    phasePath + ".Phase",
                    visited,
                    out match,
                    out path))
            {
                return true;
            }

            match = null;
            path = string.Empty;
            return false;
        }
    }
}
