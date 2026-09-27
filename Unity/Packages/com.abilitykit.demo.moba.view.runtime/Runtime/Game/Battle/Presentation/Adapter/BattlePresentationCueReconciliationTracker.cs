using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattlePresentationCueReconciliationTracker
    {
        private readonly PresentationCueReconciliationController<BattlePresentationCueRequestKey, byte> _controller;

        public BattlePresentationCueReconciliationTracker(int capacity = 1024)
        {
            _controller = new PresentationCueReconciliationController<BattlePresentationCueRequestKey, byte>(capacity);
        }

        public long ConfirmedCount => _controller.ConfirmedCount;
        public long CorrectedCount => _controller.CorrectedCount;
        public long RejectedCount => _controller.RejectedCount;
        public long StaleUpdateCount => _controller.StaleUpdateCount;

        public bool Accept(BattlePresentationCueRequestKey requestKey, in PresentationCueData data)
        {
            var update = new PresentationCueUpdate<BattlePresentationCueRequestKey, byte>(
                requestKey,
                PresentationCueSignal.None,
                BattlePresentationCueReconciliationMapper.ResolveOutcome(data.PredictionState),
                BattlePresentationCueReconciliationMapper.ResolveRevision(in data),
                _controller.CurrentGeneration,
                false,
                0);
            return _controller.Process(in update).Accepted;
        }
    }
}
