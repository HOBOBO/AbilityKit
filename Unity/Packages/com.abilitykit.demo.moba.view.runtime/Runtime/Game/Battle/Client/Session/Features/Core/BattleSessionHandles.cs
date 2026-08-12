using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    // The feature owns one explicit session runtime composed from independently testable domain resources.
    internal sealed class BattleSessionHandles
    {
        internal BattleLogicSession Session;

        internal readonly BattleSessionSnapshotRuntime Snapshot = new BattleSessionSnapshotRuntime();
        internal readonly BattleSessionNetworkRuntime Net = new BattleSessionNetworkRuntime();
        internal readonly BattleSessionDispatcherRuntime Dispatchers = new BattleSessionDispatcherRuntime();
        internal readonly BattleSessionPhaseRuntime Phase = new BattleSessionPhaseRuntime();
        internal readonly BattleSessionGatewayRoomRuntime GatewayRoom = new BattleSessionGatewayRoomRuntime();
        internal readonly BattleSessionConfirmedWorldRuntime Confirmed = new BattleSessionConfirmedWorldRuntime();
        internal readonly BattleSessionRemoteDrivenWorldRuntime RemoteDriven = new BattleSessionRemoteDrivenWorldRuntime();

        public void ResetSessionResources()
        {
            Session = null;
            Snapshot.Reset();
            Net.Reset();
            Confirmed.Reset();
            RemoteDriven.Reset();
        }

        public void Reset()
        {
            ResetSessionResources();
            Dispatchers.Reset();
            Phase.Reset();
            GatewayRoom.Reset();
        }
    }
}
