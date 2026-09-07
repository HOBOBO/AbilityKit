using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Blackboard
{
    using AbilityKit.BehaviorTree.Definition;

    public sealed class BlackboardValueSnapshot
    {
        public List<string> KeyNames { get; set; } = new();
        public List<ValueType> KeyTypes { get; set; } = new();
        public List<bool> BoolValues { get; set; } = new();
        public List<long> Int64Values { get; set; } = new();
        public List<long> Fixed64RawValues { get; set; } = new();
        public List<string> StringValues { get; set; } = new();

        internal AbilityKit.BehaviorTree.BtBlackboardValueSnapshot ToLegacy()
        {
            var snapshot = new AbilityKit.BehaviorTree.BtBlackboardValueSnapshot
            {
                KeyNames = new List<string>(KeyNames),
                BoolValues = new List<bool>(BoolValues),
                Int64Values = new List<long>(Int64Values),
                Fixed64RawValues = new List<long>(Fixed64RawValues),
                StringValues = new List<string>(StringValues),
            };
            foreach (var type in KeyTypes)
            {
                snapshot.KeyTypes.Add(type.ToLegacy());
            }
            return snapshot;
        }

        internal static BlackboardValueSnapshot FromLegacy(AbilityKit.BehaviorTree.BtBlackboardValueSnapshot source)
        {
            var snapshot = new BlackboardValueSnapshot
            {
                KeyNames = new List<string>(source.KeyNames),
                BoolValues = new List<bool>(source.BoolValues),
                Int64Values = new List<long>(source.Int64Values),
                Fixed64RawValues = new List<long>(source.Fixed64RawValues),
                StringValues = new List<string>(source.StringValues),
            };
            foreach (var type in source.KeyTypes)
            {
                snapshot.KeyTypes.Add(type.ToApi());
            }
            return snapshot;
        }
    }
}
