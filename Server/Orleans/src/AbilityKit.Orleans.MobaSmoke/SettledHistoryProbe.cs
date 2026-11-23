using System.Text.Json;
using System.Threading.Channels;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Orleans;

internal static class SettledHistoryProbe
{
    internal static async Task RunAsync(IClusterClient client)
    {
        const ulong world = 505601;
        const int tickRate = 20;
        var battleId = $"moba-settled-history-{Guid.NewGuid():N}";
        var host = client.GetGrain<IBattleLogicHostGrain>(battleId);
        var relay = client.GetGrain<IBattleFrameSyncGrain>(world.ToString());
        var observer = new Observer();
        var reference = client.CreateObjectReference<ISettledFrameSyncObserver>(observer);
        var legacyReference = client.CreateObjectReference<IFrameSyncObserver>(new LegacyObserver());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var init = await host.InitializeBattleWithResultAsync(new BattleInitParams
            {
                WorldId = world, TickRate = tickRate, RoomType = GameplayRoomTypes.Moba, MapId = 1, GameplayId = 1, RandomSeed = 12345,
                Players = [new PlayerInitInfo
                {
                    PlayerId = 1, ActorId = 1, HeroId = 3003, TeamId = 1, Level = 1, AttributeTemplateId = 3003,
                    BasicAttackSkillId = 30030101, SkillIds = [30030201, 30030301, 30030401, 30030501]
                }]
            }, "probe");
            Require(init.Succeeded && init.InitialCheckpoint != null, $"Host initialization rejected: {init.Error}");
            var generation = init.InitialCheckpoint!.RuntimeGeneration;
            var caster = (await host.GetSnapshotAsync())!.Actors.Single(actor => actor.ActorId == 1);
            var options = new FrameSyncStartOptions(world, world, tickRate, battleId, MobaSmokeConstants.DefaultSyncTemplateId,
                (int)ServerBattleRuntimeMode.BattleWorldWithFrameSync, EnableRecording: true)
            { UseSettledFrames = true, InitialCheckpoint = init.InitialCheckpoint };

            await relay.InitializeAsync(options);
            Require(!await relay.SubscribeSettledAsync(Guid.NewGuid(), reference), "Subscribed with a stale generation.");
            Require(await relay.SubscribeSettledAsync(generation, reference), "Settled subscription failed.");
            await RequireInvalidOperation(() => relay.SubscribeAsync(legacyReference));
            await RequireInvalidOperation(() => relay.InitializeAsync(options with { UseSettledFrames = false }));

            var frame = (await relay.GetMetricsAsync()).CurrentFrame + tickRate / 2;
            var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
            var accepted = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: frame,
                inputSequence: 1, predictionKey: 37, entityVersion: 1);
            var rejected = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: frame,
                inputSequence: 2, predictionKey: 38, entityVersion: 1, interruptEpoch: 99);
            var acceptedInput = new FrameInputItem(1, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in accepted));
            var rejectedInput = new FrameInputItem(1, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in rejected));
            var legacy = await relay.SubmitInputWithResultAsync(world, frame, acceptedInput);
            Require(!legacy.Accepted && legacy.Reason == FrameInputSubmitReason.SettlementModeMismatch, "Legacy input entered settled battle.");
            foreach (var input in new[] { acceptedInput, acceptedInput, rejectedInput })
                Require((await relay.SubmitSettledInputAsync(world, generation, frame, input)).Accepted, "Probe input was not queued.");
            var fault = frame + 1;
            Require((await relay.SubmitSettledInputAsync(world, generation, fault,
                new FrameInputItem(1, ushort.MaxValue, []))).Accepted, "Fault input was not queued.");

            FrameSyncSettledFrame live;
            do { live = await observer.Frames.Reader.ReadAsync(timeout.Token); } while (live.Settlement.Frame < frame);
            Require(live.Settlement.Frame == frame && live.Settlement.RuntimeGeneration == generation &&
                live.Version == FrameSyncSettledFrame.CurrentVersion && live.WorldId == world && live.BattleId == battleId, "Live frame identity mismatch.");
            var commands = live.Settlement.Commands;
            var decisions = live.Settlement.Decisions.Where(decision => decision.PredictionKey > 0).ToArray();
            Require(commands.Select(item => item.Disposition).SequenceEqual(new[]
                { SettledCommandDisposition.Executed, SettledCommandDisposition.Duplicate, SettledCommandDisposition.Rejected }), "Execution disposition mismatch.");
            Require(commands[0].ExecutedPayload.SequenceEqual(acceptedInput.Payload) && commands[1].ExecutedPayload.IsEmpty &&
                commands[2].ExecutedPayload.IsEmpty && decisions.Select(decision => decision.Ordinal).SequenceEqual(new[] { 0, 1, 2 }) &&
                decisions[0].AuthoritySequence == decisions[1].AuthoritySequence && !decisions[2].Accepted, "Action decisions or payload mismatch.");

            FrameSyncMetrics status;
            do
            {
                await Task.Delay(25, timeout.Token);
                status = await relay.GetMetricsAsync();
            } while (status.SettlementFaultFrame == null);
            Require(status.SettlementFaultFrame == fault && status.LastSettledFrame == frame && status.CurrentFrame == fault &&
                status.SettlementGeneration == generation && !observer.Frames.Reader.TryRead(out _), "Settlement crossed the fault frame.");
            Require(await host.GetCurrentFrameAsync() == fault, "Host advanced despite the failed settlement.");
            Require((await relay.SubmitSettledInputAsync(world, generation, fault + 1, acceptedInput)).Reason == FrameInputSubmitReason.SettlementFaulted,
                "Faulted session still accepts input.");
            Require(await relay.RequestCatchUpAsync(new FrameSyncCatchUpRequest(world, world, frame - 1, frame)) == null &&
                await relay.DumpRecordingAsync() == null, "Legacy history exposed settled frames.");
            Require(await relay.RequestSettledCatchUpAsync(new FrameSyncSettledCatchUpRequest(world, world, generation, frame - 1, fault)) == null,
                "CatchUp hid the fault as an empty or clamped frame.");

            var recent = (await relay.RequestSettledCatchUpAsync(new FrameSyncSettledCatchUpRequest(world, world, generation, frame - 1, frame)))!;
            var cold = (await relay.RequestSettledCatchUpAsync(new FrameSyncSettledCatchUpRequest(world, world, generation, -1, frame)))!;
            var recording = (await relay.DumpSettledRecordingAsync())!;
            Require(cold.Frames[0].Settlement.IsInitialCheckpoint && cold.Frames[0].Settlement.Frame == 0 && cold.Frames.Length == frame + 1 &&
                recording.FaultFrame == fault && recording.LastSettledFrame == frame, "Recording metadata mismatch.");
            var liveContent = JsonSerializer.Serialize(live);
            Require(JsonSerializer.Serialize(recent.Frames.Single()) == liveContent && JsonSerializer.Serialize(cold.Frames[^1]) == liveContent &&
                JsonSerializer.Serialize(recording.Frames[^1]) == liveContent, "Live, recent, cold and recorded data disagree.");
            await relay.InitializeAsync(options);
            Require((await relay.GetMetricsAsync()).SettlementFaultFrame == fault, "Idempotent initialization cleared a fault.");
            Console.WriteLine($"MOBA_SETTLED_HISTORY_PASSED Frame={frame} Fault={fault} Recorded={recording.Frames.Length} Decisions={decisions.Length}");
        }
        finally
        {
            await relay.DestroyAsync();
            await host.DestroyAsync();
            client.DeleteObjectReference<ISettledFrameSyncObserver>(reference);
            client.DeleteObjectReference<IFrameSyncObserver>(legacyReference);
        }
    }

    private static async Task RequireInvalidOperation(Func<Task> invoke)
    {
        try { await invoke(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An incompatible operation was not rejected.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Observer : ISettledFrameSyncObserver
    {
        public Channel<FrameSyncSettledFrame> Frames { get; } = Channel.CreateUnbounded<FrameSyncSettledFrame>();
        public void OnSettledFramePushed(FrameSyncSettledFrame frame) => Frames.Writer.TryWrite(frame);
    }

    private sealed class LegacyObserver : IFrameSyncObserver
    {
        public void OnFramePushed(FramePushedEvent evt) { }
    }
}
