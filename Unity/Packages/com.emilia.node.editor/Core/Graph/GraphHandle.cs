using System;
using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Node.Attributes;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphHandle
    {
        /// <summary>
        /// 初始化时
        /// </summary>
        public virtual void Initialize(EditorGraphView graphView) { }

        /// <summary>
        /// 初始化自定义模块
        /// </summary>
        public virtual void InitializeCustomModule(EditorGraphView graphView, Dictionary<Type, CustomGraphViewModule> modules) { }

        /// <summary>
        /// 所有模块初始化成功
        /// </summary>
        public virtual void AllModuleInitializeSuccess(EditorGraphView graphView) { }

        /// <summary>
        /// 加载前处理
        /// </summary>
        public virtual void OnLoadBefore(EditorGraphView graphView) { }

        /// <summary>
        /// 加载后处理
        /// </summary>
        public virtual void OnLoadAfter(EditorGraphView graphView) { }

        /// <summary>
        /// 进入聚焦时
        /// </summary>
        public virtual void OnEnterFocus(EditorGraphView graphView) { }

        /// <summary>
        /// 聚焦持续时
        /// </summary>
        public virtual void OnFocus(EditorGraphView graphView) { }

        /// <summary>
        /// 退出聚焦时
        /// </summary>
        public virtual void OnExitFocus(EditorGraphView graphView) { }

        /// <summary>
        /// 更新
        /// </summary>
        public virtual void OnUpdate(EditorGraphView graphView) { }

        /// <summary>
        /// 销毁时
        /// </summary>
        /// <param name="graphView"></param>
        public virtual void Dispose(EditorGraphView graphView) { }

        protected void AddModule<TModule>(Dictionary<Type, CustomGraphViewModule> modules) where TModule : CustomGraphViewModule, new()
        {
            modules.Add(typeof(TModule), new TModule());
        }
    }

    [EditorHandle(typeof(EditorGraphAsset))]
    public class BasicGraphHandle : GraphHandle
    {
        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            SyncSetting(graphView);
        }

        protected virtual void SyncSetting(EditorGraphView graphView)
        {
            GraphSettingStruct? graphSetting = graphView.GetGraphData<BasicGraphData>()?.graphSetting;
            if (graphSetting == null) return;

            graphView.maxLoadTimeMs = graphSetting.Value.maxLoadTimeMs;
            graphView.SetupZoom(graphSetting.Value.zoomSize.x, graphSetting.Value.zoomSize.y);
            if (graphSetting.Value.immediatelySave == false) graphView.graphSave.ResetCopy(graphView, graphView.graphAsset);
        }
    }
}