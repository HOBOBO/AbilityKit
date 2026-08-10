using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 诊断状态面板的 ViewModel：持有帧输入、查询缓存和结果状态，
    /// 不依赖 UnityEditor，可被 IMGUI、UI Toolkit 或其他前端复用。
    /// 绘制层只读取 <see cref="WorldSummary"/>、<see cref="Actors"/> 和 <see cref="StatusMessage"/> 进行渲染。
    /// </summary>
    internal sealed class BattleDebugDiagnosticStateViewModel
    {
        private long _lastRequestId;
        private long _lastStoreRevision = -1;
        private int _lastFrameInput;
        private bool _hasCachedResult;
        private BattleDiagnosticWorldSummary? _cachedWorld;
        private IReadOnlyList<BattleDiagnosticActorSummary> _cachedActors;

        /// <summary>查询的目标帧（0 表示最新）。</summary>
        public int FrameInput { get; set; }

        /// <summary>当前缓存的世界快照（null 表示尚未采样）。</summary>
        public BattleDiagnosticWorldSummary? WorldSummary => _cachedWorld;

        /// <summary>当前缓存的 Actor 摘要列表（可能为 null 或空）。</summary>
        public IReadOnlyList<BattleDiagnosticActorSummary> Actors => _cachedActors;

        /// <summary>最近一次查询的状态消息（空字符串表示无特殊状态）。</summary>
        public string StatusMessage { get; private set; } = string.Empty;

        public BattleDiagnosticQueryStatus WorldQueryStatus { get; private set; }
        public BattleDiagnosticQueryStatus ActorQueryStatus { get; private set; }

        /// <summary>最近一次查询时的 Store Revision。</summary>
        public long StoreRevision => _lastStoreRevision;

        /// <summary>标记缓存失效，下次 <see cref="RefreshIfNeeded"/> 会重新查询。</summary>
        public void InvalidateCache()
        {
            _cachedWorld = null;
            _cachedActors = null;
            WorldQueryStatus = default;
            ActorQueryStatus = default;
            _lastStoreRevision = -1;
            _hasCachedResult = false;
        }

        /// <summary>
        /// 如果缓存有效则直接返回；否则根据当前帧输入重新查询世界和 Actor 状态。
        /// </summary>
        /// <param name="session">诊断只读会话。</param>
        public void RefreshIfNeeded(IBattleDiagnosticReadOnlySession session)
        {
            var currentRevision = session.StateStoreRevision;
            if (_hasCachedResult &&
                _lastStoreRevision == currentRevision &&
                _lastFrameInput == FrameInput)
            {
                return;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var frame = FrameInput < 0 ? 0 : FrameInput;

            var worldResult = session.QueryWorld(_lastRequestId, frame);
            var actorsResult = session.QueryActors(_lastRequestId, frame);
            WorldQueryStatus = worldResult.Status;
            ActorQueryStatus = actorsResult.Status;

            _lastStoreRevision = currentRevision;
            _lastFrameInput = FrameInput;
            _hasCachedResult = true;

            if (worldResult.Status.CanDisplayResults && worldResult.Items.Count > 0)
            {
                _cachedWorld = worldResult.Items[0];
            }
            else
            {
                _cachedWorld = null;
            }

            _cachedActors = actorsResult.Items;

            StatusMessage = BuildStatusMessage(worldResult.Status, actorsResult.Status);
        }

        private static string BuildStatusMessage(
            BattleDiagnosticQueryStatus worldStatus,
            BattleDiagnosticQueryStatus actorStatus)
        {
            if (!worldStatus.CanDisplayResults && worldStatus.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"世界状态不可用：{worldStatus.Availability} {worldStatus.Message}";
            }

            if (!actorStatus.CanDisplayResults && actorStatus.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"Actor 状态不可用：{actorStatus.Availability} {actorStatus.Message}";
            }

            return string.Empty;
        }
    }
}
