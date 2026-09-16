using System;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    public static class BehaviorTreeShowcaseInstaller
    {
        private const string Root = "Assets/BehaviorTreeShowcase";
        private const string Trees = Root + "/Trees";
        private const string Runtime = Root + "/Runtime";
        private const string ScenePath = Root + "/BehaviorTreeShowcase.unity";

        [MenuItem("AbilityKit/行为树/示例/创建可运行展示场景")]
        public static void CreateShowcase()
        {
            var sourcePath = RuntimeObservationSampleInstaller.FindImportedSampleJsonPath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                EditorUtility.DisplayDialog("行为树示例", "请先在 Package Manager 导入完整运行观察示例。", "确定");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var source = AuthoringJson.Load(File.ReadAllText(
                    RuntimeObservationSampleInstaller.ToAbsolutePath(sourcePath)));
                var patrol = EnsureTree("PatrolLoop", BehaviorTreeShowcaseDocuments.BuildPatrol(source));
                var chase = EnsureTree("TargetChase", BehaviorTreeShowcaseDocuments.BuildChase(source));
                var decision = EnsureTree("PriorityDecision", source);
                var patrolJson = Export(patrol);
                var chaseJson = Export(chase);
                var decisionJson = Export(decision);

                if (File.Exists(Path.GetFullPath(ScenePath)))
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    Selection.activeObject = decision;
                    return;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateStage();
                CreateAgent("PATROL", "巡逻循环", -4.5f, patrol, patrolJson);
                CreateAgent("CHASE", "目标追踪", 0f, chase, chaseJson);
                CreateAgent("DECISION", "优先级决策", 4.5f, decision, decisionJson);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("示例场景保存失败：" + ScenePath);
                Selection.activeObject = decision;
                EditorGUIUtility.PingObject(decision);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("行为树示例创建失败", exception.Message, "确定");
            }
        }

        private static AuthoringAsset EnsureTree(string name, AuthoringSourceDocument document)
        {
            var path = Trees + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(path);
            if (existing != null) return existing;
            EnsureFolder(Trees);
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.name = name;
            asset.SaveDocument(document);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_runtimeExportPath").stringValue = Runtime;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static TextAsset Export(AuthoringAsset asset)
        {
            EnsureFolder(Runtime);
            var report = AuthoringRuntimeExporter.Export(asset);
            if (!report.Success)
                throw new InvalidOperationException(asset.name + "：" + string.Join("\n", report.Messages));
            AssetDatabase.Refresh();
            var path = asset.ResolveRuntimeExportPath(asset.LoadDocument().Tree.TreeId);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                ?? throw new IOException("运行时 JSON 无法作为 TextAsset 加载：" + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void CreateStage()
        {
            var cameraObject = new GameObject("示例相机");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 6.5f, -14f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.19f);
            camera.fieldOfView = 45f;
            cameraObject.tag = "MainCamera";

            var lightObject = new GameObject("主光源");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "行为树示例地面";
            floor.transform.position = new Vector3(0f, -1.02f, 0f);
            floor.transform.localScale = new Vector3(1.4f, 1f, 0.7f);
        }

        private static void CreateAgent(string caption, string name, float x,
            AuthoringAsset asset, TextAsset runtimeJson)
        {
            var agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agent.name = name;
            agent.transform.position = new Vector3(x, 0f, 0f);
            var labelObject = new GameObject("当前行为");
            labelObject.transform.SetParent(agent.transform, false);
            labelObject.transform.localPosition = new Vector3(-0.92f, 1.55f, 0f);
            labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = caption;
            label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 40;
            label.characterSize = 0.075f;
            label.color = Color.white;

            var sample = agent.AddComponent<RuntimeObservationSample>();
            var serialized = new SerializedObject(sample);
            serialized.FindProperty("_runtimeJson").objectReferenceValue = runtimeJson;
            serialized.FindProperty("_authoringAsset").objectReferenceValue = asset;
            serialized.FindProperty("_agentRenderer").objectReferenceValue = agent.GetComponent<Renderer>();
            serialized.FindProperty("_stateLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
