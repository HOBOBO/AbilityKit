using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticSource : IDisposable
    {
        private BattleDiagnosticSessionSnapshot _offlineSnapshot;
        private BattleDiagnosticOfflineSession _offlineSession;
        private string _offlineFilePath;

        public bool IsOffline => _offlineSession != null;
        public IBattleDiagnosticReadOnlySession Session => _offlineSession;
        public IReadOnlyList<BattleDiagnosticActorSummary> Actors =>
            _offlineSnapshot?.State.Actors ?? Array.Empty<BattleDiagnosticActorSummary>();
        public int LatestCompleteFrame =>
            _offlineSnapshot?.State.Frame ?? BattleDiagnosticFrames.Invalid;
        public bool HasEvents => _offlineSnapshot?.Events.Events.Count > 0;
        public BattleDiagnosticHealthSnapshot? HealthSnapshot =>
            _offlineSnapshot == null
                ? (BattleDiagnosticHealthSnapshot?)null
                : new BattleDiagnosticHealthSnapshot(
                    _offlineSnapshot.SessionInfo,
                    _offlineSnapshot.Events.Revision,
                    _offlineSnapshot.State.Revision,
                    _offlineSnapshot.Trace.Revision,
                    _offlineSnapshot.State.Frame,
                    _offlineSnapshot.Events.LastSequence,
                    ResolveCapturedChannels(_offlineSnapshot.Events.Events),
                    false,
                    _offlineSnapshot.Events.Metrics,
                    0L,
                    0L,
                    string.Empty,
                    string.Empty);
        public string FilePath => _offlineFilePath ?? string.Empty;
        public string DisplayName => string.IsNullOrEmpty(_offlineFilePath)
            ? string.Empty
            : Path.GetFileName(_offlineFilePath);

        /// <summary>
        /// 优先建议已记录的技能施放；同类候选沿用根目录的证据排序。
        /// </summary>
        public bool TryGetSuggestedTrace(out BattleDiagnosticTraceRootSummary root)
        {
            root = default;
            if (_offlineSession == null ||
                !_offlineSession.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace)) return false;

            var roots = BattleDiagnosticTraceRootProjection.Project(_offlineSnapshot.Trace.Nodes);
            if (roots.Count == 0) return false;
            root = roots[0];
            for (var index = 0; index < roots.Count; index++)
            {
                var candidate = roots[index];
                if (candidate.Root.ContextId == candidate.RootContextId &&
                    string.Equals(candidate.Root.Kind, "SkillCast", StringComparison.Ordinal))
                {
                    root = candidate;
                    break;
                }
            }
            return true;
        }

        private static BattleDiagnosticEventChannel ResolveCapturedChannels(
            IReadOnlyList<BattleDiagnosticEvent> events)
        {
            var channels = BattleDiagnosticEventChannel.None;
            if (events == null)
            {
                return channels;
            }

            for (var i = 0; i < events.Count; i++)
            {
                channels |= events[i].Channel;
            }

            return channels;
        }

        public void Open(string json, string filePath)
        {
            var artifact = MobaBattleDiagnosticArtifactCodec.ImportArtifact(json);
            if (artifact.BattleDiagnostics == null)
                throw new MobaBattleDiagnosticArtifactException(
                    "BattleDiagnostics.Missing",
                    "分析 Artifact 不包含战斗诊断数据段。被打开的文件可能不是战斗诊断产物。");
            var snapshot = MobaBattleDiagnosticArtifactCodec.FromSection(artifact.BattleDiagnostics);
            var metricProfile = MobaBattleDiagnosticArtifactCodec.FromMetricProfile(
                artifact.BattleDiagnostics.FrameMetricProfile);
            var session = new BattleDiagnosticOfflineSession(snapshot, metricProfile);

            var previous = _offlineSession;
            _offlineSnapshot = snapshot;
            _offlineSession = session;
            _offlineFilePath = filePath ?? string.Empty;
            previous?.Dispose();
        }

        public void ReturnToLive()
        {
            var previous = _offlineSession;
            _offlineSnapshot = null;
            _offlineSession = null;
            _offlineFilePath = null;
            previous?.Dispose();
        }

        public void Dispose()
        {
            ReturnToLive();
        }
    }
}
