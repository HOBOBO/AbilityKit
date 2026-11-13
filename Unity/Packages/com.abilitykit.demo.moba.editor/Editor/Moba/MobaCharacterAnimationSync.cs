#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Editor-only identity binding and imported animation metadata synchronization.</summary>
    public static class MobaCharacterAnimationSync
    {
        public const string BindingPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/" +
            "MobaAnimationAuthoring/character_animation_bindings.json";
        public const string PythonPreference = "AbilityKit.Moba.AnimationSync.Python";
        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        public static string ReportDirectory => Path.Combine(RepositoryRoot, "local/Logs/character-animation-sync");

        public sealed class StateInfo
        {
            public GameObject Model;
            public AnimatorController Controller;
            public AnimatorState State;
            public AnimationClip Clip;
            public int Layer;
            public string FullName;
            public JObject Source;
            public string Label => FullName + " → " + (Clip != null ? Clip.name : "[unsupported Motion]");
        }

        public sealed class Row
        {
            public JObject Target;
            public JObject Before;
            public JObject After;
            public StateInfo State;
            public List<StateInfo> Choices = new List<StateInfo>();
            public readonly List<string> Warnings = new List<string>();
            public string Error;
            public bool Bound;
            public bool Selected;
            public bool Changed => After != null && !JToken.DeepEquals(Before, After);
            public string Label => Target["modelId"] != null ? Target.Value<int>("modelId") + " / " + Target.Value<string>("animationKey")
                : Target.Value<int>("entityCode") + " / " + Target.Value<string>("path") +
                " / " + Target.Value<int>("skillId") + " / " + string.Join("/", Target["nodeIds"].Values<string>());
        }

        public sealed class ScanResult
        {
            public JObject Authoring;
            public JObject Bindings;
            public JObject Fingerprints;
            public int EntityCode;
            public int ModelId;
            public readonly List<Row> Rows = new List<Row>();
            public readonly List<string> Errors = new List<string>();
        }

        public static ScanResult Scan(int entityCode = 0)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var snapshot = RunPython("snapshot");
            return ScanAuthoring(snapshot, entityCode);
        }

        public static ScanResult ScanModel(int modelId)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return ScanModelAuthoring(RunPython("snapshot-model"), modelId);
        }

        public static int ModelForEntity(int entityCode) => RunPython("snapshot")["actors"].OfType<JObject>()
            .Single(a => a.Value<int>("Id") == entityCode).Value<int>("ModelId");

        public static JObject[] ConfiguredModels() => RunPython("snapshot-model")["models"].OfType<JObject>().ToArray();

        public static ScanResult ScanModelAuthoring(JObject snapshot, int modelId)
        {
            if (modelId <= 0 || snapshot.Value<int>("schemaVersion") != 1 ||
                snapshot["bindings"].Value<int>("schemaVersion") != 1)
                throw new InvalidDataException("Invalid model animation scan.");
            var scan = new ScanResult { Authoring = snapshot, ModelId = modelId,
                Bindings = (JObject)snapshot["bindings"].DeepClone(),
                Fingerprints = (JObject)snapshot["fingerprints"].DeepClone() };
            var modelRow = snapshot["models"].OfType<JObject>().SingleOrDefault(m => m.Value<int>("Id") == modelId);
            if (modelRow == null) throw new InvalidDataException("模型目录不存在所选模型。");
            var model = Resources.Load<GameObject>(modelRow.Value<string>("PrefabPath"));
            if (model == null) throw new InvalidDataException("正式模型无法加载。");
            var animators = model.GetComponentsInChildren<Animator>(true).Where(a => a.runtimeAnimatorController != null).ToArray();
            if (animators.Length != 1 || !(animators[0].runtimeAnimatorController is AnimatorController controller))
                throw new InvalidDataException("需要唯一 Animator 和直接 AnimatorController。");
            if (EditorUtility.IsDirty(model) || EditorUtility.IsDirty(controller))
                throw new InvalidDataException("模型或 Controller 存在未保存修改。");
            Track(scan, AssetDatabase.GetAssetPath(model));
            Track(scan, AssetDatabase.GetAssetPath(controller));
            var states = CollectStates(model, controller);
            var bindings = scan.Bindings["bindings"].OfType<JObject>().ToArray();
            if (bindings.Select(b => TargetKey((JObject)b["target"])).Distinct().Count() != bindings.Length)
                throw new InvalidDataException("Duplicate stable animation target.");
            var covered = new HashSet<StateInfo>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in (snapshot["modelAnimations"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(e => e.Value<int>("ModelId") == modelId))
            {
                var key = entry.Value<string>("ActionKey");
                if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
                    throw new InvalidDataException("Duplicate / empty model animation key.");
                var row = new Row { Target = new JObject { ["modelId"] = modelId, ["animationKey"] = key },
                    Before = new JObject { ["animatorState"] = entry["AnimatorState"], ["frameCount"] = entry["FrameCount"],
                        ["frameRate"] = entry["FrameRate"], ["layer"] = entry["Layer"] }, Choices = states };
                scan.Rows.Add(row);
                var binding = bindings.SingleOrDefault(b => TargetKey((JObject)b["target"]) == TargetKey(row.Target));
                row.Bound = binding != null;
                var matches = states.Where(s => binding != null ? JToken.DeepEquals(s.Source, binding["source"]) :
                    s.FullName == row.Before.Value<string>("animatorState") && s.Layer == row.Before.Value<int>("layer")).ToArray();
                if (matches.Length != 1) { row.Error = "稳定动画绑定失效或无法唯一匹配，请显式选择。"; continue; }
                covered.Add(matches[0]);
                Select(scan, row, matches[0]);
                row.Selected = row.Error == null && row.Warnings.Count == 0;
            }
            foreach (var state in states.Where(s => !covered.Contains(s)))
            {
                var key = state.FullName;
                // Default keys are scoped to the model and layer, not a state-machine path.
                if (keys.Contains(key)) continue;
                keys.Add(key);
                var row = new Row { Target = new JObject { ["modelId"] = modelId, ["animationKey"] = key },
                    Before = new JObject(), Choices = states };
                scan.Rows.Add(row);
                Select(scan, row, state);
                row.Selected = false; // Export only explicitly selected new actions.
            }
            VerifyFingerprints(scan.Fingerprints);
            WriteReport(scan);
            return scan;
        }

        // Public to support isolated imported-asset integration tests without production writes.
        public static ScanResult ScanAuthoring(JObject snapshot, int entityCode = 0)
        {
            if (snapshot.Value<int>("schemaVersion") != 1 || snapshot["bindings"].Value<int>("schemaVersion") != 1)
                throw new InvalidDataException("Unsupported animation authoring schema.");
            var result = new ScanResult { Authoring = snapshot, EntityCode = entityCode,
                Bindings = (JObject)snapshot["bindings"].DeepClone(),
                Fingerprints = (JObject)snapshot["fingerprints"].DeepClone() };
            var bindingEntries = (JArray)result.Bindings["bindings"];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindingEntries.OfType<JObject>())
                if (!seen.Add(TargetKey((JObject)binding["target"])))
                    throw new InvalidDataException("Duplicate stable animation target.");
            var actors = snapshot["actors"].OfType<JObject>().ToArray();
            var models = snapshot["models"].OfType<JObject>().ToArray();
            var cache = new Dictionary<int, List<StateInfo>>();
            var modelErrors = new Dictionary<int, string>();
            foreach (var action in snapshot["actions"].OfType<JObject>())
            {
                var target = (JObject)action["target"];
                var code = target.Value<int>("entityCode");
                if (entityCode > 0 && code != entityCode) continue;
                var row = new Row { Target = (JObject)target.DeepClone(), Before = (JObject)action["animation"].DeepClone() };
                result.Rows.Add(row);
                var actor = actors.SingleOrDefault(item => item.Value<int>("Id") == code);
                if (actor == null) { row.Error = "没有对应角色模型绑定（不按实体 ID 猜测模型）。"; continue; }
                var model = models.SingleOrDefault(item => item.Value<int>("Id") == actor.Value<int>("ModelId"));
                if (model == null) { row.Error = "权威模型表缺少角色引用的模型。"; continue; }
                var modelId = model.Value<int>("Id");
                if (!cache.ContainsKey(modelId))
                {
                    try
                    {
                        var prefab = Resources.Load<GameObject>(model.Value<string>("PrefabPath"));
                        if (prefab == null) throw new InvalidDataException("正式模型无法加载：" + model.Value<string>("PrefabPath"));
                        var animators = prefab.GetComponentsInChildren<Animator>(true)
                            .Where(item => item.runtimeAnimatorController != null).ToArray();
                        if (animators.Length != 1 || !(animators[0].runtimeAnimatorController is AnimatorController controller))
                            throw new InvalidDataException("需要唯一 Animator 和直接 AnimatorController；OverrideController 不自动推断。");
                        if (EditorUtility.IsDirty(prefab) || EditorUtility.IsDirty(controller))
                            throw new InvalidDataException("模型或 Controller 存在未保存修改；请保存后重新扫描。");
                        var states = CollectStates(prefab, controller);
                        Track(result, AssetDatabase.GetAssetPath(prefab));
                        Track(result, AssetDatabase.GetAssetPath(controller));
                        cache[modelId] = states;
                    }
                    catch (Exception ex) { cache[modelId] = new List<StateInfo>(); modelErrors[modelId] = ex.Message; }
                }
                if (modelErrors.TryGetValue(modelId, out var error)) { row.Error = error; continue; }
                row.Choices = cache[modelId];
                var binding = bindingEntries.OfType<JObject>().SingleOrDefault(item =>
                    TargetKey((JObject)item["target"]) == TargetKey(target));
                List<StateInfo> candidates;
                if (binding != null)
                {
                    row.Bound = true;
                    // Source identities are deliberately not replaced by a similarly named object.
                    candidates = row.Choices.Where(item => JToken.DeepEquals(item.Source, binding["source"])).ToList();
                }
                else
                {
                    var name = row.Before.Value<string>("animatorState");
                    var layer = row.Before.Value<int>("layer");
                    candidates = row.Choices.Where(item => item.Layer == layer &&
                        (item.FullName == name || item.State.name == name)).ToList();
                }
                if (candidates.Count != 1)
                {
                    row.Error = binding != null ? "稳定绑定失效：模型、State 或 Clip 被替换/重建；请重新选择。" :
                        "现有动画名无法唯一匹配当前 Layer 的 State；请手动选择。";
                    continue;
                }
                Select(result, row, candidates[0]);
                // Timing warnings require an explicit user selection; no silent bulk acceptance.
                row.Selected = row.Error == null && row.Warnings.Count == 0;
            }
            foreach (var binding in bindingEntries.OfType<JObject>())
            {
                var target = (JObject)binding["target"];
                if (entityCode > 0 && target.Value<int>("entityCode") != entityCode) continue;
                if (!result.Rows.Any(row => TargetKey(row.Target) == TargetKey(target)))
                    result.Errors.Add("动画绑定的动作目标已不存在：" + TargetKey(target));
            }
            if (entityCode > 0)
            {
                var hfsm = MobaStateMachinePublisher.SourcePath("character_hfsm_" + entityCode);
                Track(result, hfsm);
                if (File.Exists(hfsm))
                {
                    var graph = JObject.Parse(File.ReadAllText(hfsm));
                    var paths = new HashSet<string>(StringComparer.Ordinal);
                    CollectHfsmPaths(graph, graph.Value<string>("rootMachineId"), "", paths, new HashSet<string>());
                    foreach (var row in result.Rows.Where(row => !paths.Contains(row.Target.Value<string>("path"))))
                    {
                        row.Error = "动作绑定的状态路径不在当前角色 HFSM 图中。";
                        row.Selected = false;
                    }
                }
                if (result.Rows.Count == 0) result.Errors.Add("该角色没有可同步的显式 play 动作变体。");
            }
            VerifyFingerprints(result.Fingerprints);
            WriteReport(result);
            return result;
        }

        private static void CollectHfsmPaths(JObject graph, string machineId, string prefix,
            HashSet<string> paths, HashSet<string> ancestors)
        {
            if (!ancestors.Add(machineId)) throw new InvalidDataException("Cyclic character HFSM nesting.");
            var machine = graph["machines"].OfType<JObject>().Single(item => item.Value<string>("id") == machineId);
            var machinePath = string.IsNullOrEmpty(prefix) ? machineId : prefix + "/" + machineId;
            foreach (var state in machine["states"].OfType<JObject>())
            {
                var statePath = machinePath + "/" + state.Value<string>("id");
                paths.Add(statePath);
                var child = state.Value<string>("childMachineId");
                if (!string.IsNullOrEmpty(child)) CollectHfsmPaths(graph, child, statePath, paths, ancestors);
            }
            ancestors.Remove(machineId);
        }

        public static List<StateInfo> CollectStates(GameObject model, AnimatorController controller)
        {
            var output = new List<StateInfo>();
            for (var layer = 0; layer < controller.layers.Length; layer++)
                Collect(controller.layers[layer].stateMachine, controller.layers[layer].name, layer, model, controller, output);
            return output;
        }

        private static void Collect(AnimatorStateMachine machine, string prefix, int layer,
            GameObject model, AnimatorController controller, List<StateInfo> output)
        {
            foreach (var entry in machine.states)
            {
                var state = entry.state;
                var clip = state.motion as AnimationClip;
                output.Add(new StateInfo { Model = model, Controller = controller, State = state, Clip = clip,
                    Layer = layer, FullName = prefix + "." + state.name,
                    Source = clip == null ? null : new JObject {
                        ["modelGuid"] = GuidOf(model, out var modelId), ["modelFileId"] = modelId,
                        ["controllerGuid"] = GuidOf(controller, out var controllerId), ["controllerFileId"] = controllerId,
                        ["stateGuid"] = GuidOf(state, out var stateId), ["stateFileId"] = stateId,
                        ["clipGuid"] = GuidOf(clip, out var clipId), ["clipFileId"] = clipId } });
            }
            foreach (var child in machine.stateMachines)
                Collect(child.stateMachine, prefix + "." + child.stateMachine.name, layer, model, controller, output);
        }

        private static string GuidOf(UnityEngine.Object value, out long fileId)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out fileId) || fileId == 0)
                throw new InvalidDataException("Resource has no persistent identity: " + value.name);
            return guid;
        }

        public static int DurationFrames(double seconds, double frameRate)
        {
            var frames = seconds * frameRate;
            if (seconds <= 0 || frameRate <= 0 || double.IsNaN(frames) || double.IsInfinity(frames) || frames > int.MaxValue)
                throw new InvalidDataException("Invalid imported animation duration/rate.");
            var nearest = Math.Round(frames);
            return Math.Max(1, checked((int)(Math.Abs(frames - nearest) <= 0.0001 ? nearest : Math.Ceiling(frames))));
        }

        public static void Select(ScanResult scan, Row row, StateInfo state)
        {
            row.State = state;
            row.Error = null;
            row.After = null;
            row.Warnings.Clear();
            if (state.Clip == null || state.Source == null) { row.Error = "BlendTree / 空 Motion 暂不支持自动同步。"; return; }
            try
            {
                if (EditorUtility.IsDirty(state.Clip)) throw new InvalidDataException("Clip 存在未保存修改；请保存后重新扫描。");
                var frames = DurationFrames(state.Clip.length, state.Clip.frameRate);
                row.After = new JObject { ["animatorState"] = state.FullName, ["frameCount"] = frames,
                    ["frameRate"] = state.Clip.frameRate, ["layer"] = state.Layer };
                if (!Mathf.Approximately(state.State.speed, 1f) || state.State.speedParameterActive)
                    row.Warnings.Add("State 速度不是固定 1：仅同步 Clip 元数据，不折算播放速度；实际时序须验收。");
                if (state.State.transitions.Length > 0 || HasAutomaticTransitions(state.Controller, state.Layer))
                    row.Warnings.Add("Controller 存在转换边：同步不修改转换，完整播放需单独验收。");
                var clipSettings = AnimationUtility.GetAnimationClipSettings(state.Clip);
                if (Math.Abs(clipSettings.startTime) > .0001 ||
                    Math.Abs(clipSettings.stopTime - state.Clip.length) > .0001 ||
                    float.IsNaN(clipSettings.startTime) || float.IsInfinity(clipSettings.startTime) ||
                    float.IsNaN(clipSettings.stopTime) || float.IsInfinity(clipSettings.stopTime))
                    row.Warnings.Add($"Clip 播放范围不一致：曲线/导入长度={state.Clip.length:G9}s，" +
                        $"ClipSettings={clipSettings.startTime:G9}..{clipSettings.stopTime:G9}s；" +
                        "仅同步元数据，不改播放范围，需复核正式资源。");
                var clipLoop = clipSettings.loopTime;
                if (scan.ModelId == 0)
                {
                    var original = scan.Authoring["actions"].OfType<JObject>().Single(item =>
                        TargetKey((JObject)item["target"]) == TargetKey(row.Target));
                    if (clipLoop != original.Value<bool>("loop"))
                        row.Warnings.Add("Clip 循环设置与动作 loop 不同；保留现有动作策略。");
                }
                if (Math.Abs(state.Clip.length * state.Clip.frameRate - Math.Round(state.Clip.length * state.Clip.frameRate)) > .0001)
                    row.Warnings.Add("非整帧片段：时长帧数向上取整，报告保留实际时长和误差。");
                Track(scan, AssetDatabase.GetAssetPath(state.Clip));
            }
            catch (Exception ex) { row.Error = ex.Message; }
        }

        private static bool HasAutomaticTransitions(AnimatorController controller, int layer) =>
            HasTransitions(controller.layers[layer].stateMachine);

        private static bool HasTransitions(AnimatorStateMachine machine) => machine.anyStateTransitions.Length > 0 ||
            machine.entryTransitions.Length > 0 || machine.stateMachines.Any(child => HasTransitions(child.stateMachine));

        public static string TargetKey(JObject target) => target["modelId"] != null
            ? new JArray("model", target.Value<int>("modelId"), target.Value<string>("animationKey")).ToString(Formatting.None)
            : new JArray(target.Value<int>("catalogRowId"),
            target.Value<int>("entityCode"), target.Value<string>("path"), target.Value<int>("skillId"),
            target.Value<string>("actionId"), target["nodeIds"].DeepClone()).ToString(Formatting.None);

        private static void VerifyFingerprints(JObject fingerprints)
        {
            foreach (var entry in fingerprints.Properties())
            {
                var path = Path.IsPathRooted(entry.Name) ? entry.Name : Path.Combine(RepositoryRoot, entry.Name);
                if (Hash(path) != entry.Value.Value<string>())
                    throw new InvalidDataException("Source changed during scan/publication; scan again: " + entry.Name);
            }
        }

        private static void Track(ScanResult scan, string assetPath)
        {
            var path = Path.IsPathRooted(assetPath) ? assetPath : Path.Combine(Application.dataPath, "..", assetPath);
            foreach (var candidate in new[] { Path.GetFullPath(path), Path.GetFullPath(path) + ".meta" })
            {
                var hash = Hash(candidate);
                if (scan.Fingerprints.TryGetValue(candidate, out var old) && old.Value<string>() != hash)
                    throw new InvalidDataException("Resource changed during scan; scan again: " + candidate);
                scan.Fingerprints[candidate] = hash;
            }
        }

        public static string Hash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        public static JObject BuildRequest(ScanResult scan, IEnumerable<Row> selected)
        {
            if (scan.Errors.Count > 0) throw new InvalidDataException(string.Join("\n", scan.Errors));
            VerifyFingerprints(scan.Fingerprints);
            var bindings = (JObject)scan.Bindings.DeepClone();
            var entries = (JArray)bindings["bindings"];
            var changes = new JArray();
            var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in selected)
            {
                if (!scan.Rows.Contains(row) || row.Error != null || row.After == null)
                    throw new InvalidDataException("Cannot apply unresolved animation target: " + row.Label);
                if (!selectedKeys.Add(TargetKey(row.Target)) || (scan.ModelId > 0 &&
                    (string.IsNullOrWhiteSpace(row.Target.Value<string>("animationKey")) ||
                     row.Target.Value<string>("animationKey") != row.Target.Value<string>("animationKey").Trim())))
                    throw new InvalidDataException("Duplicate / empty model animation key.");
                foreach (var existing in entries.OfType<JObject>().Where(item =>
                    TargetKey((JObject)item["target"]) == TargetKey(row.Target)).ToArray()) existing.Remove();
                entries.Add(new JObject { ["target"] = row.Target.DeepClone(), ["source"] = row.State.Source.DeepClone() });
                changes.Add(new JObject { ["target"] = row.Target.DeepClone(), ["before"] = row.Before.DeepClone(),
                    ["after"] = row.After.DeepClone() });
            }
            if (changes.Count == 0) throw new InvalidDataException("请选择至少一个已解析的动画动作。");
            // Keep deterministic order without changing unrelated binding values.
            bindings["bindings"] = new JArray(entries.OfType<JObject>().OrderBy(item => TargetKey((JObject)item["target"]), StringComparer.Ordinal));
            return new JObject { ["schemaVersion"] = 1, ["fingerprints"] = scan.Fingerprints.DeepClone(),
                ["mode"] = scan.ModelId > 0 ? "model" : "legacy",
                ["bindingHash"] = scan.Authoring["bindingHash"].DeepClone(), ["bindings"] = bindings, ["changes"] = changes };
        }

        public static void Apply(ScanResult scan, IEnumerable<Row> selected, bool publish)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出 Play Mode 后再更新或发布动画配置。");
            var request = BuildRequest(scan, selected);
            RunPython("apply", request);
            if (publish)
            {
                // Validate imported identities before any publication, including already-bound actors.
                Check();
                RunPythonScript("LubanConfig/Moba/export_pipeline.py", "--apply");
                RunPythonScript("LubanConfig/Moba/export_pipeline.py", "--check-published");
                Check();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[MobaCharacterAnimationSync] Authoritative metadata synchronized" + (publish ? " and published/checked." : "."));
        }

        public static void WriteReport(ScanResult scan)
        {
            Directory.CreateDirectory(ReportDirectory);
            var report = new JObject { ["schemaVersion"] = 1, ["entityCode"] = scan.EntityCode, ["modelId"] = scan.ModelId,
                ["errors"] = new JArray(scan.Errors), ["rows"] = new JArray(scan.Rows.Select(row => new JObject {
                    ["target"] = row.Target, ["before"] = row.Before, ["after"] = row.After,
                    ["bound"] = row.Bound, ["changed"] = row.Changed, ["error"] = row.Error,
                    ["warnings"] = new JArray(row.Warnings), ["clip"] = row.State?.Clip != null ? AssetDatabase.GetAssetPath(row.State.Clip) : null,
                    ["clipSeconds"] = row.State?.Clip != null ? row.State.Clip.length : 0,
                    ["clipSettingsStartTime"] = row.State?.Clip != null ?
                        AnimationUtility.GetAnimationClipSettings(row.State.Clip).startTime : 0,
                    ["clipSettingsStopTime"] = row.State?.Clip != null ?
                        AnimationUtility.GetAnimationClipSettings(row.State.Clip).stopTime : 0,
                    ["durationRoundingErrorSeconds"] = row.After != null ?
                        row.After.Value<double>("frameCount") / row.After.Value<double>("frameRate") - row.State.Clip.length : 0 })) };
            File.WriteAllText(Path.Combine(ReportDirectory, "scan-" + (scan.ModelId > 0 ? "model-" + scan.ModelId : scan.EntityCode.ToString()) + ".json"), report.ToString(Formatting.Indented));
        }

        [MenuItem("Tools/AbilityKit/MOBA/Check Bound Character Animations")]
        public static void Check()
        {
            var snapshot = RunPython("snapshot");
            var codes = snapshot["bindings"]["bindings"].OfType<JObject>()
                .Where(item => item["target"]["modelId"] == null)
                .Select(item => item["target"].Value<int>("entityCode")).Distinct().ToArray();
            var models = snapshot["bindings"]["bindings"].OfType<JObject>()
                .Where(item => item["target"]["modelId"] != null)
                .Select(item => item["target"].Value<int>("modelId")).Distinct().ToArray();
            if (codes.Length + models.Length == 0) throw new InvalidDataException("没有已登记动画绑定；请先扫描并同步正式模型动作。");
            var failures = new List<string>();
            var warningCount = 0;
            foreach (var scan in codes.Select(code => ScanAuthoring(snapshot, code))
                .Concat(models.Select(modelId => ScanModelAuthoring(snapshot, modelId))))
            {
                failures.AddRange(scan.Errors);
                foreach (var row in scan.Rows.Where(row => row.Bound || (scan.ModelId > 0 && row.Before.Count > 0)))
                {
                    if (row.Error != null || row.Changed || !row.Bound) failures.Add(row.Label + ": " +
                        (row.Error ?? (!row.Bound ? "模型动作缺少稳定资源绑定，请同步。" : "动画配置已漂移，请同步。")));
                    warningCount += row.Warnings.Count;
                }
            }
            if (failures.Count > 0) throw new InvalidDataException(string.Join("\n", failures));
            Debug.Log("[MobaCharacterAnimationSync] Bound animation metadata check passed; timing-review warnings=" + warningCount +
                ". Metadata agreement is not full-playback acceptance.");
        }

        public static void CheckIfConfigured()
        {
            var file = Path.Combine(RepositoryRoot, BindingPath);
            if (File.Exists(file)) Check();
        }

        public static void ScanBatch()
        {
            foreach (var code in BatchEntities()) Scan(code);
            Debug.Log("[MobaCharacterAnimationSync] Imported animation scans completed: " + ReportDirectory);
        }

        public static void SynchronizeBatch()
        {
            var acceptWarnings = Environment.GetCommandLineArgs().Contains("-mobaAnimationSyncIncludeTimingWarnings");
            foreach (var code in BatchEntities())
            {
                var scan = Scan(code);
                var rows = scan.Rows.Where(row => row.Error == null && (acceptWarnings || row.Warnings.Count == 0)).ToArray();
                if (rows.Length == 0) throw new InvalidDataException("No resolved animation targets for entity " + code);
                Apply(scan, rows, false);
            }
            Check();
            Debug.Log("[MobaCharacterAnimationSync] Batch source synchronization completed; use the normal Luban publisher to publish.");
        }

        public static void SynchronizeModelsBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-mobaAnimationSyncModels");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide -mobaAnimationSyncModels.");
            var acceptWarnings = args.Contains("-mobaAnimationSyncIncludeTimingWarnings");
            foreach (var modelId in args[index + 1].Split(',').Select(int.Parse).Distinct())
            {
                var scan = ScanModel(modelId);
                Apply(scan, scan.Rows.Where(r => r.Bound && r.Error == null && (acceptWarnings || r.Warnings.Count == 0)), false);
            }
            Check();
        }

        private static int[] BatchEntities()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-mobaAnimationSyncEntities");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide -mobaAnimationSyncEntities with explicit comma-separated actor IDs.");
            var codes = args[index + 1].Split(',').Select(int.Parse).Distinct().ToArray();
            if (codes.Any(code => code <= 0)) throw new ArgumentException("Batch actor IDs must be positive.");
            return codes;
        }

        private static JObject RunPython(string command, JObject request = null)
        {
            Directory.CreateDirectory(ReportDirectory);
            var id = Guid.NewGuid().ToString("N");
            var output = Path.Combine(ReportDirectory, "result-" + id + ".json");
            var input = Path.Combine(ReportDirectory, "request-" + id + ".json");
            try
            {
                var arguments = command + " --output " + Quote(output);
                if (request != null)
                {
                    File.WriteAllText(input, request.ToString(Formatting.Indented), new UTF8Encoding(false));
                    arguments += " --request " + Quote(input);
                }
                RunPythonScript("LubanConfig/Moba/tools/character_animation_sync.py", arguments);
                return JObject.Parse(File.ReadAllText(output));
            }
            finally { if (File.Exists(input)) File.Delete(input); if (File.Exists(output)) File.Delete(output); }
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private static void RunPythonScript(string script, string arguments)
        {
            var info = new ProcessStartInfo { FileName = EditorPrefs.GetString(PythonPreference, "python"),
                Arguments = Quote(Path.Combine(RepositoryRoot, script)) + " " + arguments,
                WorkingDirectory = RepositoryRoot, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            info.EnvironmentVariables["PYTHONUTF8"] = "1";
            using (var process = Process.Start(info))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000))
                {
                    process.Kill();
                    throw new TimeoutException("Animation sync Python process exceeded 120 seconds: " + script);
                }
                Task.WaitAll(stdout, stderr);
                if (process.ExitCode != 0) throw new InvalidOperationException(script + " failed: " + stderr.Result + "\n" + stdout.Result);
            }
        }
    }
}
#endif
