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




    }
}
