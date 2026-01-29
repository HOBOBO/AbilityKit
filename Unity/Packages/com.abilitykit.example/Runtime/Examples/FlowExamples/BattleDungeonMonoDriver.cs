using AbilityKit.Ability.Impl.FlowExamples;
using AbilityKit.Ability.Flow;
using UnityEngine;

namespace AbilityKit.Ability.Impl
{
    public sealed class BattleDungeonMonoDriver : MonoBehaviour
    {
        public float RunSeconds = 3f;

        private BattleDungeonFlowManager _manager;
        private bool _running;

        private void Start()
        {
            _manager = new BattleDungeonFlowManager();
            _manager.Finished += OnFinished;
            _manager.Start(new AbilityKit.Ability.World.Abstractions.WorldId("room_1"), RunSeconds);
            _running = true;
        }

        private void OnFinished(FlowStatus status)
        {
            _running = false;

            if (_manager != null)
            {
                _manager.Finished -= OnFinished;
                _manager.Dispose();
                _manager = null;
            }
        }

        private void OnDestroy()
        {
            _running = false;
            if (_manager != null)
            {
                _manager.Finished -= OnFinished;
                _manager.Dispose();
                _manager = null;
            }
        }
    }
}
