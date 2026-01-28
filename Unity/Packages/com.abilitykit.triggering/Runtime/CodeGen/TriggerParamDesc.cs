using System;

namespace AbilityKit.Triggering.CodeGen
{
    public enum ETriggerParamType : byte
    {
        Int = 0,
        Bool = 1,
        Float = 2,
    }

    [Flags]
    public enum ETriggerParamSource : byte
    {
        Const = 1 << 0,
        Payload = 1 << 1,
        Blackboard = 1 << 2,
    }

    public readonly struct TriggerParamDesc
    {
        public readonly int Index;
        public readonly string Name;
        public readonly ETriggerParamType Type;
        public readonly ETriggerParamSource AllowedSources;

        public TriggerParamDesc(int index, string name, ETriggerParamType type, ETriggerParamSource allowedSources)
        {
            Index = index;
            Name = name;
            Type = type;
            AllowedSources = allowedSources;
        }
    }
}
