using System;
using System.IO;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.BattleScenario.Editor
{
    [CustomEditor(typeof(BattleScenarioRoot))]
    public sealed class BattleScenarioRootEditor : UnityEditor.Editor
    {
        private BattleScenarioRoot Root => (BattleScenarioRoot)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("environmentProfileId"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新增战斗实体")) AddActor();
                if (GUILayout.Button("新增障碍")) AddObstacle();
            }

            var actors = Root.GetComponentsInChildren<BattleScenarioActor>(true);
            var obstacles = Root.GetComponentsInChildren<BattleScenarioObstacle>(true);
            EditorGUILayout.LabelField($"战斗实体  {actors.Length}    障碍  {obstacles.Length}", EditorStyles.miniBoldLabel);

            foreach (var actor in actors)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{actor.Alias}  ·  Team {actor.TeamId}");
                    if (GUILayout.Button("选中", GUILayout.Width(48))) Selection.activeGameObject = actor.gameObject;
                }
            }

            foreach (var obstacle in obstacles)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{obstacle.ObstacleId}  ·  {obstacle.Shape}");
                    if (GUILayout.Button("选中", GUILayout.Width(48))) Selection.activeGameObject = obstacle.gameObject;
                }
            }

            var errors = Root.ValidateAuthoring();
            if (errors.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            else
                EditorGUILayout.HelpBox("实体与障碍的位置由子对象 Transform 决定；保存时转换为 .battlescene。", MessageType.Info);

            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                if (GUILayout.Button("导出战斗场景", GUILayout.Height(28f))) ExportScene();
            }
        }

        private void OnSceneGUI()
        {
            var root = Root.transform;
            foreach (var actor in Root.GetComponentsInChildren<BattleScenarioActor>(true))
            {
                if (actor.transform == root) continue;
                var position = actor.transform.position;
                Handles.color = actor.TeamId == 1 ? new Color(0.25f, 0.65f, 1f) : new Color(1f, 0.35f, 0.3f);
                Handles.SphereHandleCap(0, position, Quaternion.identity, 0.35f, EventType.Repaint);
                Handles.Label(position + Vector3.up * 0.45f, actor.Alias);

                EditorGUI.BeginChangeCheck();
                var next = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(actor.transform, "移动战斗实体");
                    actor.transform.position = next;
                    EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
                }
            }

            foreach (var obstacle in Root.GetComponentsInChildren<BattleScenarioObstacle>(true))
            {
                if (obstacle.transform == root) continue;
                var position = obstacle.transform.position;
                var size = obstacle.Size;
                Handles.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                using (new Handles.DrawingScope(Matrix4x4.TRS(position, obstacle.transform.rotation, size)))
                {
                    Handles.DrawWireCube(Vector3.zero, Vector3.one);
                }
                Handles.Label(position + Vector3.up * (size.y * 0.5f + 0.1f), obstacle.ObstacleId);
            }
        }

        private void AddActor()
        {
            var child = new GameObject(NextActorName());
            Undo.RegisterCreatedObjectUndo(child, "新增战斗实体");
            Undo.SetTransformParent(child.transform, Root.transform, "挂载战斗实体");
            child.transform.localPosition = Vector3.zero;
            var actor = child.AddComponent<BattleScenarioActor>();
            actor.Alias = child.name.ToLowerInvariant();
            EditorUtility.SetDirty(actor);
            Selection.activeGameObject = child;
            EditorSceneManager.MarkSceneDirty(Root.gameObject.scene);
        }

        private string NextActorName()
        {
            var index = Root.GetComponentsInChildren<BattleScenarioActor>(true).Length + 1;
            return "Actor_" + index;
        }

        private void AddObstacle()
        {
            var child = new GameObject("Obstacle_" + (Root.GetComponentsInChildren<BattleScenarioObstacle>(true).Length + 1));
            Undo.RegisterCreatedObjectUndo(child, "新增战斗障碍");
            Undo.SetTransformParent(child.transform, Root.transform, "挂载战斗障碍");
            child.transform.localPosition = Vector3.zero;
            var obstacle = child.AddComponent<BattleScenarioObstacle>();
            obstacle.ObstacleId = child.name.ToLowerInvariant();
            EditorUtility.SetDirty(obstacle);
            Selection.activeGameObject = child;
            EditorSceneManager.MarkSceneDirty(Root.gameObject.scene);
        }

        private void ExportScene()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "导出战斗场景",
                string.IsNullOrWhiteSpace(Root.SceneId) ? Root.name : Root.SceneId,
                "battlescene",
                "选择 .battlescene 输出位置");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var document = Root.ToSceneDocument();
                BattleScenarioDocumentValidator.ThrowIfInvalid(document);
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                File.WriteAllText(absolutePath, BattleScenarioCodec.SerializeScene(document));
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("战斗场景", "已导出：" + path, "确定");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("导出失败", exception.Message, "确定");
            }
        }

        [MenuItem("GameObject/AbilityKit/BattleScenario Root", false, 10)]
        private static void CreateRoot(MenuCommand command)
        {
            var rootObject = new GameObject("BattleScenarioRoot");
            Undo.RegisterCreatedObjectUndo(rootObject, "创建 BattleScenario 根节点");
            GameObjectUtility.SetParentAndAlign(rootObject, command.context as GameObject);
            rootObject.AddComponent<BattleScenarioRoot>();
            Selection.activeGameObject = rootObject;
            EditorSceneManager.MarkSceneDirty(rootObject.scene);
        }
    }

    [CustomEditor(typeof(BattleScenarioActor))]
    public sealed class BattleScenarioActorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var actor = (BattleScenarioActor)target;
            var root = actor.GetComponentInParent<BattleScenarioRoot>();
            if (root == null)
                EditorGUILayout.HelpBox("此实体需要位于 BattleScenarioRoot 层级下。", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(BattleScenarioObstacle))]
    public sealed class BattleScenarioObstacleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var obstacle = (BattleScenarioObstacle)target;
            var root = obstacle.GetComponentInParent<BattleScenarioRoot>();
            if (root == null)
                EditorGUILayout.HelpBox("此障碍需要位于 BattleScenarioRoot 层级下。", MessageType.Warning);
        }
    }
}
