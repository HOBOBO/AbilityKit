using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the stop and disposal boundary for session replication resources.
    /// </summary>
    internal sealed class SessionReplicationController
    {
        private readonly Func<Task> _stopRecoveryAsync;
        private readonly Action _disposeRecovery;
        private readonly Action _disposeReplication;
        private readonly Action _disposeInputDiagnostics;
        private readonly Func<BattleContext> _getContext;

        internal SessionReplicationController(
            Func<AuthoritativeStateRecoveryRuntime> getRecovery,
            BattleReplicationRuntime replication,
            InputSubmissionDiagnosticsBinding inputDiagnostics,
            Func<BattleContext> getContext)
            : this(
                () => getRecovery?.Invoke()?.StopAsync() ?? Task.CompletedTask,
                () => getRecovery?.Invoke()?.Dispose(),
                (replication ?? throw new ArgumentNullException(nameof(replication))).Dispose,
                (inputDiagnostics ?? throw new ArgumentNullException(nameof(inputDiagnostics))).Dispose,
                getContext)
        {
            if (getRecovery == null) throw new ArgumentNullException(nameof(getRecovery));
        }

        internal SessionReplicationController(
            Func<Task> stopRecoveryAsync,
            Action disposeRecovery,
            Action disposeReplication,
            Action disposeInputDiagnostics)
            : this(
                stopRecoveryAsync,
                disposeRecovery,
                disposeReplication,
                disposeInputDiagnostics,
                () => null)
        {
        }

        internal SessionReplicationController(
            Func<Task> stopRecoveryAsync,
            Action disposeRecovery,
            Action disposeReplication,
            Action disposeInputDiagnostics,
            Func<BattleContext> getContext)
        {
            _stopRecoveryAsync = stopRecoveryAsync ?? throw new ArgumentNullException(nameof(stopRecoveryAsync));
            _disposeRecovery = disposeRecovery ?? throw new ArgumentNullException(nameof(disposeRecovery));
            _disposeReplication = disposeReplication ?? throw new ArgumentNullException(nameof(disposeReplication));
            _disposeInputDiagnostics = disposeInputDiagnostics ??
                throw new ArgumentNullException(nameof(disposeInputDiagnostics));
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
        }

        internal Task StopRecoveryAsync() => _stopRecoveryAsync() ?? Task.CompletedTask;

        internal void Dispose()
        {
            _disposeRecovery();
            _disposeReplication();
            _disposeInputDiagnostics();
            var context = _getContext();
            if (context != null) context.CanSubmitGameplayInput = true;
        }
    }
}
