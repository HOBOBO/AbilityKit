using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSnapshotController : IDisposable
    {
        private readonly BattleHudSnapshotControllerFactory _factory;
        private readonly BattleSubscriptionGroup _subscriptions;

        private Action<BattleEnterGameSnapshot> _enterGameReceived;
        private Action<DamageEventData[]> _damageEventsReceived;
        private Action<SkillStateData[]> _skillStatesReceived;
        private Action<PresentationCueData[]> _presentationCuesReceived;

        public BattleHudSnapshotController(BattleHudSnapshotControllerFactory factory = null)
        {
            _factory = factory ?? new BattleHudSnapshotControllerFactory();
            _subscriptions = _factory.CreateSubscriptions();
        }

        public bool IsBound { get; private set; }

        public bool Bind(
            BattleContext ctx,
            Action<BattleEnterGameSnapshot> enterGameReceived,
            Action<DamageEventData[]> damageEventsReceived,
            Action<SkillStateData[]> skillStatesReceived,
            Action<PresentationCueData[]> presentationCuesReceived = null)
        {
            ClearSubscriptions();

            _enterGameReceived = enterGameReceived;
            _damageEventsReceived = damageEventsReceived;
            _skillStatesReceived = skillStatesReceived;
            _presentationCuesReceived = presentationCuesReceived;

            if (ctx == null) return false;
            if (!ctx.TryGetFrameSnapshots(out var snapshots)) return false;

            _factory.BindSnapshots(
                _subscriptions,
                snapshots,
                OnEnterGameSnapshot,
                OnDamageEventSnapshot,
                OnSkillStateSnapshot,
                _presentationCuesReceived != null ? OnPresentationCueSnapshot : (Action<ISnapshotEnvelope, PresentationCueData[]>)null);
            IsBound = true;
            return true;
        }

        public void Dispose()
        {
            Clear();
        }

        private void Clear()
        {
            ClearSubscriptions();
            _enterGameReceived = null;
            _damageEventsReceived = null;
            _skillStatesReceived = null;
            _presentationCuesReceived = null;
        }

        private void ClearSubscriptions()
        {
            _subscriptions.Clear();
            IsBound = false;
        }

        private void OnEnterGameSnapshot(ISnapshotEnvelope packet, BattleEnterGameSnapshot res)
        {
            _enterGameReceived?.Invoke(res);
        }

        private void OnDamageEventSnapshot(ISnapshotEnvelope packet, DamageEventData[] entries)
        {
            _damageEventsReceived?.Invoke(entries);
        }

        private void OnSkillStateSnapshot(ISnapshotEnvelope packet, SkillStateData[] entries)
        {
            _skillStatesReceived?.Invoke(entries);
        }

        private void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries)
        {
            _presentationCuesReceived?.Invoke(entries);
        }
    }

    internal sealed class BattleHudSnapshotControllerFactory
    {
        public BattleSubscriptionGroup CreateSubscriptions()
        {
            return new BattleSubscriptionGroup(4);
        }

        public void BindSnapshots(
            BattleSubscriptionGroup subscriptions,
            AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher snapshots,
            Action<ISnapshotEnvelope, BattleEnterGameSnapshot> enterGameReceived,
            Action<ISnapshotEnvelope, DamageEventData[]> damageEventsReceived,
            Action<ISnapshotEnvelope, SkillStateData[]> skillStatesReceived,
            Action<ISnapshotEnvelope, PresentationCueData[]> presentationCuesReceived = null)
        {
            if (subscriptions == null) return;
            if (snapshots == null) return;

            subscriptions.Add(snapshots.Subscribe<BattleEnterGameSnapshot>(
                MobaOpCodes.Snapshot.EnterGame,
                enterGameReceived));
            subscriptions.Add(snapshots.Subscribe<DamageEventData[]>(
                MobaOpCodes.Snapshot.DamageEvent,
                damageEventsReceived));
            subscriptions.Add(snapshots.Subscribe<SkillStateData[]>(
                MobaOpCodes.Snapshot.SkillState,
                skillStatesReceived));
            if (presentationCuesReceived != null)
            {
                subscriptions.Add(snapshots.Subscribe<PresentationCueData[]>(
                    MobaOpCodes.Snapshot.PresentationCue,
                    presentationCuesReceived));
            }
        }
    }
}
