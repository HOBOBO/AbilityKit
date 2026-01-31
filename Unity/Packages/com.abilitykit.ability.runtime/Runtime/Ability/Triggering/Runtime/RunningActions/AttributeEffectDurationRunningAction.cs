using System;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.Common.Log;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class AttributeEffectDurationRunningAction : IRunningAction
    {
        private AttributeEffectHandle _handle;
        private float _remaining;
        private bool _done;

        public AttributeEffectDurationRunningAction(AttributeEffectHandle handle, float durationSeconds)
        {
            _handle = handle;
            _remaining = durationSeconds;
            if (durationSeconds <= 0f)
            {
                _done = true;
                Remove();
            }
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                _done = true;
                Remove();
            }
        }

        public void Cancel()
        {
            if (_done) return;
            _done = true;
            Remove();
        }

        public void Dispose()
        {
            Remove();
        }

        private void Remove()
        {
            if (_handle == null) return;

            try
            {
                _handle.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[AttributeEffectDurationRunningAction] handle dispose failed");
            }

            _handle = null;
        }
    }
}
