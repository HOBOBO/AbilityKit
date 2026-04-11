using AbilityKit.Ability.Triggering;
using UnityEngine;

namespace AbilityKit.Examples.Triggering
{
    public sealed class AttackEventPublisher : MonoBehaviour
    {
        public KeyCode Key = KeyCode.Space;

        private void Update()
        {
            if (!Input.GetKeyDown(Key)) return;

            var args = PooledTriggerArgs.Rent();
            args["source"] = gameObject;
            GlobalEventBus.Instance.Publish(new TriggerEvent(id: "Attack", payload: null, args: args));
        }
    }
}
