using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Content migration from showcase coordinates to gameplay-owned anchors.</summary>
    public static class VeigarLivePresentationSync
    {
        private const string Root = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/";
        private const string Source = Root + "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_";
        private const string Destination = Root + "MobaVfx/Veigar/";

        public static void Publish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Destination);
            foreach (var name in new[] { "AA_Projectile", "Q_Projectile", "R_Projectile", "Q_Hit", "R_Hit" })
            {
                var visual = Extract(name, name);
                try
                {
                    if (name.EndsWith("Projectile"))
                        foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = particle.main;
                            main.prewarm = false;
                        }
                    Save(visual, name);
                }
                finally { Object.DestroyImmediate(visual); }
            }
            PublishAreaResources();
            foreach (var id in new[] { 30040101, 30040201, 30040301, 30040401, 30040501 })
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    Root + "moba/action_timeline/skill_" + id + ".json");
                NBC.ActionEditor.LogicJsonExporter.ExportSelectedTimeline();
            }
            MobaSkillPipelineExporter.Apply();
            MobaSkillPipelineExporter.Check();
            AssetDatabase.SaveAssets();
            Debug.Log("[VeigarLivePresentationSync] Anchored projectile, W sequence, E sequence and timelines published.");
        }

        public static void PublishAreaResources()
        {
            Directory.CreateDirectory(Destination);
            PublishDarkMatter();
            PublishCage();
        }

        private static GameObject Extract(string source, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + source + ".prefab")
                ?? throw new InvalidDataException("Missing formal source " + source);
            var visual = Object.Instantiate(prefab);
            visual.name = name;
            foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);
            for (var i = 0; i < visual.transform.childCount; i++)
            {
                var branch = visual.transform.GetChild(i);
                branch.localPosition = new Vector3(0, branch.localPosition.y, 0);
            }
            foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.prewarm = false;
            }
            return visual;
        }

        private static void PublishDarkMatter()
        {
            var visual = new GameObject("W_Area");
            try
            {
                var warning = Extract("W_Cast", "Warning");
                var falling = Extract("W_Projectile", "Falling");
                var impact = Extract("W_Explosion", "Impact");
                warning.transform.SetParent(visual.transform, false);
                falling.transform.SetParent(visual.transform, false);
                impact.transform.SetParent(visual.transform, false);
                // The showcase warning ring has a visible radius of 1.2 world
                // units. Match the published 2.4-unit impact footprint.
                warning.transform.localScale = Vector3.one * 2f;
                foreach (var particle in warning.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                // These stretched meshes are showcase carriers below the ground.
                // The imported Meteor is the falling body, with the original particles.
                foreach (var renderer in falling.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.enabled = renderer.name == "Meteor";
                var meteor = falling.transform.Find("Veigar_W_MainProjectile/Meteor")
                    ?? throw new InvalidDataException("Missing Dark Matter meteor.");
                var meteorBounds = meteor.GetComponent<MeshRenderer>().bounds;
                var landingOffset = 0.05f - meteorBounds.min.y;
                falling.SetActive(false);
                impact.SetActive(false);
                var clip = NewClip("W_Area", 2.471f);
                Active(clip, "Warning", true, 1.221f, false);
                Active(clip, "Falling", false, 0.65f, true, 1.221f, false);
                Active(clip, "Impact", false, 1.221f, true);
                Curve(clip, "Falling", "m_LocalPosition.y", new[] {
                    new Keyframe(0, 0), new Keyframe(0.65f, 0), new Keyframe(1.221f, landingOffset)
                });
                Bind(visual, "W_Area", clip);
                Save(visual, "W_Area");
            }
            finally { Object.DestroyImmediate(visual); }
        }

        private static void PublishCage()
        {
            var visual = new GameObject("E_Area");
            try
            {
                var warning = Extract("E_Activation", "Warning");
                var cage = Extract("E_Cage", "Boundary");
                warning.transform.SetParent(visual.transform, false);
                cage.transform.SetParent(visual.transform, false);
                var body = cage.transform.Find("Cage")
                    ?? throw new InvalidDataException("Missing Event Horizon cage.");
                var fullScale = body.localScale;
                cage.SetActive(false);
                var clip = NewClip("E_Area", 3.5f);
                Active(clip, "Warning", true, 0.5f, false);
                Active(clip, "Boundary", false, 0.5f, true, 3.5f, false);
                // Retain the imported vertical deployment, without its showcase
                // delay/hide keys or rotating the boundary away from its collider.
                Curve(clip, "Boundary/Cage", "m_LocalScale.y", new[] {
                    new Keyframe(0, fullScale.y * .5f), new Keyframe(.5f, fullScale.y * .5f),
                    new Keyframe(.65f, fullScale.y), new Keyframe(3.5f, fullScale.y)
                });
                // Unity combines scale channels into one vector binding. Preserve
                // the imported horizontal scale when animating deployment height.
                Curve(clip, "Boundary/Cage", "m_LocalScale.x", new[] {
                    new Keyframe(0, fullScale.x), new Keyframe(3.5f, fullScale.x)
                });
                Curve(clip, "Boundary/Cage", "m_LocalScale.z", new[] {
                    new Keyframe(0, fullScale.z), new Keyframe(3.5f, fullScale.z)
                });
                Bind(visual, "E_Area", clip);
                Save(visual, "E_Area");
            }
            finally { Object.DestroyImmediate(visual); }
        }

        private static AnimationClip NewClip(string name, float duration)
        {
            var clip = new AnimationClip { name = name, frameRate = 60 };
            // A constant root curve establishes the complete content lifetime.
            Curve(clip, "Duration", "m_LocalPosition.x", new[] { new Keyframe(0, 0), new Keyframe(duration, 0) });
            return clip;
        }

        private static void Active(AnimationClip clip, string path, params object[] keys)
        {
            // The first argument is the initial active flag; subsequent pairs
            // are time/flag. Kept as a content helper, never a runtime callback.
            var frames = new Keyframe[(keys.Length + 1) / 2];
            frames[0] = new Keyframe(0, (bool)keys[0] ? 1 : 0);
            for (var i = 1; i < keys.Length; i += 2)
                frames[(i + 1) / 2] = new Keyframe((float)keys[i], (bool)keys[i + 1] ? 1 : 0);
            var curve = new AnimationCurve(frames);
            for (var i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"), curve);
        }

        private static void Curve(AnimationClip clip, string path, string property, Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void Bind(GameObject visual, string name, AnimationClip clip)
        {
            new GameObject("Duration").transform.SetParent(visual.transform, false);
            var clipPath = Destination + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing == null) { AssetDatabase.CreateAsset(clip, clipPath); existing = clip; }
            else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); }
            var controllerPath = Destination + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var state = machine.AddState(name);
            state.motion = existing;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(controller);
        }

        private static void Save(GameObject visual, string name)
        {
            // Area Cue radius is a scale input. Author a unit-radius root so
            // the actual battle consumer restores the intended world footprint.
            if (name == "W_Area" || name == "E_Area")
            {
                visual.transform.localScale = Vector3.one / (name == "W_Area" ? 2.4f : 3.25f);
                foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            if (PrefabUtility.SaveAsPrefabAsset(visual, Destination + name + ".prefab") == null)
                throw new InvalidDataException("Failed to save " + name);
        }
    }
}
