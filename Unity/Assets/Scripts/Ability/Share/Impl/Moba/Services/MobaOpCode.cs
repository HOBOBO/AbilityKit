namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public enum MobaOpCode
    {
        Ready = 3001,
        Unready = 3002,
        Move = 3003,

        Skill1 = 3011,
        Skill2 = 3012,
        Skill3 = 3013,

        LobbySnapshot = 4001,
        EnterGameSnapshot = 4002,
        ActorTransformSnapshot = 4003,
        StateHashSnapshot = 4004,
    }
}
