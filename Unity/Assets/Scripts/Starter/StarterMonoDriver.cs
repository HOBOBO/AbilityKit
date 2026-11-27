using AbilityKit.Starter.Examples;
using UnityEngine;

namespace AbilityKit.Starter
{
    public sealed class StarterMonoDriver : MonoBehaviour
    {
        private StarterFlowManager _starter;
        private bool _cleanupPending;

        private void Start()
        {
            _starter = new StarterFlowManager(new[] { new StarterLogContributor() });
            _starter.Finished += OnFinished;

            _starter.Start(new StarterArgs(onEnterGame: EnterGame));
        }

        private void EnterGame()
        {
            Debug.Log("[Starter] EnterGame invoked");
        }

        private void OnFinished(AbilityKit.Ability.Flow.FlowStatus status)
        {
            Debug.Log($"[Starter] Finished: {status}");
            _cleanupPending = true;
        }

        private void LateUpdate()
        {
            if (_cleanupPending) Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            _cleanupPending = false;
            var starter = _starter;
            _starter = null;
            if (starter == null) return;
            starter.Finished -= OnFinished;
            starter.Dispose();
        }
    }
}
