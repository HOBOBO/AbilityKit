using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering
{
    public readonly struct TriggerEvent
    {
        public readonly string Id;
        public readonly object Payload;
        public readonly IReadOnlyDictionary<string, object> Args;

        public TriggerEvent(string id, object payload = null, IReadOnlyDictionary<string, object> args = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Payload = payload;
            Args = args;
        }
    }
}
