#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Editor
{
    public static class DemoGameplayCompositionBuilder
    {
        private const string MenuPath = "Tools/AbilityKit/Demos/Build Unified Gameplay Composition";
        private const string CompositionRoot = "Assets/DemoComposition";
        private const string PrefabDirectory = CompositionRoot + "/Prefabs";
        private const string ProfileDirectory = CompositionRoot + "/Profiles";
        private const string MobaRootPath = PrefabDirectory + "/MobaDemoRoot.prefab";
        private const string ShooterLocalRootPath = PrefabDirectory + "/ShooterLocalDemoRoot.prefab";
        private const string ShooterMultiplayerRootPath = PrefabDirectory + "/ShooterMultiplayerDemoRoot.prefab";
        private const string BootstrapPrefabPath = PrefabDirectory + "/DemoGameplayBootstrap.prefab";
        private const string CatalogPath = ProfileDirectory + "/DemoGameplayCatalog.asset";
        private const string GameplayScenePath = "Assets/Scenes/" + DemoSceneRoutes.Gameplay + ".unity";
        private const string StarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";

        private static readonly ProfileDefinition[] ProfileDefinitions =
        {
            new ProfileDefinition("moba-local", DemoGameplayId.Moba, DemoLaunchMode.Local, MobaRootPath),
            new ProfileDefinition("moba-multiplayer", DemoGameplayId.Moba, DemoLaunchMode.Multiplayer, MobaRootPath),
            new ProfileDefinition("shooter-local", DemoGameplayId.Shooter, DemoLaunchMode.Local, ShooterLocalRootPath),
            new ProfileDefinition("shooter-multiplayer", DemoGameplayId.Shooter, DemoLaunchMode.Multiplayer, ShooterMultiplayerRootPath)
        };

        [MenuItem(MenuPath, priority = 20)]
        public static void GenerateAll()
        {
            EnsureFolder(PrefabDirectory);
            EnsureFolder(ProfileDirectory);

            var mobaRoot = RequireRootPrefab(MobaRootPath);
            var shooterLocalRoot = RequireRootPrefab(ShooterLocalRootPath);
            var shooterMultiplayerRoot = RequireRootPrefab(ShooterMultiplayerRootPath);

            var roots = new Dictionary<string, GameObject>(StringComparer.Ordinal)
            {
                [MobaRootPath] = mobaRoot,
                [ShooterLocalRootPath] = shooterLocalRoot,
                [ShooterMultiplayerRootPath] = shooterMultiplayerRoot
            };
            var profiles = CreateOrUpdateProfiles(roots);
            var catalog = CreateOrUpdateCatalog(profiles);
            var bootstrapPrefab = CreateOrUpdateBootstrapPrefab(catalog);
            CreateOrUpdateGameplayScene(bootstrapPrefab);
            UpdateStarterConfiguration();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DemoGameplayCompositionBuilder] Unified demo composition generated successfully.");
        }

        private static GameObject RequireRootPrefab(string prefabPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                   ?? throw new FileNotFoundException(
                       $"Required gameplay root prefab '{prefabPath}' is missing.",
                       prefabPath);
        }

        private static List<DemoGameplayProfileSO> CreateOrUpdateProfiles(
            IReadOnlyDictionary<string, GameObject> roots)
        {
            var profiles = new List<DemoGameplayProfileSO>(ProfileDefinitions.Length);
            for (var i = 0; i < ProfileDefinitions.Length; i++)
            {
                var definition = ProfileDefinitions[i];
                var path = $"{ProfileDirectory}/{ToAssetName(definition.ProfileId)}.asset";
                var profile = LoadOrCreateAsset<DemoGameplayProfileSO>(path);
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("profileId").stringValue = definition.ProfileId;
                serialized.FindProperty("gameplay").enumValueIndex = (int)definition.Gameplay;
                serialized.FindProperty("mode").enumValueIndex = (int)definition.Mode;
                serialized.FindProperty("rootPrefab").objectReferenceValue = roots[definition.RootPath];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
                profiles.Add(profile);
            }

            return profiles;
        }

        private static DemoGameplayCatalogSO CreateOrUpdateCatalog(IReadOnlyList<DemoGameplayProfileSO> profiles)
        {
            var catalog = LoadOrCreateAsset<DemoGameplayCatalogSO>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var profileList = serialized.FindProperty("profiles");
            profileList.arraySize = profiles.Count;
            for (var i = 0; i < profiles.Count; i++)
            {
                profileList.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static GameObject CreateOrUpdateBootstrapPrefab(DemoGameplayCatalogSO catalog)
        {
            var root = new GameObject("DemoGameplayBootstrap");
            try
            {
                var bootstrap = root.AddComponent<DemoGameplayBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.FindProperty("starterSceneName").stringValue = DemoSceneRoutes.Starter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, BootstrapPrefabPath)
                       ?? throw new InvalidOperationException("Failed to save the gameplay bootstrap prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateGameplayScene(GameObject bootstrapPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = PrefabUtility.InstantiatePrefab(bootstrapPrefab, scene) as GameObject
                           ?? throw new InvalidOperationException("Failed to instantiate the gameplay bootstrap prefab.");
            instance.name = "DemoGameplayBootstrap";
            if (!EditorSceneManager.SaveScene(scene, GameplayScenePath))
            {
                throw new InvalidOperationException($"Failed to save gameplay bootstrap scene '{GameplayScenePath}'.");
            }
        }

        private static void UpdateStarterConfiguration()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StarterScenePath) == null)
            {
                throw new FileNotFoundException("Multiplayer starter scene is missing.", StarterScenePath);
            }

            var scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    var behaviour = behaviours[behaviourIndex];
                    if (behaviour == null || !string.Equals(
                            behaviour.GetType().FullName,
                            "AbilityKit.Starter.MultiplayerStarterController",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var controller = new SerializedObject(behaviour);
                    var config = controller.FindProperty("config").objectReferenceValue as ScriptableObject
                                 ?? throw new InvalidOperationException(
                                     "MultiplayerStarterController has no assigned starter config.");
                    var serializedConfig = new SerializedObject(config);
                    serializedConfig.FindProperty("gameplaySceneName").stringValue = DemoSceneRoutes.Gameplay;
                    serializedConfig.FindProperty("mobaProfileId").stringValue = "moba-multiplayer";
                    serializedConfig.FindProperty("shooterProfileId").stringValue = "shooter-multiplayer";
                    serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);
                    return;
                }
            }

            throw new InvalidOperationException("Multiplayer starter scene has no MultiplayerStarterController.");
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StarterScenePath, enabled: true),
                new EditorBuildSettingsScene(GameplayScenePath, enabled: true)
            };
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string ToAssetName(string profileId)
        {
            var parts = profileId.Split('-');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
                }
            }

            return string.Concat(parts) + "Profile";
        }

        private readonly struct ProfileDefinition
        {
            public ProfileDefinition(
                string profileId,
                DemoGameplayId gameplay,
                DemoLaunchMode mode,
                string rootPath)
            {
                ProfileId = profileId;
                Gameplay = gameplay;
                Mode = mode;
                RootPath = rootPath;
            }

            public string ProfileId { get; }
            public DemoGameplayId Gameplay { get; }
            public DemoLaunchMode Mode { get; }
            public string RootPath { get; }
        }
    }
}
