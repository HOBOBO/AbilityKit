using AbilityKit.Network.Room;

namespace AbilityKit.Demo.Tiny.View
{
    public readonly struct TinySyncTelemetry
    {
        public TinySyncTelemetry(TinySyncMode mode, RoomGatewayConnectionState connection,
            bool inBattle, bool needsFullSnapshot, int authoritativeFrame, int predictedFrame,
            int localPredictions, int rollbacks, int snapshotCorrections,
            int recoveryRequests, int frameOverflows)
            : this(mode, connection, inBattle, false, needsFullSnapshot, authoritativeFrame,
                predictedFrame, localPredictions, rollbacks, snapshotCorrections,
                recoveryRequests, frameOverflows)
        {
        }

        public TinySyncTelemetry(TinySyncMode mode, RoomGatewayConnectionState connection,
            bool inBattle, bool awaitingBaseline, bool needsFullSnapshot,
            int authoritativeFrame, int predictedFrame,
            int localPredictions, int rollbacks, int snapshotCorrections,
            int recoveryRequests, int frameOverflows, uint stateHash = 0)
        {
            Mode = mode;
            Connection = connection;
            InBattle = inBattle;
            AwaitingBaseline = awaitingBaseline;
            NeedsFullSnapshot = needsFullSnapshot;
            AuthoritativeFrame = authoritativeFrame;
            PredictedFrame = predictedFrame;
            LocalPredictions = localPredictions;
            Rollbacks = rollbacks;
            SnapshotCorrections = snapshotCorrections;
            RecoveryRequests = recoveryRequests;
            FrameOverflows = frameOverflows;
            StateHash = stateHash;
        }

        public TinySyncMode Mode { get; }
        public RoomGatewayConnectionState Connection { get; }
        public bool InBattle { get; }
        public bool AwaitingBaseline { get; }
        public bool NeedsFullSnapshot { get; }
        public int AuthoritativeFrame { get; }
        public int PredictedFrame { get; }
        public int LocalPredictions { get; }
        public int Rollbacks { get; }
        public int SnapshotCorrections { get; }
        public int RecoveryRequests { get; }
        public int FrameOverflows { get; }
        public uint StateHash { get; }
    }
}
