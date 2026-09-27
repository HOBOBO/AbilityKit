using System;
using AbilityKit.Demo.Moba.Share;

namespace ET.Logic
{
    /// <summary>
    /// Optional demo automation sink that listens to battle snapshots without coupling test setup to presentation.
    /// </summary>
    public sealed class ETBattleAutomationSnapshotSink : IBattleViewEventSink
    {
        private readonly Scene _scene;
        private readonly ETBattleComponent _battleComponent;

        public ETBattleAutomationSnapshotSink(Scene scene, ETBattleComponent battleComponent)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _battleComponent = battleComponent ?? throw new ArgumentNullException(nameof(battleComponent));
        }

        public void OnEnterGameSnapshot(in FrameSnapshotData snapshot)
        {
            ETBattleAutomationInstaller.InitializeFromEnterGameSnapshot(_scene, _battleComponent, in snapshot);
        }

        public void OnActorTransformSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnActorDespawnSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnSkillStateSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnProjectileEventSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnAreaEventSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnDamageEventSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnPresentationCueSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnStateHashSnapshot(in FrameSnapshotData snapshot)
        {
        }

        public void OnTriggerEvent(in TriggerEventData evt)
        {
        }

        public void OnBattleStart(int frameIndex)
        {
        }

        public void OnBattleEnd(int frameIndex, int winTeamId)
        {
        }

        public void OnFrameSyncComplete(int frameIndex)
        {
        }
    }
}
