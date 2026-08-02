using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleAssetLoadCoordinator : IBattleAssetLoadCoordinator
    {
        private readonly IBattleAssetLoadService _loadService;
        private readonly Func<BattleAssetManifest> _manifestProvider;
        private readonly IProgress<BattleAssetLoadProgress> _progress;
        private readonly object _gate = new object();

        private IBattleAssetLease _currentLease;
        private BattleAssetLoadResult _lastResult;
        private LoadOperation _activeOperation;

        public BattleAssetLoadCoordinator(
            IBattleAssetLoadService loadService,
            Func<BattleAssetManifest> manifestProvider,
            IProgress<BattleAssetLoadProgress> progress = null)
        {
            _loadService = loadService ?? throw new ArgumentNullException(nameof(loadService));
            _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
            _progress = progress;
        }

        public bool IsLoading
        {
            get
            {
                lock (_gate) return _activeOperation != null;
            }
        }

        public BattleAssetLoadResult LastResult
        {
            get
            {
                lock (_gate) return _lastResult;
            }
        }

        public void StartLoading(Action<bool> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            LoadOperation operation;
            lock (_gate)
            {
                if (_activeOperation != null)
                {
                    throw new InvalidOperationException("Battle asset loading is already in progress.");
                }

                operation = new LoadOperation(
                    new CancellationTokenSource(),
                    onComplete);
                _activeOperation = operation;
                _lastResult = null;
            }

            BattleAssetManifest manifest;
            try
            {
                manifest = _manifestProvider()
                    ?? throw new InvalidOperationException("Battle asset manifest must not be null.");
            }
            catch
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeOperation, operation))
                    {
                        _activeOperation = null;
                    }
                }

                operation.Dispose();
                throw;
            }

            _ = LoadAsyncCore(operation, manifest);
        }

        public void Cancel()
        {
            LoadOperation operation;
            lock (_gate)
            {
                operation = _activeOperation;
                _activeOperation = null;
            }

            if (operation == null) return;

            try
            {
                operation.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A racing completion already owns cleanup.
            }

            operation.Callback(false);
        }

        public void ReleaseLease()
        {
            TakeLease()?.Dispose();
        }

        public IBattleAssetLease TakeLease()
        {
            lock (_gate)
            {
                var lease = _currentLease;
                _currentLease = null;
                return lease;
            }
        }

        private async Task LoadAsyncCore(LoadOperation operation, BattleAssetManifest manifest)
        {
            var success = false;
            IBattleAssetLease loadedLease = null;
            BattleAssetLoadResult loadResult = null;
            try
            {
                loadResult = await _loadService
                    .LoadAsync(manifest, _progress, operation.Cancellation.Token)
                    .ConfigureAwait(true);
                loadedLease = loadResult?.Lease;
                if (loadResult != null && loadResult.Success && loadedLease != null && loadedLease.IsActive)
                {
                    success = true;
                }
            }
            catch (OperationCanceledException)
            {
                success = false;
            }
            catch (Exception ex)
            {
                success = false;
                loadResult = BuildExceptionResult(manifest, ex);
            }

            Action<bool> callback = null;
            IBattleAssetLease previousLease = null;
            lock (_gate)
            {
                if (ReferenceEquals(_activeOperation, operation))
                {
                    _activeOperation = null;
                    _lastResult = loadResult;
                    callback = operation.Callback;
                    if (success)
                    {
                        previousLease = _currentLease;
                        _currentLease = loadedLease;
                        loadedLease = null;
                    }
                }
            }

            operation.Dispose();
            loadedLease?.Dispose();
            previousLease?.Dispose();
            callback?.Invoke(success);
        }

        private static BattleAssetLoadResult BuildExceptionResult(
            BattleAssetManifest manifest,
            Exception exception)
        {
            var reason = "Exception: " + exception.GetType().Name + ": " + exception.Message;
            return new BattleAssetLoadResult(
                false,
                manifest.LaunchGeneration,
                manifest.ManifestVersion,
                manifest.ManifestHash,
                new[] { new BattleAssetLoadError(string.Empty, string.Empty, reason) });
        }

        private sealed class LoadOperation : IDisposable
        {
            public LoadOperation(
                CancellationTokenSource cancellation,
                Action<bool> callback)
            {
                Cancellation = cancellation;
                Callback = callback;
            }

            public CancellationTokenSource Cancellation { get; }
            public Action<bool> Callback { get; }

            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Cancellation.Dispose();
                }
            }
        }
    }
}
