using MemoryPack;

namespace AbilityKit.Ability.Host
{
    [MemoryPackable]
    public readonly partial struct PlayerId
    {
        [MemoryPackOrder(0)] public readonly string Value;

        [MemoryPackConstructor]
        public PlayerId(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }
}
