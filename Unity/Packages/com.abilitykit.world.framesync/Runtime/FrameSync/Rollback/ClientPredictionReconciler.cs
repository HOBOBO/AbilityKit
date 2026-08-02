using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    /// <summary>
    /// 【已废弃】配合 <see cref="ClientPredictionRunner"/> 的协调器。规范客户端预测栈为
    /// <c>com.abilitykit.host.extension</c> 的 <c>ClientPredictionDriverModule</c>。
    /// 本类无 demo 消费者，保留仅为过渡，后续版本将移除。
    /// </summary>
    [Obsolete("Use com.abilitykit.host.extension/ClientPredictionDriverModule instead. This world.framesync prediction reconciler has no consumer and will be removed in a future version.", false)]
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
