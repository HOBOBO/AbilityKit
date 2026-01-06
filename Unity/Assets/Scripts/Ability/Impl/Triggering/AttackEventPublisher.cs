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

            var args = PooledTriggerArgs.Rent();
            args["source"] = gameObject;
            UnityGlobalEventBus.Instance.Publish(new TriggerEvent(id: "Attack", payload: null, args: args));
        }
    }
}
