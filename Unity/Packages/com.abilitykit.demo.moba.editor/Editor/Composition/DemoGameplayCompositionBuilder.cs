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
        private const string MenuPath = "Tools/AbilityKit/Demos/Build Package-Owned Gameplay Composition";
        private const string StarterScenePath = "Assets/Scenes/" + DemoSceneRoutes.Starter + ".unity";
        private const string LegacyCompositionRoot = "Assets/DemoComposition";
        private const string LegacyPrefabDirectory = LegacyCompositionRoot + "/Prefabs";
        private const string LegacyProfileDirectory = LegacyCompositionRoot + "/Profiles";
        private const string LegacyGameplayScenePath = "Assets/Scenes/DemoGameplayBootstrapScene.unity";

        private const string MobaCompositionRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/Composition";
        private const string MobaPrefabDirectory = MobaCompositionRoot + "/Prefabs";
        private const string MobaProfileDirectory = MobaCompositionRoot + "/Profiles";
        private const string MobaSceneDirectory =
            "Packages/com.abilitykit.demo.moba.view.runtime/Scenes";
        private const string MobaRootPath = MobaPrefabDirectory + "/MobaDemoRoot.prefab";
        private const string MobaBootstrapPath = MobaPrefabDirectory + "/MobaGameplayBootstrap.prefab";
        private const string MobaCatalogPath = MobaProfileDirectory + "/MobaGameplayCatalog.asset";
        public const string MobaScenePath = MobaSceneDirectory + "/" + DemoSceneRoutes.Moba + ".unity";

        private const string ShooterCompositionRoot =
            "Packages/com.abilitykit.demo.shooter.view.runtime/Composition";
        private const string ShooterPrefabDirectory = ShooterCompositionRoot + "/Prefabs";
        private const string ShooterProfileDirectory = ShooterCompositionRoot + "/Profiles";
        private const string ShooterSceneDirectory =
            "Packages/com.abilitykit.demo.shooter.view.runtime/Scenes";
        private const string ShooterLocalRootPath = ShooterPrefabDirectory + "/ShooterLocalDemoRoot.prefab";
        private const string ShooterMultiplayerRootPath = ShooterPrefabDirectory + "/ShooterMultiplayerDemoRoot.prefab";
        private const string ShooterBootstrapPath = ShooterPrefabDirectory + "/ShooterGameplayBootstrap.prefab";
        private const string ShooterCatalogPath = ShooterProfileDirectory + "/ShooterGameplayCatalog.asset";
        public const string ShooterScenePath = ShooterSceneDirectory + "/" + DemoSceneRoutes.Shooter + ".unity";

        private static readonly ProfileDefinition[] MobaProfileDefinitions =
        {
            new ProfileDefinition(
                "moba-local",
                DemoGameplayId.Moba,
                DemoLaunchMode.Local,
                MobaRootPath,
                MobaProfileDirectory + "/MobaLocalProfile.asset"),
            new ProfileDefinition(
                "moba-multiplayer",
                DemoGameplayId.Moba,
                DemoLaunchMode.Multiplayer,
                MobaRootPath,
                MobaProfileDirectory + "/MobaMultiplayerProfile.asset")
        };

        private static readonly ProfileDefinition[] ShooterProfileDefinitions =
        {
            new ProfileDefinition(
                "shooter-local",
                DemoGameplayId.Shooter,
                DemoLaunchMode.Local,
                ShooterLocalRootPath,
                ShooterProfileDirectory + "/ShooterLocalProfile.asset"),
            new ProfileDefinition(
                "shooter-multiplayer",
                DemoGameplayId.Shooter,
                DemoLaunchMode.Multiplayer,
                ShooterMultiplayerRootPath,
                ShooterProfileDirectory + "/ShooterMultiplayerProfile.asset")
        };

        [MenuItem(MenuPath, priority = 20)]
        public static void GenerateAll()
        {
            EnsureFolder(MobaPrefabDirectory);
            EnsureFolder(MobaProfileDirectory);
            EnsureFolder(MobaSceneDirectory);
            EnsureFolder(ShooterPrefabDirectory);
            EnsureFolder(ShooterProfileDirectory);
            EnsureFolder(ShooterSceneDirectory);

            MigrateLegacyAssets();

            var roots = new Dictionary<string, GameObject>(StringComparer.Ordinal)
            {
                [MobaRootPath] = RequireRootPrefab(MobaRootPath),
                [ShooterLocalRootPath] = RequireRootPrefab(ShooterLocalRootPath),
                [ShooterMultiplayerRootPath] = RequireRootPrefab(ShooterMultiplayerRootPath)
            };

            var mobaProfiles = CreateOrUpdateProfiles(MobaProfileDefinitions, roots);
            var mobaCatalog = CreateOrUpdateCatalog(MobaCatalogPath, mobaProfiles);
            var mobaBootstrap = CreateOrUpdateBootstrapPrefab(
                MobaBootstrapPath,
                "MobaGameplayBootstrap",
                mobaCatalog);
            CreateOrUpdateGameplayScene(MobaScenePath, "MobaGameplayBootstrap", mobaBootstrap);

            var shooterProfiles = CreateOrUpdateProfiles(ShooterProfileDefinitions, roots);
            var shooterCatalog = CreateOrUpdateCatalog(ShooterCatalogPath, shooterProfiles);
            var shooterBootstrap = CreateOrUpdateBootstrapPrefab(
                ShooterBootstrapPath,
                "ShooterGameplayBootstrap",
                shooterCatalog);
            CreateOrUpdateGameplayScene(
                ShooterScenePath,
                "ShooterGameplayBootstrap",
                shooterBootstrap);

            UpdateStarterConfiguration();
            ConfigureBuildSettings();
            DeleteLegacyComposition();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[DemoGameplayCompositionBuilder] Package-owned MOBA and Shooter compositions generated successfully.");
        }

        private static void MigrateLegacyAssets()
        {
            MoveAssetIfRequired(LegacyPrefabDirectory + "/MobaDemoRoot.prefab", MobaRootPath);
            MoveAssetIfRequired(
                LegacyPrefabDirectory + "/ShooterLocalDemoRoot.prefab",
                ShooterLocalRootPath);
            MoveAssetIfRequired(
                LegacyPrefabDirectory + "/ShooterMultiplayerDemoRoot.prefab",
                ShooterMultiplayerRootPath);
            MoveAssetIfRequired(
                LegacyProfileDirectory + "/MobaLocalProfile.asset",
                MobaProfileDefinitions[0].ProfilePath);
            MoveAssetIfRequired(
                LegacyProfileDirectory + "/MobaMultiplayerProfile.asset",
                MobaProfileDefinitions[1].ProfilePath);
            MoveAssetIfRequired(
                LegacyProfileDirectory + "/ShooterLocalProfile.asset",
                ShooterProfileDefinitions[0].ProfilePath);
            MoveAssetIfRequired(
                LegacyProfileDirectory + "/ShooterMultiplayerProfile.asset",
                ShooterProfileDefinitions[1].ProfilePath);
        }

        private static void MoveAssetIfRequired(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                return;
            }

            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(
                    $"Failed to move demo asset from '{sourcePath}' to '{destinationPath}': {error}");
            }
        }

        private static GameObject RequireRootPrefab(string prefabPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                   ?? throw new FileNotFoundException(
                       $"Required gameplay root prefab '{prefabPath}' is missing.",
                       prefabPath);
        }

        private static List<DemoGameplayProfileSO> CreateOrUpdateProfiles(
            IReadOnlyList<ProfileDefinition> definitions,
            IReadOnlyDictionary<string, GameObject> roots)
        {
            var profiles = new List<DemoGameplayProfileSO>(definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var profile = LoadOrCreateAsset<DemoGameplayProfileSO>(definition.ProfilePath);
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

        private static DemoGameplayCatalogSO CreateOrUpdateCatalog(
            string catalogPath,
            IReadOnlyList<DemoGameplayProfileSO> profiles)
        {
            var catalog = LoadOrCreateAsset<DemoGameplayCatalogSO>(catalogPath);
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

        private static GameObject CreateOrUpdateBootstrapPrefab(
            string bootstrapPath,
            string rootName,
            DemoGameplayCatalogSO catalog)
        {
            var root = new GameObject(rootName);
            try
            {
                var bootstrap = root.AddComponent<DemoGameplayBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.FindProperty("starterSceneName").stringValue = DemoSceneRoutes.Starter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, bootstrapPath)
                       ?? throw new InvalidOperationException(
                           $"Failed to save gameplay bootstrap prefab '{bootstrapPath}'.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateGameplayScene(
            string scenePath,
            string rootName,
            GameObject bootstrapPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = PrefabUtility.InstantiatePrefab(bootstrapPrefab, scene) as GameObject
                           ?? throw new InvalidOperationException(
                               $"Failed to instantiate gameplay bootstrap prefab '{bootstrapPrefab.name}'.");
            instance.name = rootName;
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException($"Failed to save gameplay scene '{scenePath}'.");
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
                            "AbilityKit.Starter.StarterController",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var controller = new SerializedObject(behaviour);
                    var config = controller.FindProperty("config").objectReferenceValue as ScriptableObject
                                 ?? throw new InvalidOperationException(
                                     "StarterController has no assigned starter config.");
                    var serializedConfig = new SerializedObject(config);
                    serializedConfig.FindProperty("mobaSceneName").stringValue = DemoSceneRoutes.Moba;
                    serializedConfig.FindProperty("shooterSceneName").stringValue = DemoSceneRoutes.Shooter;
                    serializedConfig.FindProperty("mobaProfileId").stringValue = "moba-multiplayer";
                    serializedConfig.FindProperty("shooterProfileId").stringValue = "shooter-multiplayer";
                    serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);
                    return;
                }
            }

            throw new InvalidOperationException(
                "Starter scene has no StarterController.");
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StarterScenePath, enabled: true),
                new EditorBuildSettingsScene(MobaScenePath, enabled: true),
                new EditorBuildSettingsScene(ShooterScenePath, enabled: true)
            };
        }

        private static void DeleteLegacyComposition()
        {
            DeleteAssetIfPresent(LegacyGameplayScenePath);
            DeleteAssetIfPresent(LegacyPrefabDirectory + "/DemoGameplayBootstrap.prefab");
            DeleteAssetIfPresent(LegacyProfileDirectory + "/DemoGameplayCatalog.asset");
            DeleteAssetIfPresent(LegacyCompositionRoot);
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
            {
                if (!AssetDatabase.DeleteAsset(path))
                {
                    throw new InvalidOperationException($"Failed to delete legacy demo asset '{path}'.");
                }
            }
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

        private readonly struct ProfileDefinition
        {
            public ProfileDefinition(
                string profileId,
                DemoGameplayId gameplay,
                DemoLaunchMode mode,
                string rootPath,
                string profilePath)
            {
                ProfileId = profileId;
                Gameplay = gameplay;
                Mode = mode;
                RootPath = rootPath;
                ProfilePath = profilePath;
            }

            public string ProfileId { get; }
            public DemoGameplayId Gameplay { get; }
            public DemoLaunchMode Mode { get; }
            public string RootPath { get; }
            public string ProfilePath { get; }
        }
    }
}
