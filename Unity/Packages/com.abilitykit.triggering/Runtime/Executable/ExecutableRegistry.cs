using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 行为类型描述符
    /// </summary>
    public sealed class ExecutableDescriptor
    {
        public int TypeId;
        public string TypeName;
        public ExecutableMetadata Metadata;
        public bool IsScheduled => Metadata.IsScheduled;
        public bool IsPeriodic => Metadata.IsScheduled && Metadata.DefaultPeriodMs.HasValue;
        public Func<IExecutable> Factory;
    }

    /// <summary>
    /// 条件类型描述符
    /// </summary>
    public sealed class ConditionDescriptor
    {
        public int TypeId;
        public string TypeName;
        public Func<ICondition> Factory;
    }

    /// <summary>
    /// 行为类型注册表
    /// </summary>
    public sealed class ExecutableRegistry
    {
        private static readonly Lazy<ExecutableRegistry> _instance = new(() => new ExecutableRegistry());
        public static ExecutableRegistry Instance => _instance.Value;

        private readonly Dictionary<int, ExecutableDescriptor> _executables = new();
        private readonly Dictionary<string, int> _nameToId = new();
        private readonly Dictionary<int, ConditionDescriptor> _conditions = new();
        private readonly Dictionary<string, int> _conditionNameToId = new();

        private ExecutableRegistry()
        {
            RegisterBuiltin();
        }

        public void Register<TExecutable>(int typeId, string typeName, ExecutableMetadata metadata = default)
            where TExecutable : IExecutable, new()
        {
            _executables[typeId] = new ExecutableDescriptor
            {
                TypeId = typeId,
                TypeName = typeName,
                Metadata = metadata,
                Factory = () => new TExecutable()
            };
            _nameToId[typeName] = typeId;
        }

        public IExecutable CreateExecutable(int typeId)
        {
            if (_executables.TryGetValue(typeId, out var descriptor))
                return descriptor.Factory();
            throw new KeyNotFoundException($"Executable type {typeId} not found");
        }

        public TExecutable CreateExecutable<TExecutable>(int typeId) where TExecutable : IExecutable
            => (TExecutable)CreateExecutable(typeId);

        public bool TryGetDescriptor(int typeId, out ExecutableDescriptor descriptor)
            => _executables.TryGetValue(typeId, out descriptor);

        public ExecutableDescriptor GetDescriptor(int typeId)
        {
            if (_executables.TryGetValue(typeId, out var descriptor))
                return descriptor;
            throw new KeyNotFoundException($"Executable type {typeId} not found");
        }

        public bool TryGetTypeIdByName(string typeName, out int typeId)
            => _nameToId.TryGetValue(typeName, out typeId);

        public void RegisterCondition<TCondition>(int typeId, string typeName)
            where TCondition : ICondition, new()
        {
            _conditions[typeId] = new ConditionDescriptor
            {
                TypeId = typeId,
                TypeName = typeName,
                Factory = () => new TCondition()
            };
            _conditionNameToId[typeName] = typeId;
        }

        public ICondition CreateCondition(int typeId)
        {
            if (_conditions.TryGetValue(typeId, out var descriptor))
                return descriptor.Factory();
            throw new KeyNotFoundException($"Condition type {typeId} not found");
        }

        public IEnumerable<ExecutableDescriptor> GetAllExecutables()
            => _executables.Values;

        private void RegisterBuiltin()
        {
            Register<SequenceExecutable>(1, "Sequence", new ExecutableMetadata(1, "Sequence"));
            Register<IfExecutable>(10, "If", new ExecutableMetadata(10, "If"));
            Register<IfElseExecutable>(11, "IfElse", new ExecutableMetadata(11, "IfElse"));
            Register<SwitchExecutable>(12, "Switch", new ExecutableMetadata(12, "Switch"));
            Register<ActionCallExecutable>(100, "ActionCall", new ExecutableMetadata(100, "ActionCall"));
            Register<DelayExecutable>(200, "Delay", new ExecutableMetadata(200, "Delay"));

            RegisterCondition<ConstCondition>(0, "Const");
            RegisterCondition<AndCondition>(1, "And");
            RegisterCondition<OrCondition>(2, "Or");
            RegisterCondition<NotCondition>(3, "Not");
            RegisterCondition<NumericCompareCondition>(10, "NumericCompare");
            RegisterCondition<PayloadCompareCondition>(11, "PayloadCompare");
            RegisterCondition<HasTargetCondition>(20, "HasTarget");
            RegisterCondition<MultiCondition>(100, "Multi");
        }
    }
}
