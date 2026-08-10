using System;
using System.IO;
using AbilityKit.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Editor
{
    public static class MobaDemoSceneMenu
    {
        private const int DemoMapId = 1;
        private const string DemoScenePath = "Assets/Scenes/MobaDemoScene.unity";
        private const string MenuRoot = "Tools/AbilityKit/Demos/Moba/";

        [MenuItem(MenuRoot + "Open Demo Scene", priority = 10)]
        private static void OpenDemoScene()
        {
            TryOpenOrCreateDemoScene(saveScene: true, out _);
        }

        [MenuItem(MenuRoot + "Create Or Refresh Demo Scene", priority = 11)]
        private static void CreateOrRefreshDemoScene()
        {
            if (!TryOpenOrCreateDemoScene(saveScene: true, out _))
            {
                return;
            }

            EditorUtility.DisplayDialog("MOBA Demo", $"Demo scene is ready:\n{DemoScenePath}", "OK");
            PingSceneAsset();
        }

        public static void CreateOrRefreshDemoSceneBatch()
        {
            if (!TryOpenOrCreateDemoScene(saveScene: true, out _))
            {
                throw new InvalidOperationException("Unable to create or refresh the MOBA demo scene.");
            }
        }

        [MenuItem(MenuRoot + "Play Demo Scene", priority = 12)]
        private static void PlayDemoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!TryOpenOrCreateDemoScene(saveScene: true, out _))
            {
                return;
            }

            EditorApplication.EnterPlaymode();
        }

        private static bool TryOpenOrCreateDemoScene(bool saveScene, out Scene scene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                scene = SceneManager.GetActiveScene();
                return false;
            }

            EnsureSceneDirectory();

            scene = File.Exists(DemoScenePath)
                ? EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneManager.SetActiveScene(scene);
            EnsureDemoSceneObjects(scene);

            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene, DemoScenePath);
                AssetDatabase.Refresh();
            }

            Selection.activeGameObject = FindGameEntry(scene)?.gameObject;
            return true;
        }

        private static void EnsureSceneDirectory()
        {
            var directory = Path.GetDirectoryName(DemoScenePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureDemoSceneObjects(Scene scene)
        {
            EnsureCamera(scene);
            EnsureDirectionalLight(scene);
            EnsureGameEntry(scene);
            MobaMapSceneGenerator.Refresh(scene, DemoMapId);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Camera EnsureCamera(Scene scene)
        {
            var camera = FindComponentInScene<Camera>(scene);
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                SceneManager.MoveGameObjectToScene(go, scene);
                camera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }

            camera.transform.position = new Vector3(0f, 14f, -18f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            return camera;
        }

        private static Light EnsureDirectionalLight(Scene scene)
        {
            var light = FindComponentInScene<Light>(scene, candidate => candidate.type == LightType.Directional);
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                SceneManager.MoveGameObjectToScene(go, scene);
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1f;
            return light;
        }

        private static GameEntry EnsureGameEntry(Scene scene)
        {
            var entry = FindGameEntry(scene);
            if (entry == null)
            {
                var go = new GameObject("GameEntry");
                SceneManager.MoveGameObjectToScene(go, scene);
                entry = go.AddComponent<GameEntry>();
            }

            entry.name = "GameEntry";
            entry.DebugEnabled = true;
            EditorUtility.SetDirty(entry);
            return entry;
        }

        private static GameEntry FindGameEntry(Scene scene)
        {
            return FindComponentInScene<GameEntry>(scene);
        }

        private static T FindComponentInScene<T>(Scene scene, System.Predicate<T> predicate = null) where T : Component
        {
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<T>(includeInactive: true);
                for (int j = 0; j < components.Length; j++)
                {
                    var component = components[j];
                    if (component == null) continue;
                    if (predicate == null || predicate(component)) return component;
                }
            }

            return null;
        }

        private static void PingSceneAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
            if (asset == null) return;

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
