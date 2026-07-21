namespace AbilityKit.Protocol.Moba
{
    /// <summary>
    /// MOBA battle-level opcode definitions shared by runtime, view, and transport adapters.
    /// </summary>
    public static class MobaOpCodes
    {
        public static class Input
        {
            public const int Ready = 3001;
            public const int Unready = 3002;
            public const int Move = 3003;
            public const int Attack = 3004;
            public const int Stop = 3005;

            public const int Skill1 = 3011;
            public const int Skill2 = 3012;
            public const int Skill3 = 3013;
            public const int SkillInput = 3020;

            public const int DebugSpawnUnit = 3030;
            public const int DebugReplaceHero = 3031;
        }

        public static class Snapshot
        {
            public const int Lobby = 4001;
            public const int EnterGame = 4002;
            public const int ActorTransform = 4003;
            public const int StateHash = 4004;
            public const int ActorSpawn = 4005;
            public const int ProjectileEvent = 4006;
            public const int DamageEvent = 4007;
            public const int ActorDespawn = 4008;
            public const int AreaEvent = 4009;
            public const int PresentationCue = 4010;
            public const int SkillState = 4011;
            public const int PlayerHeroChanged = 4012;
        }
    }
}
