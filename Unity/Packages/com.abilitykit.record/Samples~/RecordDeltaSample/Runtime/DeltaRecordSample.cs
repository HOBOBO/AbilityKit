using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Record.Adapters.EventCodecs;
using AbilityKit.Core.Common.Record.Adapters.Replay;
using AbilityKit.Core.Common.Record.Core;
using UnityEngine;

namespace AbilityKit.Record.Samples
{
    public sealed class DeltaRecordSample : MonoBehaviour
    {
        [ContextMenu("Run Delta Record Sample")]
        public void Run()
        {
            var profile = new RecordProfile
            {
                EnableInputs = false,
                EnableStateHash = false,
                EnableSnapshots = false,
            };

            var serializer = new JsonRecordContainerSerializer();
            var trackFactory = new DefaultRecordTrackFactory();

            using var session = new RecordSession(
                profile,
                container: null,
                serializer,
                writerFactory: trackFactory,
                readerFactory: trackFactory);

            var trackId = RecordTrackId.FromName("world.delta.sample");
            if (!session.TryGetWriter(trackId, out var writer) || writer == null)
            {
                Debug.LogError("[DeltaRecordSample] Failed to get track writer");
                return;
            }

            var frame = new FrameIndex(10);
            var deltaPayload = new byte[] { 1, 2, 3, 4 };
            var delta = new WorldStateSnapshot(opCode: 9001, payload: deltaPayload);
            WorldDeltaEventCodec.Write(writer, frame, in delta);

            var bytes = session.Serialize();

            using var replaySession = new RecordSession(
                profile,
                container: null,
                serializer,
                writerFactory: trackFactory,
                readerFactory: trackFactory);

            if (!replaySession.TryLoad(bytes))
            {
                Debug.LogError("[DeltaRecordSample] Failed to load serialized record container");
                return;
            }

            if (!replaySession.TryGetReader(trackId, out var reader) || reader == null)
            {
                Debug.LogError("[DeltaRecordSample] Failed to get track reader");
                return;
            }

            var handler = new TypedReplayEventHandler
            {
                OnDelta = d => Debug.Log($"[DeltaRecordSample] OnDelta frame={frame.Value}, opCode={d.OpCode}, payloadLen={d.Payload?.Length ?? 0}"),
            };

            var clock = new FixedStepReplayClock(fixedDelta: 1f);
            clock.Reset(new FrameIndex(9));
            var controller = new BasicReplayController(clock, reader, handler);

            controller.Play();
            controller.Tick(1f);
        }
    }
}
