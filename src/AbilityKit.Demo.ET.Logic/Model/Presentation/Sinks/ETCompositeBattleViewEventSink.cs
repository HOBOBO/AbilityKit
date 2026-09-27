using System;
using AbilityKit.Demo.Moba.Share;

namespace ET.Logic
{
    /// <summary>
    /// Dispatches battle view events to multiple ET-side sinks without mixing their responsibilities.
    /// </summary>
    public sealed class ETCompositeBattleViewEventSink : IBattleViewEventSink
    {
        private readonly IBattleViewEventSink[] _sinks;

        public ETCompositeBattleViewEventSink(params IBattleViewEventSink[] sinks)
        {
            _sinks = sinks ?? Array.Empty<IBattleViewEventSink>();
        }

        public void OnEnterGameSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnEnterGameSnapshot(in snapshot);
            }
        }

        public void OnActorTransformSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnActorTransformSnapshot(in snapshot);
            }
        }

        public void OnActorDespawnSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnActorDespawnSnapshot(in snapshot);
            }
        }

        public void OnSkillStateSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnSkillStateSnapshot(in snapshot);
            }
        }

        public void OnProjectileEventSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnProjectileEventSnapshot(in snapshot);
            }
        }

        public void OnAreaEventSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnAreaEventSnapshot(in snapshot);
            }
        }

        public void OnDamageEventSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnDamageEventSnapshot(in snapshot);
            }
        }

        public void OnPresentationCueSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnPresentationCueSnapshot(in snapshot);
            }
        }

        public void OnStateHashSnapshot(in FrameSnapshotData snapshot)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnStateHashSnapshot(in snapshot);
            }
        }

        public void OnTriggerEvent(in TriggerEventData evt)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnTriggerEvent(in evt);
            }
        }

        public void OnBattleStart(int frameIndex)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnBattleStart(frameIndex);
            }
        }

        public void OnBattleEnd(int frameIndex, int winTeamId)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnBattleEnd(frameIndex, winTeamId);
            }
        }

        public void OnFrameSyncComplete(int frameIndex)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.OnFrameSyncComplete(frameIndex);
            }
        }
    }
}
