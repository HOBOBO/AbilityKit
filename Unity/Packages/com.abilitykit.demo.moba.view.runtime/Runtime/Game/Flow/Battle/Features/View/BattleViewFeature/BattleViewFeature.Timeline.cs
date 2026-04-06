using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.View;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private int _lastAlignedFrame = int.MinValue;

        private void RegisterAllSeekables()
        {
            if (_timeline == null || _binder == null) return;
            _timeline.Clear();
            _binder.ForEachShellGameObject((actorId, entityId, go) => RegisterSeekablesOnGameObject(go));
            _lastAlignedFrame = int.MinValue;
        }

        private void RegisterSeekablesForEntity(EC.IEntityId id)
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
            if (_timeline == null) return;
            if (_ctx == null) return;

            var frame = _ctx.LastFrame;
            if (frame == _lastAlignedFrame) return;

            var tickRate = _ctx.Plan.TickRate;
            var secondsPerFrame = tickRate > 0 ? 1f / tickRate : 0f;
            _timeline.SeekAll(frame, secondsPerFrame);

            _lastAlignedFrame = frame;

            var worldId = _ctx.RuntimeWorldId;
            _events?.Publish(new ViewFrameAlignedEvent(isConfirmed: false, worldId: worldId, frame: frame));
            _events?.Flush();
        }
    }
}
