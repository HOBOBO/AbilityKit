#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Content inspection and authoring only; never executes gameplay.</summary>
    public static class KarthusSkillSync
    {
        private const string View = "Packages/com.abilitykit.demo.moba.view.runtime/";
        private const string Folder = View + "Resources/ChampionVfx/11.Karthus_Project/";

        public static void ImportAndSyncAndPublish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildPresentation();
            var migration = typeof(AbilityKit.Ability.Editor.TriggerAuthoringProjectAsset).Assembly.GetType(
                "AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration", throwOnError: true);
            Invoke(migration, "ImportProductionSourcesBatch");
            ApplyFlows();
            Invoke(migration, "PublishRuntimeBatch");
            Invoke(migration, "CheckRuntimeBatch");
            MobaStateMachinePublisher.Publish();
            MobaStateMachinePublisher.Check();
            MobaSkillPipelineExporter.Check();
            Debug.Log("[KarthusSkillSync] Trigger, HFSM and flow publication checked.");
        }

        private static void Invoke(Type type, string method)
        {
            try { type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException(method + " failed", ex.InnerException); }
        }

        public static void ApplyFlows()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(View + "Configs/Moba/KarthusSkillFlows.json");
            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(View + "Configs/Moba/SkillFlowCO.asset");
            if (source == null || asset == null || EditorUtility.IsDirty(asset))
                throw new InvalidOperationException("Missing or unsaved flow authoring source.");
            foreach (SkillFlowDTO dto in LubanConfigGroupDeserializer.Instance.DeserializeFromText(source.text, typeof(SkillFlowDTO)))
            {
                var previous = asset.dataList.SingleOrDefault(f => f.Id == dto.Id);
                var flow = SkillFlowDef.FromDto(dto, previous);
                var path = View + "Resources/moba/action_timeline/skill_" + dto.Id + ".json";
                var oldSelection = Selection.activeObject;
                try
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                        ?? throw new InvalidOperationException("Missing timeline " + path);
                    NBC.ActionEditor.LogicJsonExporter.ExportSelectedTimeline();
                }
                finally { Selection.activeObject = oldSelection; }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var timeline = flow.Phases.OfType<SkillTimelinePhaseDef>().Single();
                timeline.MobaLogicTimeline = AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace(".json", ".moba.logic.json"));
                timeline.Timeline = AbilityKit.Demo.Moba.Services.MobaActionTimelineCompiler.CompileLogicPhase(timeline.MobaLogicTimeline.text);
                if (previous != null) asset.dataList[Array.IndexOf(asset.dataList, previous)] = flow;
                else asset.dataList = asset.dataList.Concat(new[] { flow }).ToArray();
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
        }

        public static void BuildPresentation()
        {
            const string destination = View + "Resources/moba/presentation/karthus/";
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", destination)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            DeriveParticle("Q_Explosion", "Q_Explosion", .5f, false, false);
            DeriveParticle("Q_Cast", "Q_Warning", 0f, false, true);
            DeriveParticle("R_Hit", "R_Hit", 3f, false, false);
            DeriveParticle("E_Area", "E_Aura", 0f, true, false);
            BuildRequiemTargetPresentation();
            BuildWallPreview();
            BuildWallArea();
            AssetDatabase.SaveAssets();
            Debug.Log("[KarthusSkillSync] Derived warning, hit and looping aura presentation assets.");

            void DeriveParticle(string suffix, string output, float delayOffset, bool loop, bool warning)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Karthus_Project_" + suffix + ".prefab")
                    ?? throw new InvalidOperationException("Missing particle " + suffix);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(instance))
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    instance.name = output;
                    foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main;
                        main.startDelay = Mathf.Max(0, main.startDelay.constantMax - delayOffset);
                        if (warning)
                        {
                            main.duration = .75f; main.startLifetime = .75f; main.startDelay = 0;
                        }
                        if (loop)
                        {
                            // Activation, deactivation and the initial shadow are one-shot accents.
                            var ambient = !particle.name.Contains("Activation") && !particle.name.Contains("Deactivation") && particle.name != "PS_Shadow";
                            if (!ambient)
                            {
                                var emission = particle.emission; emission.enabled = false;
                            }
                            main.loop = ambient;
                        }
                        main.playOnAwake = true;
                        main.stopAction = ParticleSystemStopAction.None;
                    }
                    PrefabUtility.SaveAsPrefabAsset(instance, destination + output + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }

            void BuildWallPreview()
            {
                var preview = new GameObject("W_Preview");
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Karthus_Project_W_Wall.prefab")
                        ?? throw new InvalidOperationException("Missing wall presentation");
                    var child = UnityEngine.Object.Instantiate(source, preview.transform, false);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Animations/Karthus_VFX_W_Wall.anim");
                    clip.SampleAnimation(child, 1f);
                    foreach (var animator in child.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                    foreach (var particle in child.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main; main.playOnAwake = false;
                        var emission = particle.emission; emission.enabled = false;
                    }
                    // The point indicator uses radius .5, whose existing VFX
                    // preview scale is .25. Author the preview at its inverse;
                    // the formal area resource keeps its native scale.
                    child.transform.localScale = Vector3.one * 4;
                    child.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    PrefabUtility.SaveAsPrefabAsset(preview, destination + "W_Preview.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }

            void BuildWallArea()
            {
                var area = new GameObject("W_Area");
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Karthus_Project_W_Wall.prefab");
                    var child = UnityEngine.Object.Instantiate(source, area.transform, false);
                    // The existing area cue applies its six-unit broadphase
                    // radius as a scale factor. The source wall is in metres.
                    child.transform.localScale = Vector3.one / 6f;
                    PrefabUtility.SaveAsPrefabAsset(area, destination + "W_Area.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(area); }
            }

            void BuildRequiemTargetPresentation()
            {
                var warning = new GameObject("R_Warning");
                var hit = PrefabUtility.LoadPrefabContents(destination + "R_Hit.prefab");
                try
                {
                    Add(warning, "R_Orbital", false);
                    Add(warning, "R_Channel", false);
                    Add(hit, "R_Channel", true);
                    PrefabUtility.SaveAsPrefabAsset(warning, destination + "R_Warning.prefab");
                    PrefabUtility.SaveAsPrefabAsset(hit, destination + "R_Hit.prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(warning);
                    PrefabUtility.UnloadPrefabContents(hit);
                }

                void Add(GameObject owner, string suffix, bool impact)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Karthus_Project_" + suffix + ".prefab")
                        ?? throw new InvalidOperationException("Missing target presentation " + suffix);
                    var child = UnityEngine.Object.Instantiate(source, owner.transform, false);
                    child.name = suffix;
                    // The impact contains only late particles; its seeking-beam
                    // animation belongs to the preceding warning interval.
                    if (impact)
                        foreach (var animator in child.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
                    foreach (var particle in child.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main;
                        // The source mixes its three-second warning with the
                        // final orbital strike. Their owners now supply timing.
                        var late = suffix == "R_Channel" && main.startDelay.constantMax >= 2.8f;
                        var emission = particle.emission;
                        emission.enabled = emission.enabled && (impact ? late : !late);
                        if (impact) main.startDelay = Mathf.Max(0, main.startDelay.constantMax - 3f);
                        main.playOnAwake = true;
                        main.stopAction = ParticleSystemStopAction.None;
                    }
                }
            }
        }

        public static void Inspect()
        {
            var controllers = new JArray();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                var states = new JArray();
                foreach (var layer in controller.layers)
                    ReadStates(layer.stateMachine, layer.name, states);
                controllers.Add(new JObject { ["path"] = path, ["states"] = states });
            }
            var prefabs = new JArray();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                prefabs.Add(new JObject
                {
                    ["path"] = path,
                    ["animators"] = JArray.FromObject(prefab.GetComponentsInChildren<Animator>(true).Select(a => new
                    {
                        path = AnimationUtility.CalculateTransformPath(a.transform, prefab.transform),
                        controller = AssetDatabase.GetAssetPath(a.runtimeAnimatorController)
                    })),
                    ["particles"] = JArray.FromObject(prefab.GetComponentsInChildren<ParticleSystem>(true).Select(p => new
                    {
                        path = AnimationUtility.CalculateTransformPath(p.transform, prefab.transform),
                        duration = p.main.duration, loop = p.main.loop, startDelay = p.main.startDelay.constantMax,
                        startLifetime = p.main.startLifetime.constantMax, playOnAwake = p.main.playOnAwake,
                        stopAction = p.main.stopAction.ToString()
                    })),
                    ["components"] = JArray.FromObject(prefab.GetComponentsInChildren<Component>(true)
                        .Where(c => c != null).Select(c => c.GetType().FullName).Distinct().OrderBy(n => n)),
                    ["children"] = JArray.FromObject(prefab.GetComponentsInChildren<Transform>(true)
                        .Select(t => AnimationUtility.CalculateTransformPath(t, prefab.transform)))
                });
            }
            var output = new JObject { ["controllers"] = controllers, ["prefabs"] = prefabs };
            var destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/karthus-unity-resource-facts.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, output.ToString(Formatting.Indented));
            Debug.Log("[KarthusSkillSync] Read-only resource inspection complete: " + destination);
        }

        private static void ReadStates(AnimatorStateMachine machine, string prefix, JArray states)
        {
            foreach (var item in machine.states)
            {
                var clip = item.state.motion as AnimationClip;
                var settings = clip != null ? AnimationUtility.GetAnimationClipSettings(clip) : null;
                states.Add(new JObject
                {
                    ["key"] = prefix + "." + item.state.name, ["speed"] = item.state.speed,
                    ["length"] = clip != null ? clip.length : 0,
                    ["frameRate"] = clip != null ? clip.frameRate : 0,
                    ["clip"] = clip != null ? AssetDatabase.GetAssetPath(clip) : "",
                    ["loop"] = settings != null && settings.loopTime,
                    ["events"] = clip != null ? JArray.FromObject(AnimationUtility.GetAnimationEvents(clip)
                        .Select(e => new { e.time, e.functionName })) : new JArray()
                });
            }
            foreach (var child in machine.stateMachines)
                ReadStates(child.stateMachine, prefix + "." + child.stateMachine.name, states);
        }
    }
}
#endif
