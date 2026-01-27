using System;
using Emilia.Node.Attributes;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// ImGUI控制器
    /// </summary>
    [Serializable]
    public class EditorGraphImGUIRoot
    {
        [SerializeField]
        private EditorWindow _window;

        [NonSerialized, OdinSerialize]
        private EditorGraphAsset _asset;

        [NonSerialized, OdinSerialize]
        private GraphSettingStruct? settingStruct;

        private EditorGraphViewDrawer _drawer;

        private GUIStyle tipsStyle;

        /// <summary>
        /// 窗口
        /// </summary>
        public EditorWindow window => this._window;

        /// <summary>
        /// 资产
        /// </summary>
        public EditorGraphAsset asset => this._asset;

        public EditorGraphView graphView { get; private set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EditorWindow window)
        {
            this._window = window;
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
        public void SetSetting(GraphSettingStruct settingStruct)
        {
            this.settingStruct = settingStruct;
        }

        /// <summary>
        /// ImGUI绘制
        /// </summary>
        public void OnImGUI(float height, float width = -1)
        {
            if (this._asset != null && this._drawer == null)
            {
                graphView = new EditorGraphView();
                graphView.window = window;
                graphView.Initialize();

                GraphSettingStruct? loadSetting = settingStruct;
                if (settingStruct == null) loadSetting = asset.GetType().GetCustomAttribute<GraphSettingAttribute>()?.settingStruct;

                graphView.Reload(asset);
                if (loadSetting != null) graphView.GetGraphData<BasicGraphData>().graphSetting = loadSetting.Value;

                this._drawer = new EditorGraphViewDrawer();
                this._drawer.Initialize(graphView);

                EditorApplication.update -= Update;
                EditorApplication.update += Update;

                graphView.onGraphAssetChange -= OnGraphAssetChange;
                graphView.onGraphAssetChange += OnGraphAssetChange;
            }

            if (this._drawer != null && asset != null) this._drawer.Draw(height, width);
            else
            {
                InitTipsStyle();
                GUILayout.Label("当前编辑的对象为空", tipsStyle, GUILayout.Height(height));
            }
        }

        private void OnGraphAssetChange(EditorGraphAsset graphAsset)
        {
            _asset = graphAsset;
        }

        private void InitTipsStyle()
        {
            if (this.tipsStyle != null) return;
            tipsStyle = new GUIStyle(GUI.skin.label);
            tipsStyle.alignment = TextAnchor.MiddleCenter;
            tipsStyle.fontSize = 20;
        }

        /// <summary>
        /// 更新
        /// </summary>
        public void Update()
        {
            if (this._asset == null) Reload();
            graphView?.OnUpdate();
        }

        /// <summary>
        /// 重新加载
        /// </summary>
        public void Reload()
        {
            graphView?.Dispose();
            graphView = null;

            this._drawer?.Dispose();
            this._drawer = null;
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            graphView?.Dispose();
            graphView = null;

            this._drawer?.Dispose();
            this._drawer = null;

            EditorApplication.update -= Update;
        }
    }
}