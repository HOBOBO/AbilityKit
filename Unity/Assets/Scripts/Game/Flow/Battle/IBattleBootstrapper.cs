namespace AbilityKit.Game.Flow
{
    public interface IBattleBootstrapper
    {
        BattleStartPlan Build();
    }

    public readonly struct BattleStartPlan
    {
        public readonly string WorldId;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string PlayerId;

        public readonly bool AutoConnect;
        public readonly bool AutoCreateWorld;
        public readonly bool AutoJoin;
        public readonly bool AutoReady;

        public readonly int CreateWorldOpCode;
        public readonly byte[] CreateWorldPayload;

        public BattleStartPlan(
            string worldId,
            string worldType,
            string clientId,
            string playerId,
            bool autoConnect,
            bool autoCreateWorld,
            bool autoJoin,
            bool autoReady,
            int createWorldOpCode,
            byte[] createWorldPayload)
        {
            WorldId = worldId;
            WorldType = worldType;
            ClientId = clientId;
            PlayerId = playerId;
            AutoConnect = autoConnect;
            AutoCreateWorld = autoCreateWorld;
            AutoJoin = autoJoin;
            AutoReady = autoReady;
            CreateWorldOpCode = createWorldOpCode;
            CreateWorldPayload = createWorldPayload;
        }
    }
}
