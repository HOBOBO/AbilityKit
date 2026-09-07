namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class BlackboardKeyDefinition
    {
        public string Name { get; set; } = "";
        public ValueType Type { get; set; } = ValueType.Int64;
        public PropertyValue? Default { get; set; }

        internal AbilityKit.BehaviorTree.BtBlackboardKeyDefinition ToLegacy() => new()
        {
            Name = Name,
            Type = Type.ToLegacy(),
            Default = Default?.ToLegacy(),
        };

        internal static BlackboardKeyDefinition FromLegacy(AbilityKit.BehaviorTree.BtBlackboardKeyDefinition source) => new()
        {
            Name = source.Name,
            Type = source.Type.ToApi(),
            Default = source.Default == null ? null : PropertyValue.FromLegacy(source.Default),
        };
    }
}
