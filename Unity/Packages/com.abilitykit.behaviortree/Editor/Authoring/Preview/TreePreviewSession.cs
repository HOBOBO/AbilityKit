#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.Deterministic;
using UnityEditor;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

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
        private readonly TreeRuntimeSnapshot _initialState;
        private int _frame;
        private bool _disposed;
        private bool _updateSubscribed;
        private bool _paused;

        private TreePreviewSession(TreeRuntime runtime, TreeRuntimeSnapshot initialState)
        {
            _runtime = runtime;
            _initialState = initialState;
            InitialBlackboard = ObservationBlackboard.Copy(initialState.Blackboard
                ?? throw new InvalidOperationException("预览初始快照没有黑板数据。"));
        }

        public TreeRuntime Runtime => _runtime;
        public ObservationBlackboard InitialBlackboard { get; }
        public int Frame => _frame;
        public bool IsPaused => _paused;
        public bool IsFaulted { get; private set; }
        public string? FaultMessage { get; private set; }
        public event Action<string>? Faulted;

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
            => TryStart(document, registry, null, debugName, out session, out error);

        public static bool TryStart(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            TreeDefinitionResolver? subtreeResolver,
            string debugName,
            out TreePreviewSession? session,
            out string? error)
            => TryStart(document, registry, subtreeResolver, debugName, null, out session, out error);

        public static bool TryStart(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            TreeDefinitionResolver? subtreeResolver,
            string debugName,
            IReadOnlyDictionary<string, PropertyValue>? initialOverrides,
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

            TreeRuntime? runtime = null;
            TreeRuntimeSnapshot? initialState = null;
            try
            {
                var build = BehaviorTreeBuildPipeline.Build(document, registry, subtreeResolver);
                if (!build.Success || build.CompiledDefinition == null)
                    throw new InvalidOperationException(string.Join(
                        "\n",
                        build.Diagnostics.Select(diagnostic =>
                            $"[{diagnostic.Code}] {diagnostic.Message}")));

                var options = new TreeRunOptions
                {
                    Seed = 0x12345678UL,
                    // 预览需要自循环，否则树完成后画布不再变化。
                    RestartWhenComplete = true,
                    DebugName = debugName,
                    DebugOwnerLabel = "Editor Preview",
                };
                runtime = build.Expansion == null
                    ? TreeRuntime.Create(build.CompiledDefinition, registry, options: options)
                    : TreeRuntime.Create(build.Expansion, registry, options: options);
                ApplyInitialOverrides(runtime, initialOverrides);
                runtime.Enable(0, Fixed64.Zero);
                initialState = runtime.CaptureState();
            }
            catch (Exception ex)
            {
                if (runtime != null)
                {
                    try { runtime.Dispose(); }
                    catch (Exception) { /* 启动失败时以原始错误为准。 */ }
                }
                error = ex.Message;
                return false;
            }

            session = new TreePreviewSession(runtime, initialState!);
            session.SubscribeUpdate();
            return true;
        }

        private static void ApplyInitialOverrides(
            TreeRuntime runtime,
            IReadOnlyDictionary<string, PropertyValue>? overrides)
        {
            if (overrides == null) return;
            var blackboard = runtime.Blackboard;
            var schema = blackboard.Schema;
            foreach (var pair in overrides)
            {
                if (pair.Value == null || !schema.TryGetType(pair.Key, out var type)
                    || pair.Value.Type != type)
                    throw new InvalidOperationException("预览黑板初始值无效：" + pair.Key);
                switch (type)
                {
                    case ValueType.Bool: blackboard.SetBool(pair.Key, pair.Value.BoolValue); break;
                    case ValueType.Int64: blackboard.SetInt64(pair.Key, pair.Value.Int64Value); break;
                    case ValueType.Fixed64:
                        blackboard.SetFixed64(pair.Key, Fixed64.FromRaw(pair.Value.Fixed64Raw));
                        break;
                    case ValueType.String: blackboard.SetString(pair.Key, pair.Value.StringValue); break;
                }
            }
        }

        public void Pause() => _paused = true;

        public void Resume()
        {
            if (_disposed || IsFaulted) return;
            _paused = false;
        }

        public bool Step()
        {
            if (!_paused || _disposed || IsFaulted) return false;
            Advance();
            return !IsFaulted;
        }

        public bool TryReset(out string? error)
        {
            error = null;
            if (_disposed)
            {
                error = "预览会话已关闭。";
                return false;
            }
            try
            {
                _runtime.Disable();
                _runtime.Enable(0, Fixed64.Zero);
                _runtime.RestoreState(_initialState);
                _frame = 0;
                IsFaulted = false;
                FaultMessage = null;
                SubscribeUpdate();
                return true;
            }
            catch (Exception ex)
            {
                IsFaulted = true;
                FaultMessage = ex.Message;
                error = ex.Message;
                UnsubscribeUpdate();
                try { _runtime.Disable(); }
                catch (Exception) { /* Preserve the reset error. */ }
                return false;
            }
        }

        internal void Tick()
        {
            if (_disposed || IsFaulted || _paused) return;
            Advance();
        }

        private void Advance()
        {
            _frame++;
            try
            {
                _runtime.Update(_frame, Fixed64.FromRatio(_frame, TicksPerSecond));
            }
            catch (Exception ex)
            {
                IsFaulted = true;
                FaultMessage = string.IsNullOrWhiteSpace(ex.Message)
                    ? ex.GetType().Name
                    : ex.Message;
                UnsubscribeUpdate();
                try { Faulted?.Invoke(FaultMessage); }
                catch (Exception) { /* 预览故障通知不能再次泄漏到 EditorApplication.update。 */ }
            }
        }

        private void SubscribeUpdate()
        {
            if (_updateSubscribed) return;
            EditorApplication.update += Tick;
            _updateSubscribed = true;
        }

        private void UnsubscribeUpdate()
        {
            if (!_updateSubscribed) return;
            EditorApplication.update -= Tick;
            _updateSubscribed = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnsubscribeUpdate();
            try { _runtime.Disable(); } catch (Exception) { /* 观察端不因停止异常泄漏 */ }
            try { _runtime.Dispose(); } catch (Exception) { /* 预览释放不能污染编辑器 update。 */ }
        }
    }
}
