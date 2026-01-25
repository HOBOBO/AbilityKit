using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class ClientPredictionReconciler
    {
        public Action<FrameIndex> OnRollbackRequested;

        private readonly WorldStateHashRingBuffer _predicted;

        public ClientPredictionReconciler(WorldStateHashRingBuffer predicted)
        {
            _predicted = predicted ?? throw new ArgumentNullException(nameof(predicted));
        }

        public void RecordPredictedHash(FrameIndex frame, WorldStateHash hash)
        {
            _predicted.Store(frame, hash);
        }

        public bool OnAuthoritativeHash(FrameIndex frame, WorldStateHash authoritative)
        {
            if (!_predicted.TryGet(frame, out var predicted))
            {
                return false;
            }

            if (predicted.Value == authoritative.Value)
            {
                return false;
            }

            OnRollbackRequested?.Invoke(frame);
            return true;
        }

        public void Clear()
        {
            _predicted.Clear();
        }
    }
}
