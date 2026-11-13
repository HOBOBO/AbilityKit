using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Content-only adaptations of showcase resources to authoritative area anchors.</summary>
    public static class MobaAreaPresentationResourceSync
    {
        private const string ResourcesRoot = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/";

        public static void Publish()
        {
            Adapt("ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E", "MobaVfx/Jhin/E_Trap", "Trap_1");
            Adapt("ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Explosion", "MobaVfx/Veigar/W_Area");
            Adapt("ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E_Cage", "MobaVfx/Veigar/E_Area");
            AssetDatabase.SaveAssets();
            Debug.Log("[MobaAreaPresentationResourceSync] Area anchor resources published.");
        }

        private static void Adapt(string source, string destination, string retainedBranch = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesRoot + source + ".prefab")
                ?? throw new InvalidDataException("Missing area source: " + source);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.name = Path.GetFileName(destination);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    Object.DestroyImmediate(animator);
                if (!string.IsNullOrEmpty(retainedBranch) && instance.transform.Find(retainedBranch) == null)
                    throw new InvalidDataException("Missing area branch: " + source + "/" + retainedBranch);
                for (var index = instance.transform.childCount - 1; index >= 0; index--)
                {
                    var branch = instance.transform.GetChild(index);
                    if (!string.IsNullOrEmpty(retainedBranch) && branch.name != retainedBranch)
                    {
                        Object.DestroyImmediate(branch.gameObject);
                        continue;
                    }
                    // Showcase XZ landing offsets are already supplied by the area.
                    // Preserve imported units, model proportions and authored height.
                    branch.localPosition = new Vector3(0f, branch.localPosition.y, 0f);
                    branch.gameObject.SetActive(true);
                }
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
                foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main;
                    main.prewarm = false;
                }
                var path = ResourcesRoot + destination + ".prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                    throw new InvalidDataException("Could not publish area resource: " + path);
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
