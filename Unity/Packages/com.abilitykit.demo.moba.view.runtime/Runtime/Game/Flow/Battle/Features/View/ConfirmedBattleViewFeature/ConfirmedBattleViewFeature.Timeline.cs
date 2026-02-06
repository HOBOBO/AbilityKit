using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.View;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature
    {
        private int _lastAlignedFrame = int.MinValue;

        private void RegisterAllSeekables()
        {
            if (_timeline == null || _binder == null) return;
            _timeline.Clear();
            _binder.ForEachShellGameObject((actorId, entityId, go) => RegisterSeekablesOnGameObject(go));
            _lastAlignedFrame = int.MinValue;
        }

        private void RegisterSeekablesForEntity(EC.EntityId id)
        {
            if (_timeline == null || _binder == null) return;
            if (!_binder.TryGetShellGameObject(id, out var go)) return;
            RegisterSeekablesOnGameObject(go);
            _lastAlignedFrame = int.MinValue;
        }

        private void RegisterSeekablesOnGameObject(GameObject go)
        {
            if (_timeline == null) return;
            if (go == null) return;

            var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (monos == null || monos.Length == 0) return;

            for (int i = 0; i < monos.Length; i++)
            {
                if (monos[i] is IFrameSeekableView seekable)
                {
                    _timeline.Register(seekable);
                }
            }
        }

        private void SeekAllToCurrentFrame()
        {
            if (_timeline == null || _confirmedCtx == null) return;
            var frame = _confirmedCtx.LastFrame;
            if (frame == _lastAlignedFrame) return;

            var tickRate = _confirmedCtx.Plan.TickRate;
            var secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            _timeline.SeekAll(frame, secondsPerFrame);

            _lastAlignedFrame = frame;

            var worldId = _confirmedCtx.RuntimeWorldId;
            _events?.Publish(new ViewFrameAlignedEvent(isConfirmed: true, worldId: worldId, frame: frame));
            _events?.Flush();
        }
    }
}
