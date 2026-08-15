using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    internal readonly struct BattleLoadingSnapshot
    {
        public BattleLoadingSnapshot(
            bool isVisible,
            bool isLoading,
            int loadedCount,
            int totalCount,
            string currentAssetKey,
            bool completed,
            bool success,
            string statusLine,
            string errorMessage,
            IReadOnlyList<BattleAssetLoadError> errors)
        {
            IsVisible = isVisible;
            IsLoading = isLoading;
            LoadedCount = loadedCount;
            TotalCount = totalCount;
            CurrentAssetKey = currentAssetKey ?? string.Empty;
            Completed = completed;
            Success = success;
            StatusLine = statusLine ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            Errors = errors ?? Array.Empty<BattleAssetLoadError>();
        }

        public bool IsVisible { get; }
        public bool IsLoading { get; }
        public int LoadedCount { get; }
        public int TotalCount { get; }
        public string CurrentAssetKey { get; }
        public bool Completed { get; }
        public bool Success { get; }
        public string StatusLine { get; }
        public string ErrorMessage { get; }
        public IReadOnlyList<BattleAssetLoadError> Errors { get; }
        public float Progress01 => TotalCount <= 0 ? 0f : LoadedCount / (float)TotalCount;

        public BattleAssetLoadProgressSnapshot ToProgressSnapshot()
        {
            return new BattleAssetLoadProgressSnapshot
            {
                IsLoading = IsLoading,
                LoadedCount = LoadedCount,
                TotalCount = TotalCount,
                CurrentAssetKey = CurrentAssetKey,
                Completed = Completed,
                Success = Success,
                ErrorMessage = ErrorMessage,
                Errors = Errors
            };
        }
    }

    internal static class BattleLoadingLeaseTransaction
    {
        public static bool TryAdopt(
            IBattleAssetLoadSessionPort sessionPort,
            IBattleAssetLease lease)
        {
            if (lease == null) return false;
            if (sessionPort == null || !lease.IsActive)
            {
                DisposeRejected(lease);
                return false;
            }

            try
            {
                sessionPort.AdoptAssetLease(lease);
                return true;
            }
            catch (Exception ex)
            {
                DisposeRejected(lease);
                Debug.LogWarning("[BattleLoadingScreen] Asset lease adoption failed: " + ex.Message);
                return false;
            }
        }

        private static void DisposeRejected(IBattleAssetLease lease)
        {
            try
            {
                lease.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleLoadingScreen] Failed to release rejected lease: " + ex.Message);
            }
        }
    }

    internal interface IBattleLoadingCommandSink
    {
        void RequestCancel();
        void RequestRetry();
        void RequestReturnLobby();
    }

    internal sealed class BattleLoadingRuntime : IBattleAssetLoadProgressObserver, IDisposable
    {
        private IBattleAssetLoadCoordinator _coordinator;
        private IBattleAssetLoadSessionPort _sessionPort;
        private int _operationGeneration;
        private bool _started;
        private bool _cancelRequested;
        private bool _completionPending;
        private BattleLoadingSnapshot _snapshot = InitialSnapshot();

        public BattleLoadingSnapshot Snapshot => _snapshot;
        public bool HasCoordinator => _coordinator != null;

        public void Attach(
            IBattleAssetLoadSessionPort sessionPort,
            IBattleAssetLoadCoordinator coordinator)
        {
            _operationGeneration++;
            _sessionPort = sessionPort;
            if (coordinator != null)
            {
                _coordinator = coordinator;
            }
            _started = false;
            _cancelRequested = false;
            _completionPending = false;
            _snapshot = InitialSnapshot();
        }

        public void SetCoordinator(IBattleAssetLoadCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public bool TryAdoptPreloadedLease(IBattleAssetLease lease)
        {
            if (lease == null) return false;
            if (!BattleLoadingLeaseTransaction.TryAdopt(_sessionPort, lease)) return false;

            _started = true;
            _snapshot = new BattleLoadingSnapshot(
                false,
                false,
                1,
                1,
                string.Empty,
                true,
                true,
                "Load complete",
                string.Empty,
                Array.Empty<BattleAssetLoadError>());
            _completionPending = true;
            return true;
        }

        public void Start()
        {
            if (_started) return;
            if (_coordinator == null)
            {
                MarkUnavailable();
                return;
            }

            _started = true;
            _cancelRequested = false;
            var operationGeneration = ++_operationGeneration;
            var coordinator = _coordinator;
            OnLoadStarted(new BattleAssetLoadProgressSnapshot { IsLoading = true });

            try
            {
                coordinator.StartLoading(success =>
                    CompleteOperation(operationGeneration, coordinator, success));
            }
            catch (Exception ex)
            {
                var failed = new BattleAssetLoadProgressSnapshot
                {
                    Completed = true,
                    IsLoading = false,
                    Success = false,
                    ErrorMessage = ex.Message
                };
                OnLoadCompleted(failed);
                Debug.LogWarning("[BattleLoadingScreen] Start failed: " + ex.Message);
            }
        }

        public void Cancel()
        {
            if (_coordinator == null || !_snapshot.IsLoading) return;
            _cancelRequested = true;
            try
            {
                _coordinator.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleLoadingScreen] Cancel failed: " + ex.Message);
            }
        }

        public void Retry()
        {
            if (_snapshot.IsLoading || !_snapshot.Completed || _snapshot.Success) return;
            _started = false;
            Start();
        }

        public void Tick()
        {
            if (!_completionPending) return;
            _completionPending = false;
            _sessionPort?.NotifyAssetsLoadCompleted();
        }

        public void MarkUnavailable()
        {
            _snapshot = new BattleLoadingSnapshot(
                true,
                false,
                0,
                0,
                string.Empty,
                true,
                false,
                "Battle asset loader is unavailable",
                string.Empty,
                Array.Empty<BattleAssetLoadError>());
        }

        public void OnProgress(BattleAssetLoadProgress progress)
        {
            OnLoadProgressed(new BattleAssetLoadProgressSnapshot
            {
                IsLoading = true,
                LoadedCount = progress.LoadedCount,
                TotalCount = progress.TotalCount,
                CurrentAssetKey = progress.CurrentAssetKey
            });
        }

        public void Dispose()
        {
            _operationGeneration++;
            if (_coordinator != null && _coordinator.IsLoading)
            {
                try
                {
                    _coordinator.Cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BattleLoadingScreen] Cancel failed: " + ex.Message);
                }
            }

            _coordinator?.ReleaseLease();
            _coordinator = null;
            _sessionPort = null;
            _started = false;
            _cancelRequested = false;
            _completionPending = false;
        }

        void IBattleAssetLoadProgressObserver.OnLoadStarted(BattleAssetLoadProgressSnapshot snapshot)
        {
            OnLoadStarted(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadProgressed(BattleAssetLoadProgressSnapshot snapshot)
        {
            OnLoadProgressed(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadCompleted(BattleAssetLoadProgressSnapshot snapshot)
        {
            OnLoadCompleted(snapshot);
        }

        void IBattleAssetLoadProgressObserver.OnLoadCancelled(BattleAssetLoadProgressSnapshot snapshot)
        {
            OnLoadCancelled(snapshot);
        }

        private void CompleteOperation(
            int operationGeneration,
            IBattleAssetLoadCoordinator coordinator,
            bool success)
        {
            if (operationGeneration != _operationGeneration)
            {
                coordinator.ReleaseLease();
                return;
            }

            var result = coordinator.LastResult;
            var errors = result?.Errors ?? Array.Empty<BattleAssetLoadError>();
            var errorMessage = !success && errors.Count > 0
                ? BuildErrorSummary(errors)
                : string.Empty;

            if (success)
            {
                var lease = coordinator.TakeLease();
                if (lease == null)
                {
                    success = false;
                    errorMessage = "Asset load completed without an active lease";
                }
                else if (!BattleLoadingLeaseTransaction.TryAdopt(_sessionPort, lease))
                {
                    success = false;
                    errorMessage = "Battle session rejected the loaded asset lease";
                }
            }

            var completed = new BattleAssetLoadProgressSnapshot
            {
                Completed = true,
                IsLoading = false,
                Success = success,
                ErrorMessage = !success && string.IsNullOrEmpty(errorMessage)
                    ? "Load failed"
                    : errorMessage,
                Errors = errors
            };

            if (_cancelRequested)
            {
                OnLoadCancelled(completed);
            }
            else
            {
                OnLoadCompleted(completed);
            }

            if (success)
            {
                _completionPending = true;
            }
        }

        private void OnLoadStarted(BattleAssetLoadProgressSnapshot snapshot)
        {
            _snapshot = FromProgress(
                snapshot,
                true,
                $"Loading {snapshot.TotalCount} asset(s)...");
        }

        private void OnLoadProgressed(BattleAssetLoadProgressSnapshot snapshot)
        {
            var status = !string.IsNullOrEmpty(snapshot.CurrentAssetKey)
                ? $"[{snapshot.LoadedCount}/{snapshot.TotalCount}] {snapshot.CurrentAssetKey}"
                : $"[{snapshot.LoadedCount}/{snapshot.TotalCount}]";
            _snapshot = FromProgress(snapshot, true, status);
        }

        private void OnLoadCompleted(BattleAssetLoadProgressSnapshot snapshot)
        {
            var status = snapshot.Success
                ? "Load complete"
                : "Load failed: " + (snapshot.ErrorMessage ?? "unknown");
            _snapshot = FromProgress(snapshot, !snapshot.Success, status);
        }

        private void OnLoadCancelled(BattleAssetLoadProgressSnapshot snapshot)
        {
            _snapshot = FromProgress(snapshot, true, "Cancelled");
        }

        private static BattleLoadingSnapshot FromProgress(
            BattleAssetLoadProgressSnapshot snapshot,
            bool isVisible,
            string statusLine)
        {
            return new BattleLoadingSnapshot(
                isVisible,
                snapshot.IsLoading,
                snapshot.LoadedCount,
                snapshot.TotalCount,
                snapshot.CurrentAssetKey,
                snapshot.Completed,
                snapshot.Success,
                statusLine,
                snapshot.ErrorMessage,
                snapshot.Errors);
        }

        private static BattleLoadingSnapshot InitialSnapshot()
        {
            return new BattleLoadingSnapshot(
                true,
                false,
                0,
                0,
                string.Empty,
                false,
                false,
                "Initializing...",
                string.Empty,
                Array.Empty<BattleAssetLoadError>());
        }

        private static string BuildErrorSummary(IReadOnlyList<BattleAssetLoadError> errors)
        {
            if (errors == null || errors.Count == 0) return "Load failed";

            var first = errors[0];
            var keyOrPath = !string.IsNullOrEmpty(first.AssetKey)
                ? first.AssetKey
                : first.AssetPath;
            var summary = string.IsNullOrEmpty(keyOrPath)
                ? first.Reason
                : keyOrPath + ": " + first.Reason;
            return errors.Count > 1
                ? summary + " (and " + (errors.Count - 1) + " more)"
                : summary;
        }
    }
}
