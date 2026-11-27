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
    /// <summary>
    /// 校验和发布 MOBA 生产技能流程，编排交互与批处理共用的输出。
    /// </summary>
    public static class MobaSkillPipelineExporter
    {
        internal const string FlowAsset = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
        private const string ResourceRoot = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba";
        private const string LubanSkills = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/skills.json";
        private const string ConsoleRoot = "../src/AbilityKit.Demo.Moba.Console/Configs/moba";
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// 比对正式产物；内容不一致时只保存本地诊断预览并报错。
        /// </summary>
        public static void Check() => Export(false);

        /// <summary>
        /// 按既有批处理契约校验后直接发布全量流程，不弹确认。
        /// </summary>
        public static void Apply() => Export(true);

        private static void Export(bool apply)
        {
            var rows = ReadValidatedFlows();
            foreach (var output in EnumerateOutputs(rows))
            {
                var change = CompareOutput(output.Path, output.Content);
                if (change == OutputChange.Unchanged) continue;
                if (apply)
                    WriteOutput(output.Path, output.Content);
                else if (change == OutputChange.Create)
                    throw new FileNotFoundException("Published flow is missing", output.Path);
                else
                {
                    var run = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
                    var preview = Absolute($"../local/Logs/moba-pipeline-preview/{run}/{output.ReportPath}");
                    WriteOutput(preview, output.Content);
                    throw new InvalidDataException($"Published flow differs from SkillFlowCO: {output.Path}. Preview: {preview}");
                }
            }
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[MobaSkillPipelineExporter] {(apply ? "Published" : "Validated")} {rows.Count} flows.");
        }

        internal static void PublishFromInspector(Func<string, bool> confirm)
        {
            var rows = ReadValidatedFlows();
            var pending = new List<(string Path, string Content)>();
            var unityCreate = 0;
            var unityUpdate = 0;
            var consoleCreate = 0;
            var consoleUpdate = 0;
            foreach (var output in EnumerateOutputs(rows))
            {
                var change = CompareOutput(output.Path, output.Content);
                if (change == OutputChange.Unchanged) continue;
                pending.Add((output.Path, output.Content));
                if (output.ReportPath.StartsWith("Unity/", StringComparison.Ordinal))
                {
                    if (change == OutputChange.Create) unityCreate++;
                    else unityUpdate++;
                }
                else
                {
                    if (change == OutputChange.Create) consoleCreate++;
                    else consoleUpdate++;
                }
            }

            if (pending.Count == 0)
            {
                Debug.Log("[MobaSkillPipelineExporter] 发布产物与源文件一致，无需写入。");
                return;
            }

            var preview = $"已校验 {rows.Count} 条流程：\n{FlowAsset}\n\n" +
                $"{ResourceRoot}\n  聚合与分片：新建 {unityCreate}，覆盖 {unityUpdate}\n" +
                $"{ConsoleRoot}\n  聚合：新建 {consoleCreate}，覆盖 {consoleUpdate}\n\n" +
                "不删除旧分片，不写入 Luban 或 Trigger。发布不是事务，失败时请核对版本控制差异。";
            if (!confirm(preview)) return;

            foreach (var output in pending)
                WriteOutput(output.Path, output.Content);
            AssetDatabase.Refresh();
            Debug.Log($"[MobaSkillPipelineExporter] Published {rows.Count} flows.");
        }

        private static List<SkillFlowDTO> ReadValidatedFlows()
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
                var dto = flow.ToDto();
                SkillRecoveryContract.Validate(dto);
                BasicAttackFlowDTO.Validate(dto);
                rows.Add(dto);
            }
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            ValidateSkillReferences(ids);
            return rows;
        }

        private static IEnumerable<(string Path, string ReportPath, string Content)> EnumerateOutputs(List<SkillFlowDTO> rows)
        {
            var unityRoot = Absolute(ResourceRoot);
            var consoleRoot = Absolute(ConsoleRoot);
            var aggregate = JsonConvert.SerializeObject(rows, JsonSettings) + "\n";
            yield return (Path.Combine(unityRoot, "skill_flows.json"), "Unity/skill_flows.json", aggregate);
            yield return (Path.Combine(consoleRoot, "skill_flows.json"), "Console/skill_flows.json", aggregate);
            foreach (var row in rows)
            {
                var filename = $"skill_flows_{row.Id}.json";
                var content = JsonConvert.SerializeObject(row, JsonSettings) + "\n";
                yield return (Path.Combine(unityRoot, "skill_flows", filename), "Unity/skill_flows/" + filename, content);
            }
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

        private enum OutputChange
        {
            Unchanged,
            Create,
            Update
        }

        private static OutputChange CompareOutput(string path, string content)
        {
            if (!File.Exists(path)) return OutputChange.Create;
            return JToken.DeepEquals(Canonical(File.ReadAllText(path)), Canonical(content))
                ? OutputChange.Unchanged
                : OutputChange.Update;
        }

        private static void WriteOutput(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidDataException(path));
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
