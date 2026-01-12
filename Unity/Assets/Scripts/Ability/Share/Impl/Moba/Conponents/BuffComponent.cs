using System.Collections.Generic;
using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class BuffsComponent : IComponent
    {
        public List<BuffRuntime> Active;
    }

    public sealed class BuffRuntime
    {
        public int BuffId;
        public float Remaining;
        public int SourceId;
        public int StackCount;
        public long SourceContextId;
    }
}
