#nullable enable

using System;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Room
{
    public enum RoomGatewayConnectionState
    {
        Connected,
        Reconnecting,
        RestoreRequired,
        Exhausted
    }

    /// <summary>Tracks connection epochs; callers restore their own room and battle state.</summary>
    public sealed class RoomGatewayConnectionRecovery : IDisposable
    {
        private readonly object _gate = new object();
        private readonly NetworkSessionRecoveryCoordinator _coordinator = new NetworkSessionRecoveryCoordinator();
        private readonly NetworkSdkClientRecoveryBinding _binding;
        private RoomGatewayConnectionState _state = RoomGatewayConnectionState.Connected;
        private long _generation;

        public RoomGatewayConnectionRecovery(NetworkSdkClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _coordinator.DecisionPublished += OnDecision;
            _binding = client.BindRecoverySignals(_coordinator);
        }

        public RoomGatewayConnectionState State
        {
            get { lock (_gate) return _state; }
        }

        public long Generation
        {
            get { lock (_gate) return _generation; }
        }

        public bool CompleteRestore(long generation)
        {
            lock (_gate)
            {
                if (_state != RoomGatewayConnectionState.RestoreRequired || _generation != generation)
                    return false;
                _state = RoomGatewayConnectionState.Connected;
            }
            _coordinator.Reset();
            return true;
        }

        private void OnDecision(NetworkSessionRecoveryDecision decision)
        {
            lock (_gate)
            {
                switch (decision.Signal.Kind)
                {
                    case NetworkSessionRecoverySignalKind.ConnectionLost:
                    case NetworkSessionRecoverySignalKind.ReconnectScheduled:
                    case NetworkSessionRecoverySignalKind.ReconnectAttemptStarted:
                        _state = RoomGatewayConnectionState.Reconnecting;
                        break;
                    case NetworkSessionRecoverySignalKind.ConnectionRestored:
                        _generation++;
                        _state = RoomGatewayConnectionState.RestoreRequired;
                        break;
                    case NetworkSessionRecoverySignalKind.ReconnectExhausted:
                        _state = RoomGatewayConnectionState.Exhausted;
                        break;
                }
            }
        }

        public void Dispose()
        {
            _binding.Dispose();
            _coordinator.DecisionPublished -= OnDecision;
        }
    }
}
