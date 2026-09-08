#nullable enable

using System;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.Deterministic;
using UnityEditor;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 编辑器内无头预览会话：把一个 authoring 文档编译为 <see cref="TreeRuntime"/>，
    /// 用固定逻辑步长在 <see cref="EditorApplication.update"/> 上推进，并把实例注册进
    /// <see cref="DebugRegistry"/>，供观察窗口着色。预览是纯观察者，不写回资产；
    /// 时钟由会话注入（固定 60Hz 逻辑帧），与真实帧率解耦，保证确定性。
    /// </summary>
    public sealed class TreePreviewSession : IDisposable
    {
        private const long TicksPerSecond = 60;

        private readonly TreeRuntime _runtime;
        private int _frame;

        private TreePreviewSession(TreeRuntime runtime)
        {
            _runtime = runtime;
        }

        public TreeRuntime Runtime => _runtime;
        public int Frame => _frame;

        /// <summary>
        /// 编译文档并启动预览。失败时返回 false 并给出错误（校验错误或运行时异常），
        /// 不注册任何实例、不订阅编辑器更新。
        /// </summary>
        public static bool TryStart(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            string debugName,
            out TreePreviewSession? session,
            out string? error)
        {
            session = null;
            error = null;
            if (document == null || document.Tree == null)
            {
                error = "预览失败：文档为空。";
                return false;
            }

            var definition = TreeExporter.ToRuntimeDefinition(document);
            var errors = TreeValidator.Validate(definition, registry);
            if (errors.Count > 0)
            {
                error = string.Join("\n", errors);
                return false;
            }

            TreeRuntime runtime;
            try
            {
                runtime = TreeRuntime.Create(
                    definition,
                    registry,
                    options: new TreeRunOptions
                    {
                        Seed = 0x12345678UL,
                        // 预览需要自循环，否则树完成后画布不再变化。
                        RestartWhenComplete = true,
                        DebugName = debugName,
                        DebugOwnerLabel = "Editor Preview",
                    });
                runtime.Enable(0, Fixed64.Zero);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            session = new TreePreviewSession(runtime);
            EditorApplication.update += session.Tick;
            return true;
        }

        private void Tick()
        {
            _frame++;
            _runtime.Update(_frame, Fixed64.FromRatio(_frame, TicksPerSecond));
        }

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            try { _runtime.Disable(); } catch (Exception) { /* 观察端不因停止异常泄漏 */ }
            _runtime.Dispose();
        }
    }
}
