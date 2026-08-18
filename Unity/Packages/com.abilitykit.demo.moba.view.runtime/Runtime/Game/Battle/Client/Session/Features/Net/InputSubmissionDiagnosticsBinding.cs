using System;
using AbilityKit.Network.Battle;

namespace AbilityKit.Game.Flow
{
    internal sealed class InputSubmissionDiagnosticsBinding : IDisposable
    {
        private readonly object _gate = new object();
        private NetworkTransport _transport;
        private InputSubmissionStatsSnapshot _snapshot;
        private Action<NetworkSubmitInputResponse> _completed;
        private Action<Exception> _failed;

        internal bool IsBound => _transport != null;

        internal void Bind(NetworkTransport transport)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            Dispose();
            _transport = transport;
            _snapshot = new InputSubmissionStatsSnapshot();
            InputSubmissionStatsProvider.Current = _snapshot;
            _completed = HandleCompleted;
            _failed = HandleFailed;
            transport.SubmitInputCompleted += _completed;
            transport.SubmitInputFailed += _failed;
        }

        public void Dispose()
        {
            var transport = _transport;
            _transport = null;
            if (transport != null)
            {
                if (_completed != null) transport.SubmitInputCompleted -= _completed;
                if (_failed != null) transport.SubmitInputFailed -= _failed;
            }

            if (ReferenceEquals(InputSubmissionStatsProvider.Current, _snapshot))
            {
                InputSubmissionStatsProvider.Current = null;
            }

            _snapshot = null;
            _completed = null;
            _failed = null;
        }

        private void HandleCompleted(NetworkSubmitInputResponse response)
        {
            lock (_gate)
            {
                if (_transport == null || _snapshot == null) return;
                var previous = _snapshot;
                var snapshot = new InputSubmissionStatsSnapshot
                {
                    CompletedCount = previous.CompletedCount + 1,
                    AcceptedCount = previous.AcceptedCount + (response.Accepted ? 1 : 0),
                    RejectedCount = previous.RejectedCount + (response.Accepted ? 0 : 1),
                    FailedCount = previous.FailedCount,
                    LastServerFrame = response.ServerFrame,
                    LastAcceptedFrame = response.AcceptedFrame,
                    LastReasonCode = response.ReasonCode,
                    LastShouldResync = response.ShouldResync,
                    LastStatus = response.Status,
                    LastMessage = response.Message,
                    LastFailure = previous.LastFailure
                };
                _snapshot = snapshot;
                InputSubmissionStatsProvider.Current = snapshot;
            }
        }

        private void HandleFailed(Exception exception)
        {
            lock (_gate)
            {
                if (_transport == null || _snapshot == null) return;
                var previous = _snapshot;
                var snapshot = new InputSubmissionStatsSnapshot
                {
                    CompletedCount = previous.CompletedCount,
                    AcceptedCount = previous.AcceptedCount,
                    RejectedCount = previous.RejectedCount,
                    FailedCount = previous.FailedCount + 1,
                    LastServerFrame = previous.LastServerFrame,
                    LastAcceptedFrame = previous.LastAcceptedFrame,
                    LastReasonCode = previous.LastReasonCode,
                    LastShouldResync = previous.LastShouldResync,
                    LastStatus = previous.LastStatus,
                    LastMessage = previous.LastMessage,
                    LastFailure = exception?.ToString() ?? string.Empty
                };
                _snapshot = snapshot;
                InputSubmissionStatsProvider.Current = snapshot;
            }
        }
    }
}
