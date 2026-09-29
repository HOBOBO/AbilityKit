using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaSkillPipelineExporter
    {
        private const string FlowAsset = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string ResourceRoot = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba";
        private const string LubanSkills = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/skills.json";
        private const string ConsoleRoot = "../src/AbilityKit.Demo.Moba.Console/Configs/moba";
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        [MenuItem("Tools/AbilityKit/Demos/Moba/Pipeline/Validate Published Flows")]
        public static void Check() => Export(false);

        [MenuItem("Tools/AbilityKit/Demos/Moba/Pipeline/Publish Flows")]
        public static void Apply() => Export(true);

        private static void Export(bool apply)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(FlowAsset);
            if (asset?.dataList == null || asset.dataList.Length == 0)
                throw new InvalidDataException("SkillFlowCO has no authored flows: " + FlowAsset);

            var ids = new HashSet<int>();
            var rows = new List<SkillFlowDTO>(asset.dataList.Length);
            foreach (var flow in asset.dataList)
            {
                if (flow == null || flow.Id <= 0 || !ids.Add(flow.Id))
                    throw new InvalidDataException("Null, invalid or duplicate SkillFlow ID: " + flow?.Id);
                ValidatePhases(flow);
                rows.Add(flow.ToDto());
            }
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            ValidateSkillReferences(ids);

            var unityRoot = Absolute(ResourceRoot);
            var consoleRoot = Absolute(ConsoleRoot);
            var aggregate = JsonConvert.SerializeObject(rows, JsonSettings) + "\n";
            Publish(Path.Combine(unityRoot, "skill_flows.json"), aggregate, apply);
            Publish(Path.Combine(consoleRoot, "skill_flows.json"), aggregate, apply);
            foreach (var row in rows)
            {
                var filename = $"skill_flows_{row.Id}.json";
                var content = JsonConvert.SerializeObject(row, JsonSettings) + "\n";
                Publish(Path.Combine(unityRoot, "skill_flows", filename), content, apply);
            }
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[MobaSkillPipelineExporter] {(apply ? "Published" : "Validated")} {rows.Count} flows.");
        }

        private static void ValidatePhases(SkillFlowDef flow)
        {
            if (flow.Phases == null || flow.Phases.Count == 0)
                throw new InvalidDataException($"SkillFlow {flow.Id} has no phases.");
            var phaseIds = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<SkillPhaseDef>();
            foreach (var phase in flow.Phases) ValidatePhase(flow.Id, phase, phaseIds, visited);
        }

        private static void ValidatePhase(int flowId, SkillPhaseDef phase, HashSet<string> phaseIds, HashSet<SkillPhaseDef> visited)
        {
            if (phase == null || !visited.Add(phase))
                throw new InvalidDataException($"SkillFlow {flowId} has a null or reused phase.");
            if (!string.IsNullOrWhiteSpace(phase.PhaseId) && !phaseIds.Add(phase.PhaseId.Trim()))
                throw new InvalidDataException($"SkillFlow {flowId} repeats phase ID {phase.PhaseId}.");
            if (phase is SkillCompositePhaseDef composite)
            {
                if (composite.Children == null || composite.Children.Count == 0)
                    throw new InvalidDataException($"SkillFlow {flowId} has an empty composite phase.");
                foreach (var child in composite.Children) ValidatePhase(flowId, child, phaseIds, visited);
            }
            if (phase is SkillRepeatPhaseDef repeat)
            {
                if (repeat.RepeatCount <= 0) throw new InvalidDataException($"SkillFlow {flowId} has an invalid repeat count.");
                ValidatePhase(flowId, repeat.Phase, phaseIds, visited);
            }
            if (phase is SkillTimelinePhaseDef timeline && timeline.MobaLogicTimeline != null &&
                !AssetDatabase.GetAssetPath(timeline.MobaLogicTimeline).EndsWith(".moba.logic.json", StringComparison.Ordinal))
                throw new InvalidDataException($"SkillFlow {flowId} references a non-logic ActionEditor asset.");
            phase.ToDto();
        }

        private static void ValidateSkillReferences(HashSet<int> flowIds)
        {
            var path = Absolute(LubanSkills);
            var skills = JsonConvert.DeserializeObject<SkillDTO[]>(File.ReadAllText(path));
            foreach (var skill in skills ?? Array.Empty<SkillDTO>())
            {
                if (skill.PreCastFlowId > 0 && !flowIds.Contains(skill.PreCastFlowId))
                    throw new InvalidDataException($"Skill {skill.Id} references missing precast flow {skill.PreCastFlowId}.");
                if (skill.CastFlowId > 0 && !flowIds.Contains(skill.CastFlowId))
                    throw new InvalidDataException($"Skill {skill.Id} references missing cast flow {skill.CastFlowId}.");
            }
        }

        private static void Publish(string path, string content, bool apply)
        {
            if (!apply)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Published flow is missing", path);
                var actual = Canonical(File.ReadAllText(path));
                var expected = Canonical(content);
                if (!JToken.DeepEquals(actual, expected))
                {
                    var preview = Absolute("../local/moba-pipeline-preview/" + Path.GetFileName(path));
                    Directory.CreateDirectory(Path.GetDirectoryName(preview) ?? throw new InvalidDataException(preview));
                    File.WriteAllText(preview, content);
                    throw new InvalidDataException("Published flow differs from SkillFlowCO. Preview: " + preview);
                }
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidDataException(path));
            if (!File.Exists(path) || !JToken.DeepEquals(Canonical(File.ReadAllText(path)), Canonical(content)))
                File.WriteAllText(path, content);
        }

        private static JToken Canonical(string json)
        {
            var token = JToken.Parse(json);
            var dto = token.Type == JTokenType.Array
                ? JToken.FromObject(token.ToObject<SkillFlowDTO[]>())
                : JToken.FromObject(token.ToObject<SkillFlowDTO>());
            PruneDefaults(dto);
            return dto;
        }

        private static void PruneDefaults(JToken token)
        {
            if (token is JArray array)
            {
                foreach (var child in array) PruneDefaults(child);
                return;
            }
            if (!(token is JObject obj)) return;
            foreach (var property in new List<JProperty>(obj.Properties()))
            {
                PruneDefaults(property.Value);
                var value = property.Value;
                if (value.Type == JTokenType.Null ||
                    value.Type == JTokenType.Boolean && !value.Value<bool>() ||
                    value.Type == JTokenType.Integer && value.Value<long>() == 0 ||
                    value.Type == JTokenType.Float && value.Value<double>() == 0 ||
                    value is JContainer container && !container.HasValues)
                    property.Remove();
            }
        }

        private static string Absolute(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
