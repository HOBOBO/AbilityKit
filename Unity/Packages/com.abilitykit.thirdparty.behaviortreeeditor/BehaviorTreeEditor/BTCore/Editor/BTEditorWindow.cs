using System;
using System.IO;
using BTCore.Runtime;
using BTCore.Runtime.Blackboards;
using BTCore.Runtime.Unity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class BTEditorWindow : EditorWindow
    {
        public static BTEditorWindow Instance;
        private BTView _btView { get; set; }
        private BehaviorTreeConfigObject _behaviorTreeConfigObject;
        
        private NodeInspectorView _nodeInspectorView;
        private ToolbarMenu _toolbarMenu;
        private BlackboardView _blackboardView;

        private Button _undoButton;
        private Button _redoButton;
        private Button _saveButton;

        private BTUndoRedo _undoRedo;
        
        public Blackboard Blackboard => _blackboardView.ExportData();
        public BTView BTView => _btView;

        private BTree _preBTree;
        
        [MenuItem("Tools/BehaviorTree/BTEditorWindow")]
        public static void OpenWindow()
        {
            var wnd = GetWindow<BTEditorWindow>();
            wnd.titleContent = new GUIContent("BTEditorWindow");
            wnd.minSize = new Vector2(800, 600);
        }

        public void CreateGUI() {
            Instance = this;

            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;
        
            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BTEditorDef.BTEditorWindowUxmlPath);
            visualTree.CloneTree(root);

            _btView = root.Q<BTView>();
            _nodeInspectorView = root.Q<NodeInspectorView>();
            _toolbarMenu = root.Q<ToolbarMenu>();
            _blackboardView = root.Q<BlackboardView>();
            _undoButton = root.Q<Button>("undo-button");
            _redoButton = root.Q<Button>("redo-button");
            _saveButton = root.Q<Button>("save-button");

            _undoButton.clicked -= OnUndo;
            _undoButton.clicked += OnUndo;
            _undoButton.SetEnabled(false);
            
            _redoButton.clicked -= OnRedo;
            _redoButton.clicked += OnRedo;
            _redoButton.SetEnabled(false);
            
            _saveButton.clicked -= OnSave;
            _saveButton.clicked += OnSave;
            
            _toolbarMenu.RegisterCallback<MouseEnterEvent>(OnEnterToolbarMenu);
            
            _blackboardView.OnValueListChanged = OnBlackboardValueChanged;
            _btView.OnNodeSelected = OnNodeSelected;
            
            _undoRedo = new BTUndoRedo();
            
            OnSelectionChange();
        }

        private void OnUndo() {
            _undoRedo.Undo();
            UpdateUndoRedoButtonState();
        }

        private void OnRedo() {
            _undoRedo.Redo();
            UpdateUndoRedoButtonState();
        }

        private void OnSave()
        {
            if (_behaviorTreeConfigObject != null)
            {
                _behaviorTreeConfigObject.SaveSource(new BTree {
                    BTData = _btView.ExportData(),
                    Blackboard = Blackboard
                });
            }
        }

        public void AddCommand(ICommand command) {
            _undoRedo.AddCommand(command);
            UpdateUndoRedoButtonState();
        }

        private void UpdateUndoRedoButtonState() {
            _undoButton.SetEnabled(_undoRedo.CanUndo);
            _redoButton.SetEnabled(_undoRedo.CanRedo);
        }
        
        private void OnBlackboardValueChanged() {
            _nodeInspectorView.UpdateNodeBindValues();
        }

        private void OnNodeSelected(BTNodeView nodeView) {
            _nodeInspectorView.UpdateSelection(nodeView);
        }

        public void ClearNodeSelectedInspector() {
            _nodeInspectorView.ClearSelection();
        }

        private void OnEnterToolbarMenu(MouseEnterEvent evt) {
            _toolbarMenu.menu.MenuItems().Clear();
            foreach (var filePath in Directory.GetFiles(BTEditorDef.DataDir, "*.json")) {
                var fileName = Path.GetFileName(filePath);
                _toolbarMenu.menu.AppendAction($"{fileName}", _ => {
                    var btData = (BTree) null;
                    try {
                        var json = File.ReadAllText(filePath);
                        btData = JsonConvert.DeserializeObject<BTree>(json, BTDef.SerializerSettingsAuto);
                    }
                    catch (Exception ex) {
                        Debug.LogError($"反序列BT数据失败，path: {filePath} ex: {ex}");
                        return;
                    }
                    
                    SelectNewTree(btData);
                });
            }
            
            _toolbarMenu.menu.AppendSeparator();
            
            // 保存当前行为树配置数据
            _toolbarMenu.menu.AppendAction("Save", _ => {
                var treeNodeData = _btView.ExportData();
                
                // 入口子节点为空
                if (string.IsNullOrEmpty(treeNodeData.EntryNode.ChildGuid)) {
                    ShowNotification("BT入口不能为空");
                    return;
                }
                
                var path = EditorUtility.SaveFilePanel("另存为", BTEditorDef.DataDir, BTEditorDef.DefaultFileName,
                    BTEditorDef.DataExt);
                if (string.IsNullOrEmpty(path)) {
                    return;
                }

                try {
                    var btData = new BTree {
                        BTData = _btView.ExportData(),
                        Blackboard = Blackboard
                    };
                    var json = JsonConvert.SerializeObject(btData, BTDef.SerializerSettingsAll);
                    File.WriteAllText(path, json);
                    AssetDatabase.Refresh();
                }
                catch (Exception e) {
                    ShowNotification("BT数据保存失败");
                    Debug.LogError($"BT树数据序列化失败，ex: {e}");
                    return;
                }
                
                ShowNotification("BT数据保存成功");
                Debug.Log($"BT数据保存成功，路径：{path}");
            });
        }

        private void OnInspectorUpdate() {
            if (EditorApplication.isPlaying) {
                _btView.UpdateNodesStyle();
            }
        }

        private void OnEnable() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable() {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange stateChange) {
            if (stateChange == PlayModeStateChange.EnteredEditMode) {
                OnSelectionChange();
            }
        }

        private void OnSelectionChange() {
            if (_btView == null) {
                return;
            }

            var btData = (BTree) null;
            var behaviorTree = (BehaviorTree) null;
            
            // 1. 优先判断是否选中运行时的BT
            if (Selection.activeGameObject != null &&
                (behaviorTree = Selection.activeGameObject.GetComponent<BehaviorTree>())) {
                if (EditorApplication.isPlaying) {
                    btData = behaviorTree.BTree;
                }
                else {
                    behaviorTree.CreateBTree();
                    btData = behaviorTree.BTree;
                }
            }
            
            // 2. 再判断选中的资源可以反序列化为BT数据
            if (btData == null && Selection.activeObject != null) {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!path.StartsWith(BTEditorDef.DataDir) || !path.EndsWith(BTEditorDef.DataExt)) {
                    //SelectNewTree(new BTree());
                    return;
                }
            
                try {
                    var json = File.ReadAllText(path);
                    btData = JsonConvert.DeserializeObject<BTree>(json, BTDef.SerializerSettingsAuto);
                }
                catch (Exception e) {
                    ShowNotification("导入BT数据失败");
                    Debug.LogError($"导入BT数据失败，path: {path} ex: {e}");
                }
            }
            
            SelectNewTree(btData ?? new BTree());
        }

        public void ShowNotification(string message, double fadeoutWait = 4.0) {
            ShowNotification(new GUIContent(message), fadeoutWait);
        }
        
        public void SelectNewTree(BTree bTree,BehaviorTreeConfigObject configObject = null) {
            if (_btView == null) {
                return;
            }

            if (_preBTree == bTree) {
                return;
            }
            _preBTree = bTree;
            bTree?.RebuildTree();
            if (configObject != null)
            {
                _behaviorTreeConfigObject = configObject;
            }
            
            // 每次导入新的BT数据时，需要清空undo、redo保存数据
            if (_undoRedo != null) {
                _undoRedo.Clear();
                UpdateUndoRedoButtonState();
            }

            _btView.ImportData(bTree.BTData);
            _blackboardView.ImportData(bTree.Blackboard);
        }
    }
}