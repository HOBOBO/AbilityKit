using AbilityKit.Ability.Flow;
using UnityEngine;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public sealed class FlowControlBlocksDemoDriver : MonoBehaviour
    {
        public bool UseRace = true;

        private FlowSession _session;

        private void Start()
        {
            _session = new FlowSession();
            _session.Start(FlowControlBlocksDemo.Build(UseRace));
        }

        private void Update()
        {
            if (_session == null) return;
            if (_session.Status != FlowStatus.Running) return;
            _session.Step(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
