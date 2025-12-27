using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class AttackEventPublisher : MonoBehaviour
    {
        public KeyCode Key = KeyCode.Space;

        private void Update()
        {
            if (!Input.GetKeyDown(Key)) return;

            UnityGlobalEventBus.Instance.Publish(new TriggerEvent(
                id: "Attack",
                payload: null,
                args: new Dictionary<string, object>
                {
                    {"source", gameObject}
                }
            ));
        }
    }
}
