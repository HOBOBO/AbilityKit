using System;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PlanActionModuleAttribute : Attribute
    {
        public int Order { get; }

        public PlanActionModuleAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
