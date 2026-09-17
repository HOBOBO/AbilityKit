using System;
using System.IO;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase.Editor
{
    public static class HfsmShowcaseInstaller
    {
        private const string Root = "Assets/HfsmShowcase";
        private const string GraphFolder = Root + "/Graphs";
        private const string ActionFolder = Root + "/Actions";
        private const string RuntimeFolder = Root + "/Runtime";
        private const string ScenePath = Root + "/HfsmShowcase.unity";

        [MenuItem("AbilityKit/HFSM/Samples/Create Complete Showcase Scene")]
        public static void CreateShowcase()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var patrol = EnsureGraph("PatrolAndBlock", HfsmShowcaseDocuments.BuildPatrol);
                var combat = EnsureGraph("HierarchicalCombat", HfsmShowcaseDocuments.BuildCombat);
                var actions = EnsureActions();
                var actionJson = ExportActions(actions);
                var patrolJson = Export(patrol, actions);
                var combatJson = Export(combat, actions);

                if (File.Exists(Path.GetFullPath(ScenePath)))
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    Selection.activeObject = combat;
                    return;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateStage();
                CreateAgent("PATROL", -4.5f, patrol, patrolJson, actions, actionJson,
                    moving: false, hasTarget: false, distance: 5f);
                CreateAgent("STRIKE", 0f, combat, combatJson, actions, actionJson,
                    moving: false, hasTarget: true, distance: 1f);
                CreateAgent("CHASE", 4.5f, combat, combatJson, actions, actionJson,
                    moving: false, hasTarget: true, distance: 5f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unable to save HFSM showcase scene: " + ScenePath);
                Selection.activeObject = combat;
                EditorGUIUtility.PingObject(combat);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("HFSM Showcase", ex.Message, "OK");
            }
        }

        public static TextAsset Export(GraphAsset graph, HfsmShowcaseActionAsset actions)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            var sourcePath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException("Save the HFSM graph as an asset before exporting it.");
            var report = DefinitionExporter.Export(graph, HfsmShowcaseDocuments.BuildCatalog());
            if (!report.IsSuccess)
                throw new InvalidOperationException(string.Join("\n", report.Issues));
            var catalog = CompositeActionCatalog.LoadJson(actions.ExportJson());
            foreach (var machine in report.Definition.Machines)
                foreach (var state in machine.States)
                    if (state.BehaviorKey.StartsWith("showcase.patrol.", StringComparison.Ordinal) ||
                        state.BehaviorKey.StartsWith("showcase.combat.", StringComparison.Ordinal))
                        catalog.Get(state.BehaviorKey);
            AssetDatabase.SaveAssetIfDirty(graph);
            EnsureFolder(RuntimeFolder);
            var path = RuntimeFolder + "/" + Path.GetFileNameWithoutExtension(sourcePath) + ".json";
            File.WriteAllText(Path.GetFullPath(path), report.Json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                ?? throw new IOException("Unable to import exported HFSM definition: " + path);
        }

        public static TextAsset ExportActions(HfsmShowcaseActionAsset actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(actions)))
                throw new InvalidOperationException("Save composite actions as an asset before exporting.");
            var json = actions.ExportJson();
            AssetDatabase.SaveAssetIfDirty(actions);
            EnsureFolder(RuntimeFolder);
            var path = RuntimeFolder + "/ShowcaseActions.json";
            File.WriteAllText(Path.GetFullPath(path), json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path)
                ?? throw new IOException("Unable to import composite action JSON: " + path);
        }

        private static HfsmShowcaseActionAsset EnsureActions()
        {
            EnsureFolder(ActionFolder);
            var path = ActionFolder + "/ShowcaseActions.asset";
            var existing = AssetDatabase.LoadAssetAtPath<HfsmShowcaseActionAsset>(path);
            if (existing != null) return existing;
            var seed = Resources.Load<TextAsset>("hfsm_showcase_actions")
                ?? throw new IOException("Import the Complete HFSM Showcase sample first.");
            var asset = ScriptableObject.CreateInstance<HfsmShowcaseActionAsset>();
            asset.LoadJson(seed.text);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static GraphAsset EnsureGraph(string name, Func<GraphAsset> create)
        {
            EnsureFolder(GraphFolder);
            var path = GraphFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
            if (existing != null) return existing;
            var graph = create();
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            return graph;
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
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 6f, -13f);
            cameraObject.transform.LookAt(Vector3.zero);
            cameraObject.tag = "MainCamera";
            camera.backgroundColor = new Color(0.14f, 0.18f, 0.2f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(48f, -20f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Showcase Floor";
            floor.transform.position = new Vector3(0f, -1.02f, 0f);
            floor.transform.localScale = new Vector3(1.5f, 1f, 0.8f);
        }

        private static void CreateAgent(string label, float x, GraphAsset graph, TextAsset json,
            HfsmShowcaseActionAsset actions, TextAsset actionJson, bool moving, bool hasTarget, float distance)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = label;
            go.transform.position = new Vector3(x, 0f, 0f);
            var caption = new GameObject("State Label");
            caption.transform.SetParent(go.transform, false);
            caption.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            caption.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = caption.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 40;
            text.characterSize = 0.075f;
            text.color = Color.white;
            var agent = go.AddComponent<HfsmShowcaseAgent>();
            var serialized = new SerializedObject(agent);
            serialized.FindProperty("_sourceGraph").objectReferenceValue = graph;
            serialized.FindProperty("_runtimeJson").objectReferenceValue = json;
            serialized.FindProperty("_sourceActions").objectReferenceValue = actions;
            serialized.FindProperty("_actionJson").objectReferenceValue = actionJson;
            serialized.FindProperty("_agentRenderer").objectReferenceValue = go.GetComponent<Renderer>();
            serialized.FindProperty("_stateLabel").objectReferenceValue = text;
            serialized.FindProperty("_moving").boolValue = moving;
            serialized.FindProperty("_hasTarget").boolValue = hasTarget;
            serialized.FindProperty("_targetDistance").floatValue = distance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
