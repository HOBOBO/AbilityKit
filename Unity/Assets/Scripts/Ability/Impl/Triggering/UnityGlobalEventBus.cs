using AbilityKit.Triggering;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Triggering
{
    public static class UnityGlobalEventBus
    {
        public static readonly EventBus Instance = new EventBus();
    }
}
