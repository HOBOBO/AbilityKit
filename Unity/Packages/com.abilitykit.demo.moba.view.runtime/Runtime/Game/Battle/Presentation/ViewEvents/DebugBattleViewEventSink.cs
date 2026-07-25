using AbilityKit.Ability.Host;
using AbilityKit.Ability.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class DebugBattleViewEventSink : IBattleViewEventSink
    {
        private readonly DebugBattleViewEventLineBuffer _lines;
        private readonly DebugBattleViewEventFormatter _formatter;

        public int Total => _lines.Total;

        public DebugBattleViewEventSink(
            int maxLines,
            DebugBattleViewEventFormatter formatter = null,
            DebugBattleViewEventSinkFactory factory = null)
        {
            factory ??= new DebugBattleViewEventSinkFactory();

            _lines = factory.CreateLines(maxLines);
            _formatter = formatter ?? factory.CreateFormatter();
        }

        public string[] GetRecentLines()
        {
            return _lines.GetRecentLines();
        }

        public void OnTriggerEvent(in TriggerEvent evt)
        {
            _lines.Push(_formatter.FormatTrigger(in evt));
        }

        public void OnSummonEvent(string eventId, in DemoMobaSummonEventPayload payload)
        {
            _lines.Push($"[Summon] {eventId}: summonActorId={payload.SummonActorId}, summonId={payload.SummonId}, owner={payload.OwnerActorId}, reason={payload.Reason}");
        }

        public void OnEnterGameSnapshot(ISnapshotEnvelope packet, EnterMobaGameRes res)
        {
            _lines.Push(_formatter.FormatEnterGame(in res));
        }

        public void OnActorTransformSnapshot(ISnapshotEnvelope packet, MobaActorTransformSnapshotEntry[] entries)
        {
            _lines.Push(_formatter.FormatActorTransforms(entries));
        }

        public void OnProjectileEventSnapshot(ISnapshotEnvelope packet, MobaProjectileEventSnapshotEntry[] entries)
        {
            _lines.Push(_formatter.FormatProjectiles(entries));
        }

        public void OnAreaEventSnapshot(ISnapshotEnvelope packet, MobaAreaEventSnapshotEntry[] entries)
        {
            _lines.Push(_formatter.FormatAreas(entries));
        }

        public void OnDamageEventSnapshot(ISnapshotEnvelope packet, MobaDamageEventSnapshotEntry[] entries)
        {
            _lines.Push(_formatter.FormatDamages(entries));
        }

        public void OnPresentationCueSnapshot(ISnapshotEnvelope packet, PresentationCueData[] entries)
        {
            _lines.Push(_formatter.FormatPresentationCues(entries));
        }

        public void Tick()
        {
        }
    }

    internal sealed class DebugBattleViewEventSinkFactory
    {
        public DebugBattleViewEventLineBuffer CreateLines(int maxLines)
        {
            return new DebugBattleViewEventLineBuffer(maxLines);
        }

        public DebugBattleViewEventFormatter CreateFormatter()
        {
            return new DebugBattleViewEventFormatter();
        }
    }
}
