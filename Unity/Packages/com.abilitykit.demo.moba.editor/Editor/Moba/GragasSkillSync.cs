#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Config.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Content authoring/import only; all gameplay uses the shared consumers.</summary>
    public static class GragasSkillSync
    {
        private const string View = "Packages/com.abilitykit.demo.moba.view.runtime/";
        private const string Folder = View + "Resources/ChampionVfx/10.Gragas_StarGuardian/";
        private const string PresentationFolder = View + "Resources/moba/presentation/gragas/";

        public static void Inspect()
        {
            var output = new JArray();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Folder }))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                var states = new JArray();
                foreach (var layer in controller.layers)
                    foreach (var state in layer.stateMachine.states)
                    {
                        var clip = state.state.motion as AnimationClip;
                        states.Add(new JObject { ["key"] = layer.name + "." + state.state.name,
                            ["speed"] = state.state.speed, ["length"] = clip != null ? clip.length : 0,
                            ["frameRate"] = clip != null ? clip.frameRate : 0,
                            ["clip"] = clip != null ? AssetDatabase.GetAssetPath(clip) : "" });
                    }
                output.Add(new JObject { ["controller"] = AssetDatabase.GetAssetPath(controller), ["states"] = states,
                    ["parameters"] = JArray.FromObject(controller.parameters.Select(p => new { p.name, type = p.type.ToString() })) });
            }
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/gragas-animation-facts.json"));
            File.WriteAllText(path, output.ToString(Formatting.Indented));
            Debug.Log("[GragasSkillSync] Inspected animation facts: " + path);
        }

        public static void InspectQAnimationSegments()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/Gragas_VFX_Q_Projectile.anim")
                ?? throw new InvalidOperationException("Missing Q projectile clip.");
            var times = new[] { 0f, .45833334f, 1.1666666f, 1.3333334f, 4.5f, 4.7083335f,
                4.7916665f, 4.875f, 5.0416665f, 5.1666665f, 8.458333f };
            var curves = new JArray();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                curves.Add(new JObject { ["path"] = binding.path, ["property"] = binding.propertyName,
                    ["type"] = binding.type.FullName,
                    ["keys"] = JArray.FromObject(curve.keys.Select(k => new { k.time, k.value })),
                    ["samples"] = JArray.FromObject(times.Select(t => new { time = t, value = curve.Evaluate(t) })) });
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            var result = new JObject { ["clip"] = AssetDatabase.GetAssetPath(clip), ["length"] = clip.length,
                ["startTime"] = settings.startTime, ["stopTime"] = settings.stopTime,
                ["loopTime"] = settings.loopTime, ["curves"] = curves };
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/gragas-q-animation-segments.json"));
            File.WriteAllText(path, result.ToString(Formatting.Indented));
            Debug.Log("[GragasSkillSync] Read-only Q curve inspection: " + path);
        }

        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildQPresentation();
            BuildRPresentation();
            BuildEDashPresentation();
            var flows = AssetDatabase.LoadAssetAtPath<TextAsset>(View + "Configs/Moba/GragasSkillFlows.json");
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(View + "Configs/Moba/SkillFlowCO.asset");
            if (flows == null || asset == null || EditorUtility.IsDirty(asset))
                throw new InvalidOperationException("Missing or unsaved flow authoring source.");
            foreach (SkillFlowDTO dto in LubanConfigGroupDeserializer.Instance.DeserializeFromText(flows.text, typeof(SkillFlowDTO)))
            {
                var previous = asset.dataList.SingleOrDefault(f => f.Id == dto.Id);
                var flow = SkillFlowDef.FromDto(dto, previous);
                var source = View + "Resources/moba/action_timeline/skill_" + dto.Id + ".json";
                var oldSelection = Selection.activeObject;
                try
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(source)
                        ?? throw new InvalidOperationException("Missing timeline " + source);
                    NBC.ActionEditor.LogicJsonExporter.ExportSelectedTimeline();
                }
                finally { Selection.activeObject = oldSelection; }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var timeline = flow.Phases.OfType<SkillTimelinePhaseDef>().Single();
                timeline.MobaLogicTimeline = AssetDatabase.LoadAssetAtPath<TextAsset>(source.Replace(".json", ".moba.logic.json"));
                timeline.Timeline = AbilityKit.Demo.Moba.Services.MobaActionTimelineCompiler.CompileLogicPhase(timeline.MobaLogicTimeline.text);
                if (previous != null) asset.dataList[Array.IndexOf(asset.dataList, previous)] = flow;
                else asset.dataList = asset.dataList.Concat(new[] { flow }).ToArray();
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            Debug.Log("[GragasSkillSync] Imported, exported and checked formal skill flows/timelines.");
        }

        // Content derivation only. Original assets live behind a junction and stay read-only.
        public static void BuildQPresentation()
        {
            const string target = PresentationFolder;
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", target)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/Gragas_VFX_Q_Projectile.anim")
                ?? throw new InvalidOperationException("Missing Q projectile animation.");
            var controllerPath = target + "Q_Barrel.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var names = new[] { "flight", "deployed", "detonating" };
            var starts = new[] { 0f, 11f / 24f, 113f / 24f };
            var ends = new[] { 11f / 24f, 113f / 24f, 125f / 24f };
            for (var i = 0; i < names.Length; i++)
            {
                var path = target + "Q_" + names[i] + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                clip.ClearCurves();
                clip.frameRate = original.frameRate;
                foreach (var binding in AnimationUtility.GetCurveBindings(original))
                {
                    var source = AnimationUtility.GetEditorCurve(original, binding);
                    var keys = source.keys.Where(k => k.time > starts[i] && k.time < ends[i])
                        .Select(k => { k.time -= starts[i]; return k; }).ToList();
                    keys.Insert(0, new Keyframe(0, source.Evaluate(starts[i])));
                    keys.Add(new Keyframe(ends[i] - starts[i], source.Evaluate(ends[i])));
                    AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
                }
                // No gameplay or source animation events are copied into these presentation clips.
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = ends[i] - starts[i]; settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == names[i])
                    ?? machine.AddState(names[i]);
                state.motion = clip; state.speed = 1;
                if (i == 0) machine.defaultState = state;
                EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(controller);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Gragas_StarGuardian_Q_Projectile.prefab")
                ?? throw new InvalidOperationException("Missing Q projectile prefab.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Q_Barrel";
                var animator = instance.GetComponentInChildren<Animator>(true)
                    ?? throw new InvalidOperationException("Missing Q projectile Animator.");
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(instance, target + "Q_Barrel.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            AssetDatabase.SaveAssets();
            Debug.Log("[GragasSkillSync] Derived Q flight, deployed and 0.5s detonation clips/controller/prefab.");
        }

        /// <summary>
        /// 从原展示的独立爆炸粒子和材质冲击波生成同锚点的爆心资源，不修改源预制体。
        /// </summary>
        public static void BuildRPresentation()
        {
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Gragas_StarGuardian_R_Impact.prefab");
            var wave = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Gragas_StarGuardian_R_Shockwave.prefab");
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/Gragas_VFX_R_Shockwave.anim");
            if (impact == null || wave == null || source == null)
                throw new InvalidOperationException("Missing Gragas R impact, shockwave or its material animation.");

            const float sourceLeadIn = 1f;
            const float sourceImpactGroundOffset = 0.54f;
            const float sourceShockwaveGroundOffset = 0.005f;
            var clipPath = PresentationFolder + "R_Shockwave_AtImpact.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.ClearCurves();
            clip.frameRate = source.frameRate;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = curve.keys.Where(key => key.time >= sourceLeadIn).Select(key =>
                {
                    key.time -= sourceLeadIn;
                    return key;
                }).ToList();
                if (keys.Count == 0 || keys[0].time > 0f)
                    keys.Insert(0, new Keyframe(0f, curve.Evaluate(sourceLeadIn)));
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
            }
            var controllerPath = PresentationFolder + "R_Shockwave.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.defaultState ?? machine.AddState("Impact");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(state);

            var root = new GameObject("R_Impact");
            try
            {
                var impactInstance = (GameObject)PrefabUtility.InstantiatePrefab(impact, root.transform);
                impactInstance.transform.localPosition += Vector3.up * sourceImpactGroundOffset;
                var waveInstance = (GameObject)PrefabUtility.InstantiatePrefab(wave, root.transform);
                waveInstance.transform.localPosition += Vector3.up * sourceShockwaveGroundOffset;
                foreach (var renderer in waveInstance.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = BuildGroundShockwaveMaterial(renderer.sharedMaterial);
                var animator = waveInstance.GetComponent<Animator>()
                    ?? throw new InvalidOperationException("Missing Gragas R shockwave Animator.");
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (PrefabUtility.SaveAsPrefabAsset(root, PresentationFolder + "R_Impact.prefab") == null)
                    throw new InvalidOperationException("Could not save Gragas R impact composition.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        private static Material BuildGroundShockwaveMaterial(Material source)
        {
            if (source == null || !source.HasProperty("_V_Offset") || !source.HasProperty("_Depth"))
                throw new InvalidOperationException("Gragas R shockwave material does not support the animated UV offset and depth fade.");

            var path = PresentationFolder + "R_Shockwave_" + source.name + ".mat";
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (groundMaterial == null)
            {
                groundMaterial = new Material(source);
                AssetDatabase.CreateAsset(groundMaterial, path);
            }
            else
            {
                groundMaterial.shader = source.shader;
                groundMaterial.CopyPropertiesFromMaterial(source);
            }
            groundMaterial.SetFloat("_Depth", 0f);
            EditorUtility.SetDirty(groundMaterial);
            return groundMaterial;
        }

        /// <summary>
        /// 从只读源资源抽取冲刺分支，保留相对模型的朝向和比例，不带展示爆炸。
        /// </summary>
        public static void BuildEDashPresentation()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Gragas_StarGuardian_E.prefab");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Gragas_StarGuardian_Model.prefab");
            var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/Gragas_Model_SpellE.anim");
            if (source == null || model == null || animation == null)
                throw new InvalidOperationException("Missing Gragas E presentation source, model or animation.");
            var sourceBranch = source.transform.Find("root/FX_SpellE_Root")
                ?? throw new InvalidOperationException("Missing Gragas E dash branch.");
            var root = new GameObject("E_Dash");
            var sampleModel = UnityEngine.Object.Instantiate(model);
            try
            {
                animation.SampleAnimation(sampleModel, 1f / 3f);
                var modelRoot = sampleModel.transform.Find("root")
                    ?? throw new InvalidOperationException("Missing Gragas model root.");
                var mount = new GameObject("DashMount").transform;
                mount.SetParent(root.transform, false);
                mount.localPosition = modelRoot.localPosition;
                mount.localRotation = modelRoot.localRotation;
                mount.localScale = modelRoot.localScale;
                var branch = UnityEngine.Object.Instantiate(sourceBranch.gameObject, mount, false);
                branch.name = sourceBranch.name;
                branch.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAsset(root, PresentationFolder + "E_Dash.prefab") == null)
                    throw new InvalidOperationException("Could not save Gragas E dash presentation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleModel);
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        public static void PublishRuntime()
        {
            var migration = typeof(AbilityKit.Ability.Editor.TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration", throwOnError: true);
            foreach (var method in new[] { "PublishRuntimeBatch", "CheckRuntimeBatch" })
            {
                try { migration.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
                catch (TargetInvocationException ex) { throw new InvalidOperationException(method + " failed", ex.InnerException); }
            }
            MobaStateMachinePublisher.Publish();
            MobaStateMachinePublisher.Check();
            MobaSkillPipelineExporter.Check();
            Debug.Log("[GragasSkillSync] Trigger, HFSM and flow runtime publication checked.");
        }

        public static void ImportAndSyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var migration = typeof(AbilityKit.Ability.Editor.TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration", throwOnError: true);
            try { migration.GetMethod("ImportProductionSourcesBatch", BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("Trigger source import failed", ex.InnerException); }
            SyncAndPublish();
        }

        public static void SyncAndPublish()
        {
            Apply();
            PublishRuntime();
        }
    }
}
#endif
