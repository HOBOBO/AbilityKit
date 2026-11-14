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
    public static class LuxSkillSync
    {
        private const string View = "Packages/com.abilitykit.demo.moba.view.runtime/";
        private const string Folder = View + "Resources/ChampionVfx/12.Lux_DarkStar/";

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
            Debug.Log("[LuxSkillSync] Trigger, HFSM and flow publication checked.");
        }

        private static void Invoke(Type type, string method)
        {
            try { type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException ex) { throw new InvalidOperationException(method + " failed", ex.InnerException); }
        }

        public static void ApplyFlows()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(View + "Configs/Moba/LuxSkillFlows.json");
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
            const string destination = View + "Resources/moba/presentation/lux/";
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", destination)));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var suffix in new[] { "E_Charge", "E_Explosion", "E_Projectile", "Passive_Mark", "Q_Cage", "Q_Projectile", "R_Channel", "R_Hit", "W_Projectile" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Lux_DarkStar_" + suffix + ".prefab")
                    ?? throw new InvalidOperationException("Missing Lux presentation source " + suffix);
                var instance = UnityEngine.Object.Instantiate(source);
                try
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(instance))
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    instance.name = suffix;
                    foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main;
                        main.playOnAwake = true;
                        main.stopAction = ParticleSystemStopAction.None;
                        if (suffix == "R_Hit") main.startDelay = Mathf.Max(0, main.startDelay.constantMax - .7f);
                        if (suffix == "R_Channel") main.startDelay = main.startDelay.constantMax * (10f / 7f);
                        if (suffix == "E_Charge" || suffix == "Passive_Mark" || suffix == "Q_Cage" || suffix.EndsWith("_Projectile"))
                        {
                            var accent = particle.name.Contains("Start") || particle.name.Contains("Spawn") || particle.name.Contains("Shockwave");
                            if (!accent) main.loop = true;
                        }
                    }
                    // The source beam's animation submits at 0.7s. Retiming the
                    // owned controller keeps its portal/beam sequence aligned to
                    // the approved 1s gameplay submission. Spatial normalization
                    // belongs to the game-res extraction source.
                    if(suffix=="E_Charge")
                    {
                        var ring=instance.transform.Find("FX_E_Charge/PS_RangeCircle").GetComponent<ParticleSystem>();
                        var main=ring.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                        main.duration=5f;main.startLifetime=5f;
                        main.startSize3D=false;main.startSize=6.2f/ring.transform.lossyScale.x;
                        var size=ring.sizeOverLifetime;size.enabled=false;
                        var position=ring.transform.position;position.y=.03f;ring.transform.position=position;
                        // Area cues multiply their resource by the authoritative
                        // radius. Store this area resource at unit radius.
                        instance.transform.localScale/=3.1f;
                    }
                    if(suffix=="E_Explosion")instance.transform.localScale/=.1f;
                    if (suffix == "R_Channel")
                    {
                        var mainBeam = instance.transform.Find("Fx_ULTIMATE/PS_Beam").GetComponent<ParticleSystem>();
                        var beamEmission = mainBeam.emission;
                        beamEmission.enabled = true;
                        var beamMain = mainBeam.main;
                        beamMain.startDelay = 1f;
                        // The showcase animates uniform particle size, which
                        // also changes beam length. Author fixed-range planes;
                        // retain the source materials, colors and portal pulses.
                        foreach (var plane in new[] { ("PS_Area",2f), ("PS_PreBeam",.12f),
                            ("PS_Beam",2f), ("PS_BeamLight",2f), ("PS_BeamLightCenter",.9f) })
                            FitPlanarParticle(instance.transform.Find("Fx_ULTIMATE/"+plane.Item1).GetComponent<ParticleSystem>(),plane.Item2,33.4f);
                        foreach(var path in new[]{"PS_PreBeam/PS_BlackDots","PS_Beam/PS_BlackDots","PS_Beam/PS_Distortion"})
                            ContainBeamStream(instance.transform.Find("Fx_ULTIMATE/"+path).GetComponent<ParticleSystem>(),33.4f);
                        foreach(var path in new[]{"PS_Area","PS_PreBeam"})
                        {
                            var main=instance.transform.Find("Fx_ULTIMATE/"+path).GetComponent<ParticleSystem>().main;
                            main.startLifetime=1f;
                        }
                        var animator = instance.GetComponentInChildren<Animator>(true);
                        var sourceController = animator.runtimeAnimatorController as AnimatorController;
                        var controllerPath = destination + "R_Channel.controller";
                        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
                            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceController), controllerPath);
                        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                        var sourceClip=(sourceController.layers[0].stateMachine.states[0].state.motion as AnimationClip);
                        var clipPath=destination+"R_Channel.anim";
                        if(!AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip),clipPath);
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        var areaScale=instance.transform.Find("Fx_ULTIMATE/PS_Area").localScale;
                        // Keep the charge width pulse inside the hit rectangle;
                        // its longitudinal size must stay fixed throughout it.
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Fx_ULTIMATE/PS_Area",typeof(Transform),"m_LocalScale.x"),AnimationCurve.Linear(0,areaScale.x*.3f,.7f,areaScale.x));
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Fx_ULTIMATE/PS_Area",typeof(Transform),"m_LocalScale.y"),AnimationCurve.Constant(0,.7f,areaScale.y));
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Fx_ULTIMATE/PS_Area",typeof(Transform),"m_LocalScale.z"),AnimationCurve.Constant(0,.7f,areaScale.z));
                        EditorUtility.SetDirty(clip);
                        foreach (var layer in controller.layers)
                            foreach (var state in layer.stateMachine.states) {state.state.speed = .7f;state.state.motion=clip;}
                        EditorUtility.SetDirty(controller);
                        animator.runtimeAnimatorController = controller;
                    }
                    PrefabUtility.SaveAsPrefabAsset(instance, destination + suffix + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[LuxSkillSync] Owned missile, mark, cage, orb and retimed beam prefabs saved.");
        }

        private static void FitPlanarParticle(ParticleSystem particle,float width,float length)
        {
            var renderer=particle.GetComponent<ParticleSystemRenderer>();
            var bounds=renderer.mesh.bounds;
            var position=particle.transform.localPosition;position.x=0;position.z=0;particle.transform.localPosition=position;
            var scale=particle.transform.lossyScale;
            var main=particle.main;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.startSize3D=true;
            main.startSizeX=width/(bounds.size.x*Mathf.Abs(scale.x));
            main.startSizeY=1;
            main.startSizeZ=length/(bounds.size.z*Mathf.Abs(scale.z));
            var size=particle.sizeOverLifetime;size.enabled=false;
            var speedSize=particle.sizeBySpeed;speedSize.enabled=false;
        }

        private static void ContainBeamStream(ParticleSystem particle,float length)
        {
            var position=particle.transform.localPosition;position.z=0;particle.transform.localPosition=position;
            var main=particle.main;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var velocity=particle.velocityOverLifetime;velocity.enabled=false;
            var force=particle.forceOverLifetime;force.enabled=false;
            var scale=particle.transform.lossyScale;
            var shape=particle.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;
            var localLength=length/Mathf.Abs(scale.z);
            var sign=Mathf.Sign(Vector3.Dot(particle.transform.forward,Vector3.forward));
            shape.scale=new Vector3(.6f/Mathf.Abs(scale.x),.6f/Mathf.Abs(scale.y),localLength);
            shape.position=new Vector3(0,0,sign*localLength/2);
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
                    ["rootScale"] = prefab.transform.localScale.ToString("F5"),
                    ["meshes"] = JArray.FromObject(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => new {
                        path = AnimationUtility.CalculateTransformPath(r.transform, prefab.transform),
                        bounds = r.bounds.size.ToString("F5"), localBounds = r.localBounds.size.ToString("F5"),
                        scale = r.transform.lossyScale.ToString("F5") })),
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
            var destination = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/lux-unity-resource-facts.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, output.ToString(Formatting.Indented));
            Debug.Log("[LuxSkillSync] Read-only resource inspection complete: " + destination);
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
