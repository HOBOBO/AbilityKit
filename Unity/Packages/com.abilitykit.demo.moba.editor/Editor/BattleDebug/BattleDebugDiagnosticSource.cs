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
        public string FilePath => _offlineFilePath ?? string.Empty;
        public string DisplayName => string.IsNullOrEmpty(_offlineFilePath)
            ? string.Empty
            : Path.GetFileName(_offlineFilePath);

        public void Open(string json, string filePath)
        {
            var snapshot = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json);
            var session = new BattleDiagnosticOfflineSession(snapshot);

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
