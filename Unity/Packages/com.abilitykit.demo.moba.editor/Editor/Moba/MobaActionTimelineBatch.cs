using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Applies reviewed, explicit timeline-to-phase content mappings. Never infers hero behavior.</summary>
    public static class MobaActionTimelineBatch
    {
        public static void Apply()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-mobaActionTimelineManifest");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Provide -mobaActionTimelineManifest with a reviewed mapping file.");
            ApplyManifest(args[index + 1]);
        }

        public static void ApplyManifest(string path)
        {
            var manifest = JObject.Parse(File.ReadAllText(path));
            if (manifest.Value<int>("schemaVersion") != 1 || !(manifest["entries"] is JArray entries))
                throw new InvalidDataException("Unsupported timeline mapping manifest.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(manifest.Value<string>("flowAsset"));
            if (asset?.dataList == null || EditorUtility.IsDirty(asset))
                throw new InvalidDataException("Flow authoring asset is missing or has unsaved edits.");
            var inputs = entries.OfType<JObject>().Select(e => e.Value<string>("source"))
                .Concat(entries.OfType<JObject>().Where(e => e["showcase"] != null).Select(e => e.Value<string>("showcase")))
                .Concat(new[] { manifest.Value<string>("flowAsset") }).Distinct()
                .ToDictionary(Absolute, p => File.ReadAllText(Absolute(p)), StringComparer.Ordinal);
            var previousSelection = Selection.activeObject;
            try
            {
                // Export with the existing ActionEditor exporter. Do not hand-write runtime partitions.
                foreach (var source in entries.OfType<JObject>().Select(e => e.Value<string>("source")).Distinct())
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(source)
                        ?? throw new InvalidDataException("Missing timeline source: " + source);
                    NBC.ActionEditor.LogicJsonExporter.ExportSelectedTimeline();
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                // Validate all mappings before changing the shared authoring asset.
                var pending = entries.OfType<JObject>().Where(e => e["flowId"] != null).Select(entry =>
                {
                    var flow = asset.dataList.Single(f => f != null && f.Id == entry.Value<int>("flowId"));
                    var phaseIndex = entry.Value<int>("phaseIndex");
                    if (phaseIndex < 0 || phaseIndex >= flow.Phases.Count ||
                        !(flow.Phases[phaseIndex] is SkillTimelinePhaseDef phase))
                        throw new InvalidDataException("Expected an existing Timeline phase: " + flow.Id);
                    var phaseId = entry.Value<string>("phaseId");
                    if (string.IsNullOrWhiteSpace(phaseId) || (!string.IsNullOrWhiteSpace(phase.PhaseId) && phase.PhaseId != phaseId))
                        throw new InvalidDataException("Phase mapping changed; review the manifest: " + flow.Id);
                    var sourcePath = entry.Value<string>("source");
                    var logic = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath.Substring(0, sourcePath.Length - 5) + ".moba.logic.json")
                        ?? throw new InvalidDataException("Missing logic export: " + sourcePath);
                    var compiled = MobaActionTimelineCompiler.CompileLogicPhase(logic.text);
                    var oldEvents = JToken.FromObject(phase.ToDto().Timeline.Events ?? Array.Empty<AbilityKit.Demo.Moba.Share.Config.SkillTimelineEventDTO>());
                    var newEvents = JToken.FromObject(compiled.Events);
                    if (!JToken.DeepEquals(oldEvents, newEvents))
                        throw new InvalidDataException("Timeline migration would change effect timing or failure policy: " + flow.Id);
                    return new { flow, phase, phaseId, logic, compiled, entry };
                }).ToArray();
                if (pending.Select(p => p.flow.Id + "/" + p.phaseId).Distinct().Count() != pending.Length)
                    throw new InvalidDataException("Duplicate phase mapping.");
                if (inputs.Any(p => File.ReadAllText(p.Key) != p.Value))
                    throw new InvalidDataException("Authoring input changed during export; reload and review before applying.");
                foreach (var item in pending)
                {
                    item.phase.PhaseId = item.phaseId;
                    item.phase.MobaLogicTimeline = item.logic;
                    item.phase.Timeline = item.compiled;
                }
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                // Maintain the actual import sources as well; reimport preserves explicit references by phase ID.
                foreach (var item in pending.Where(p => p.entry["showcase"] != null))
                {
                    var showcase = item.entry.Value<string>("showcase");
                    var absolute = Absolute(showcase);
                    var original = JToken.Parse(File.ReadAllText(absolute));
                    var token = JToken.FromObject(item.flow.ToDto(), JsonSerializer.Create(
                        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
                    if (original is JArray) token = new JArray(token);
                    File.WriteAllText(absolute, token.ToString(Formatting.Indented) + "\n");
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                MobaSkillPipelineExporter.Apply();
                MobaSkillPipelineExporter.Check();
                Debug.Log("[MobaActionTimelineBatch] Exported " + entries.Count + " sources; bound and checked " + pending.Length + " phases.");
            }
            finally { Selection.activeObject = previousSelection; }
        }

        private static string Absolute(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
