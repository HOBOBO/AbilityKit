namespace AbilityKit.BehaviorTree.Registry
{
    using AbilityKit.BehaviorTree.Definition;

    public sealed class BlackboardKeyRef
    {
        public string Key { get; }
        public ValueType Type { get; }

        public BlackboardKeyRef(string key, ValueType type)
        {
            Key = key;
            Type = type;
        }

        internal AbilityKit.BehaviorTree.BtBlackboardKeyRef ToLegacy() => new(Key, Type.ToLegacy());

        internal static BlackboardKeyRef FromLegacy(AbilityKit.BehaviorTree.BtBlackboardKeyRef source)
            => new(source.Key, source.Type.ToApi());
    }
}
