using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Content migration: adapt extracted visual branches to gameplay-owned anchors.</summary>
    public static class JinxLivePresentationSync
    {
        private const string ResourcesRoot = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/";
        private const string Source = ResourcesRoot + "ChampionVfx/08.Jinx_Arcanist/Jinx_Arcanist_";
        private const string Destination = ResourcesRoot + "MobaVfx/Jinx/";

        [MenuItem("Tools/AbilityKit/Demos/Moba/Jinx/Publish Live Repair %&j")]
        public static void Publish()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Destination);
            Adapt("AA_Minigun_Projectile", "AA_Minigun_Projectile", particleSizeScale: 2f);
            Adapt("AA_Rocket_Projectile", "AA_Rocket_Projectile");
            Adapt("E_Activation_center", "E_Trap");
            Adapt("W", "W_Projectile", true);
            Adapt("R_Projectile", "R_Projectile");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var id in new[] { 30020201, 30020301, 30020401 })
            {
                var path = ResourcesRoot + "moba/action_timeline/skill_" + id + ".json";
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                    ?? throw new InvalidDataException("Missing authored timeline: " + path);
                NBC.ActionEditor.LogicJsonExporter.ExportSelectedTimeline();
            }
            JinxWSkillSync.SyncAndPublish();
            MobaStateMachinePublisher.Publish();
            MobaStateMachinePublisher.Check();
            AssetDatabase.SaveAssets();
            Debug.Log("[JinxLivePresentationSync] Gameplay anchor prefabs and QWER presentation published and checked.");
        }

        public static void PublishMinigunProjectileResource()
        {
            Directory.CreateDirectory(Destination);
            Adapt("AA_Minigun_Projectile", "AA_Minigun_Projectile", particleSizeScale: 2f);
            AssetDatabase.SaveAssets();
            Debug.Log("[JinxLivePresentationSync] Minigun projectile resource published.");
        }

        private static void Adapt(string source, string destination, bool bolt = false, float particleSizeScale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + source + ".prefab")
                ?? throw new InvalidDataException("Missing formal visual source: " + source);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.name = "Jinx_" + destination;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    Object.DestroyImmediate(animator);
                // Adapt particle size without scaling the authored muzzle height
                // or flight anchor. The source showcase uses a much closer camera.
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    if (main.startSize3D)
                    {
                        main.startSizeXMultiplier *= particleSizeScale;
                        main.startSizeYMultiplier *= particleSizeScale;
                        main.startSizeZMultiplier *= particleSizeScale;
                    }
                    else main.startSizeMultiplier *= particleSizeScale;
                }
                if (destination.EndsWith("_Projectile") && !bolt)
                {
                    // The source showcase animator reveals its missile mesh.
                    // This derived prefab has no showcase animator: gameplay
                    // creation/destruction now owns that visibility interval.
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.enabled = true;
                    // Extracted demo clips are short one-shot bursts (R: 0.17s).
                    // A gameplay projectile owns its visual until impact/expiry.
                    // Keep flight emitters alive; event sub-emitters retain their
                    // authored one-shot behavior.
                    foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (system.name.StartsWith("SE_")) continue;
                        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = system.main;
                        main.loop = true;
                        // Prewarming world-space particles before the runtime sets
                        // the projectile position leaves particles at prefab origin.
                        main.prewarm = false;
                    }
                }
                for (var i = 0; i < instance.transform.childCount; i++)
                {
                    var branch = instance.transform.GetChild(i);
                    // Extraction retained demo-scene muzzle/landing XZ offsets. The
                    // projectile/area now supplies that position, so apply it once.
                    branch.localPosition = new Vector3(0f, branch.localPosition.y, 0f);
                    if (destination == "R_Projectile")
                        branch.localPosition = Vector3.up * 0.8f;
                    if (bolt)
                    {
                        branch.localPosition = new Vector3(0f, 0.8f, 0f);
                        branch.localRotation = Quaternion.identity;
                        branch.localScale = new Vector3(0.1f, 1f, 0.12f);
                        foreach (var renderer in branch.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.enabled = true;
                            renderer.sharedMaterial = AdaptBoltMaterial(renderer.sharedMaterial);
                        }
                    }
                }
                var path = Destination + destination + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                    throw new InvalidDataException("Could not save authored runtime prefab: " + path);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static Material AdaptBoltMaterial(Material source)
        {
            // The range mesh's white additive material disappears against the
            // demo's bright floor. Reuse its formal textures with the source
            // package's alpha-composited shader so the moving bolt has a core.
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.abilitykit.demo.moba.view.runtime/GameAssets/GameResDependencies/08.Jinx_Arcanist/Shaders/AddMul.shader")
                ?? throw new InvalidDataException("Missing formal alpha-composited VFX shader.");
            if (source == null) throw new InvalidDataException("Missing formal W material.");
            var material = new Material(shader) { name = "W_MovingBolt" };
            material.SetTexture("_MainTexture", source.GetTexture("_MainTexture"));
            material.SetTexture("_Noise", source.GetTexture("_Mask"));
            material.SetTextureScale("_Noise", source.GetTextureScale("_Mask"));
            material.SetColor("_Color", new Color(0.1f, 0.65f, 1f, 1f));
            material.SetFloat("_EmissiveIntensity", 1f);
            material.SetFloat("_OpacityIntensity", 4f);
            material.SetFloat("_HasNoiseEmissive", 1f);
            // Preserve readable texture silhouettes on the bright gameplay floor;
            // noise modulates the glow, not the opaque core's visibility.
            material.SetFloat("_HasNoiseOpacity", 0f);
            material.SetFloat("_NoisePower", source.GetFloat("_MaskPower"));
            material.SetFloat("_U_Speed", source.GetFloat("_U_Speed"));
            material.SetFloat("_V_Speed", source.GetFloat("_V_Speed"));
            var path = Destination + "W_MovingBolt.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            EditorUtility.CopySerialized(material, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(material);
            return existing;
        }
    }
}
