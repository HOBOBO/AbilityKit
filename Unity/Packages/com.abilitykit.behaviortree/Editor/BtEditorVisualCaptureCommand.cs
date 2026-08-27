#nullable enable

using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Deterministic;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 启动即打开图编辑器 + 观察窗口（含一棵示例树 + 一个运行实例），供 GUI 模式下
    /// 用 OS 级截屏验证编辑器界面。用法：Unity.exe -projectPath Unity -executeMethod
    /// AbilityKit.BehaviorTree.Editor.BtEditorVisualCaptureCommand.Run
    /// （不要 -batchmode/-quit，让窗口保持打开）。
    /// </summary>
    public static class BtEditorVisualCaptureCommand
    {
        public static void Run()
        {
            // 一棵示例树（反应式骨架）
            var document = BtAuthoringTemplates.BuildReactiveLoop();
            document.Tree.TreeId = "visual_capture";

            var asset = ScriptableObject.CreateInstance<BtAuthoringAsset>();
            asset.name = "VisualCapture";
            asset.SaveDocument(document);

            // 打开图编辑器
            BtAuthoringGraphWindow.Open(asset);

            // 注册一个运行实例（供观察窗口展示）
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            var runtime = BtTreeRuntime.Create(document.Tree, registry, null,
                new BtTreeRunOptions { DebugName = "visual_capture", DebugOwnerLabel = "actor:1" });
            runtime.Enable(0, Fixed64.Zero);
            runtime.Update(1, Fixed64.Zero);

            // 打开观察窗口并聚焦
            var observation = EditorWindow.GetWindow<BtDebugObservationWindow>();
            observation.Show();

            var graph = EditorWindow.GetWindow<BtAuthoringGraphWindow>();
            graph.Focus();

            Debug.Log("[BtEditorVisualCapture] graph + observation windows opened with a sample tree and a running instance.");
        }
    }
}
