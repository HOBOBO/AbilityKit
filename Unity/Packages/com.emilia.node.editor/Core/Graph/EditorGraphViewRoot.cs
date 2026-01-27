using System;
using Emilia.Node.Attributes;
using UnityEditor;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// UIElement控制器
    /// </summary>
    [Serializable]
    public class EditorGraphViewRoot
    {
        private VisualElement _rootVisualElement;
        private EditorWindow _editorWindow;

        private EditorGraphView _graphView;

        private EditorGraphAsset _asset;
        private GraphSettingStruct? settingStruct;

        /// <summary>
        /// 根VisualElement
        /// </summary>
        public VisualElement rootVisualElement => this._rootVisualElement;

        /// <summary>
        /// 目标Window
        /// </summary>
        public EditorWindow editorWindow => this._editorWindow;

        public EditorGraphView graphView => this._graphView;
        public EditorGraphAsset asset => this._asset;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(VisualElement root, EditorWindow editorWindow)
        {
            this._rootVisualElement = root;
            this._editorWindow = editorWindow;

            if (this._graphView != null)
            {
                this._graphView.Dispose();
                this._graphView.RemoveFromHierarchy();
                this._graphView = null;
            }

            this._graphView = new EditorGraphView();
            this._graphView.style.flexGrow = 1;

            this._graphView.window = editorWindow;
            this._graphView.Initialize();

            this._rootVisualElement.Add(this._graphView);

            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private void OnUpdate()
        {
            if (this._graphView == null) return;
            this._graphView.OnUpdate();
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update -= OnUpdate;

            this._graphView.Dispose();
            this._graphView.RemoveFromHierarchy();
            this._graphView = null;
        }

        /// <summary>
        /// 重新加载
        /// </summary>
        public void Reload()
        {
            if (this._graphView == null) return;
            this._graphView.Reload(this._asset);
            BasicGraphData basicGraphData = this._graphView.GetGraphData<BasicGraphData>();
            if (this.settingStruct != null) basicGraphData.graphSetting = this.settingStruct.Value;
        }

        /// <summary>
        /// 设置资源
        /// </summary>
        public void SetAsset(EditorGraphAsset asset)
        {
            this._asset = asset;
            Reload();
        }

        /// <summary>
        /// 改变设置
        /// </summary>
        public void SetSetting(GraphSettingStruct settingStruct, bool isReload = false)
        {
            this.settingStruct = settingStruct;
            if (isReload) Reload();
        }
    }
}