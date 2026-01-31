namespace AbilityKit.Ability.Host
{
    public readonly struct WorldInitData
    {
        public readonly int OpCode;
        public readonly byte[] Payload;

        public WorldInitData(int opCode, byte[] payload)
        {
            OpCode = opCode;
            Payload = payload;
        }
    }
}
