namespace AbilityKit.Triggering.Blackboard
{
    public readonly struct BlackboardKeyMeta
    {
        public readonly int Id;
        public readonly string Name;
        public readonly BlackboardKeyType Type;
        public readonly bool Readable;
        public readonly bool Writable;

        public BlackboardKeyMeta(int id, string name, BlackboardKeyType type, bool readable, bool writable)
        {
            Id = id;
            Name = name;
            Type = type;
            Readable = readable;
            Writable = writable;
        }
    }
}
