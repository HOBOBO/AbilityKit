using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleHudSnapshotHandlers
    {
        public BattleHudSnapshotHandlers(
            Action<BattleEnterGameSnapshot> enterGame,
            Action<DamageEventData[]> damageEvents,
            Action<SkillStateData[]> skillStates,
            Action<PresentationCueData[]> presentationCues,
            Action<GameplayClockData> gameplayClock,
            Action<GameplayMatchData> gameplayMatch,
            Action<GameplayRespawnData[]> gameplayRespawn)
        {
            EnterGame = enterGame;
            DamageEvents = damageEvents;
            SkillStates = skillStates;
            PresentationCues = presentationCues;
            GameplayClock = gameplayClock;
            GameplayMatch = gameplayMatch;
            GameplayRespawn = gameplayRespawn;
        }

        public Action<BattleEnterGameSnapshot> EnterGame { get; }
        public Action<DamageEventData[]> DamageEvents { get; }
        public Action<SkillStateData[]> SkillStates { get; }
        public Action<PresentationCueData[]> PresentationCues { get; }
        public Action<GameplayClockData> GameplayClock { get; }
        public Action<GameplayMatchData> GameplayMatch { get; }
        public Action<GameplayRespawnData[]> GameplayRespawn { get; }
    }

    internal sealed class BattleHudSnapshotController : IDisposable
    {
        private readonly BattleHudSnapshotControllerFactory _factory;
        private readonly BattleSubscriptionGroup _subscriptions;
        private AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher _dispatcher;

        public BattleHudSnapshotController(BattleHudSnapshotControllerFactory factory = null)
        {
            _factory = factory ?? new BattleHudSnapshotControllerFactory();
            _subscriptions = _factory.CreateSubscriptions();
        }

        public bool IsBound { get; private set; }

        public bool IsBoundTo(IBattleGameplayPresentationSource ctx) =>
            IsBound && ctx != null && ctx.TryGetFrameSnapshots(out var snapshots) &&
            ReferenceEquals(_dispatcher, snapshots);

        public bool Bind(
            IBattleGameplayPresentationSource ctx,
            Action<BattleEnterGameSnapshot> enterGameReceived,
            Action<DamageEventData[]> damageEventsReceived,
            Action<SkillStateData[]> skillStatesReceived,
            Action<PresentationCueData[]> presentationCuesReceived = null,
            Action<GameplayClockData> gameplayClockReceived = null,
            Action<GameplayMatchData> gameplayMatchReceived = null,
            Action<GameplayRespawnData[]> gameplayRespawnReceived = null)
        {
            return Bind(ctx, new BattleHudSnapshotHandlers(
                enterGameReceived, damageEventsReceived, skillStatesReceived,
                presentationCuesReceived, gameplayClockReceived, gameplayMatchReceived,
                gameplayRespawnReceived));
        }

        public bool Bind(IBattleGameplayPresentationSource ctx, in BattleHudSnapshotHandlers handlers)
        {
            ClearSubscriptions();
            if (ctx == null || !ctx.TryGetFrameSnapshots(out var snapshots)) return false;

            _factory.BindSnapshots(_subscriptions, snapshots, in handlers);
            _dispatcher = snapshots;
            IsBound = true;
            return true;
        }

        public void Dispose() => ClearSubscriptions();

        private void ClearSubscriptions()
        {
            _subscriptions.Clear();
            _dispatcher = null;
            IsBound = false;
        }
    }

    internal sealed class BattleHudSnapshotControllerFactory
    {
        public BattleSubscriptionGroup CreateSubscriptions() => new BattleSubscriptionGroup(7);

        public void BindSnapshots(
            BattleSubscriptionGroup subscriptions,
            AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher snapshots,
            in BattleHudSnapshotHandlers handlers)
        {
            if (subscriptions == null || snapshots == null) return;
            var callbacks = handlers;

            if (callbacks.EnterGame != null)
                subscriptions.Add(snapshots.Subscribe<BattleEnterGameSnapshot>(
                    MobaOpCodes.Snapshot.EnterGame, (_, data) => callbacks.EnterGame(data)));
            if (callbacks.DamageEvents != null)
                subscriptions.Add(snapshots.Subscribe<DamageEventData[]>(
                    MobaOpCodes.Snapshot.DamageEvent, (_, data) => callbacks.DamageEvents(data)));
            if (callbacks.SkillStates != null)
                subscriptions.Add(snapshots.Subscribe<SkillStateData[]>(
                    MobaOpCodes.Snapshot.SkillState, (_, data) => callbacks.SkillStates(data)));
            if (callbacks.PresentationCues != null)
                subscriptions.Add(snapshots.Subscribe<PresentationCueData[]>(
                    MobaOpCodes.Snapshot.PresentationCue, (_, data) => callbacks.PresentationCues(data)));
            if (callbacks.GameplayClock != null)
                subscriptions.Add(snapshots.Subscribe<GameplayClockData>(
                    GameplayClockSnapshotRoute.OpCode, (_, data) => callbacks.GameplayClock(data)));
            if (callbacks.GameplayMatch != null)
                subscriptions.Add(snapshots.Subscribe<GameplayMatchData>(
                    GameplayMatchSnapshotRoute.OpCode, (_, data) => callbacks.GameplayMatch(data)));
            if (callbacks.GameplayRespawn != null)
                subscriptions.Add(snapshots.Subscribe<GameplayRespawnData[]>(
                    GameplayRespawnSnapshotRoute.OpCode, (_, data) => callbacks.GameplayRespawn(data)));
        }
    }
}
