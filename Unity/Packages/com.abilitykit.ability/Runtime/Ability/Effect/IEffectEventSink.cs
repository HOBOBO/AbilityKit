using System;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Share.Effect
{
    public interface IEffectEventSink
    {
        void Publish(string eventId, object payload = null, Action<PooledTriggerArgs> fillArgs = null);
    }
}
