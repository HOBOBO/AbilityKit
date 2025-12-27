using AbilityKit.Starter.Examples;
using UnityEngine;

namespace AbilityKit.Starter
{
    public sealed class StarterMonoDriver : MonoBehaviour
    {
        private StarterFlowManager _starter;

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
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_starter == null) return;
            _starter.Finished -= OnFinished;
            _starter.Dispose();
            _starter = null;
        }
    }
}
