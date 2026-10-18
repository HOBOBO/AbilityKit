namespace AbilityKit.Game.Flow
{
    internal static class MobaFlowConditionIds
    {
        public const string BattleRequested = "battle_requested";
        public const string Authenticated = "authenticated";
        public const string RoomReady = "room_ready";
        public const string ConnectivityReady = "connectivity_ready";
        public const string AssetsReady = "assets_ready";
        public const string BattleEntryReady = "battle_entry_ready";
    }
 
    internal readonly struct MobaFlowConditionContext
    {
        public MobaFlowConditionContext(
            bool battleRequested,
            bool authenticated,
            bool roomReady,
            bool connectivityReady,
            bool assetsReady)
        {
            BattleRequested = battleRequested;
            Authenticated = authenticated;
            RoomReady = roomReady;
            ConnectivityReady = connectivityReady;
            AssetsReady = assetsReady;
        }
 
        public bool BattleRequested { get; }
        public bool Authenticated { get; }
        public bool RoomReady { get; }
        public bool ConnectivityReady { get; }
        public bool AssetsReady { get; }
 
        public bool BattleEntryReady => BattleRequested && Authenticated && RoomReady && ConnectivityReady && AssetsReady;
    }

    internal sealed class MobaFlowConditionResolver
    {
        public bool Evaluate(string conditionId, in MobaFlowConditionContext ctx)
        {
            if (string.IsNullOrEmpty(conditionId))
            {
                return true;
            }

            switch (conditionId)
            {
                case MobaFlowConditionIds.BattleRequested:
                    return ctx.BattleRequested;
                case MobaFlowConditionIds.Authenticated:
                    return ctx.Authenticated;
                case MobaFlowConditionIds.RoomReady:
                    return ctx.RoomReady;
                case MobaFlowConditionIds.ConnectivityReady:
                    return ctx.ConnectivityReady;
                case MobaFlowConditionIds.AssetsReady:
                    return ctx.AssetsReady;
                case MobaFlowConditionIds.BattleEntryReady:
                    return ctx.BattleEntryReady;
                default:
                    return false;
            }
        }
    }
}
