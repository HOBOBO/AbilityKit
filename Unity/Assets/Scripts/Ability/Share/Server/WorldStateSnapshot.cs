namespace AbilityKit.Ability.Server
{
    public readonly struct WorldStateSnapshot
    {
        public readonly int OpCode;
        public readonly byte[] Payload;

        public WorldStateSnapshot(int opCode, byte[] payload)
        {
            OpCode = opCode;
            Payload = payload;
        }
    }
}
