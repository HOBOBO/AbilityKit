using System.Threading.Channels;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplay;
using Orleans;

internal static class FrameCommitProbe
{
    private const byte Complete = 1;
    private const byte SkipWorld = 2;
    private const byte ThrowInWorld = 3;
    private const byte RejectInput = 4;
    private const byte Incomplete = 5;
    private const uint ProbeHash = 123;

    internal static ServerGameplayModuleCatalog CreateCatalog()
    {
        var catalog = ServerGameplayModuleCatalog.Default;
        var moba = catalog.ResolveModule(GameplayRoomTypes.Moba);
        var probeModule = new ServerGameplayModule(moba.Descriptor, moba.SyncDeclaration, moba.CreateRoomAdapter,
            _ => new ProbeAdapter(), moba.CreateWorldBlueprints()
                .Select(blueprint => (Func<IWorldBlueprint>)(() => blueprint)).ToArray());
        return new ServerGameplayModuleCatalog(catalog.Modules
            .Select(module => ReferenceEquals(module, moba) ? probeModule : module).ToArray());
    }

    internal static async Task RunAsync(IClusterClient client)
    {
        await VerifyGapAsync(client, 19193, new[] { Complete, SkipWorld, ThrowInWorld, Complete, RejectInput },
            new[] { ProbeHash, 0u, 0u, ProbeHash, ProbeHash }, verifyReplacement: true);
        await VerifyGapAsync(client, 19194, new[] { Complete, RejectInput, Complete },
            new[] { ProbeHash, ProbeHash, ProbeHash });
        await VerifyGapAsync(client, 19195, new[] { Complete, Incomplete, Complete },
            new[] { ProbeHash, ProbeHash, ProbeHash });
    }

    private static async Task VerifyGapAsync(IClusterClient client, ulong worldId, byte[] modes, uint[] expectedHashes,
        bool verifyReplacement = false)
    {
        const int tickRate = 20;
        var battleId = $"moba-frame-commit-probe-{Guid.NewGuid():N}";
        var host = client.GetGrain<IBattleLogicHostGrain>(battleId);
        var frames = client.GetGrain<IBattleFrameSyncGrain>(worldId.ToString());
        var observer = new ProbeObserver();
        var reference = client.CreateObjectReference<IFrameSyncObserver>(observer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            if (verifyReplacement)
            {
                await frames.GetMetricsAsync();
                await Task.Delay(150, timeout.Token);
                var inactive = await frames.GetMetricsAsync();
                Require(inactive.CurrentFrame == 0 && inactive.RecordingFrameCount == 0,
                    "Relay timer advanced or recorded frames before initialization.");
            }

            var initParams = new BattleInitParams
            {
                WorldId = worldId,
                TickRate = tickRate,
                RoomType = GameplayRoomTypes.Moba
            };
            var init = await host.InitializeBattleWithResultAsync(initParams, "probe");
            Require(init.Succeeded, $"Host initialization rejected: {init.Error}");
            var frameOptions = new FrameSyncStartOptions(worldId, worldId, tickRate, battleId,
                MobaSmokeConstants.DefaultSyncTemplateId, (int)ServerBattleRuntimeMode.BattleWorldWithFrameSync,
                EnableRecording: true) { InitialCheckpoint = init.InitialCheckpoint };
            Require(init.InitialCheckpoint != null && await host.GetCurrentFrameAsync() == 1,
                "The host did not initialize at an input-free frame zero.");
            var frameZero = await host.TickFrameAsync(worldId, 0, 1f / tickRate, new[] { Input(ThrowInWorld) });
            Require(!frameZero.WorldTicked && !frameZero.SettlementEligible && frameZero.StateHash == 0 &&
                await host.GetCurrentFrameAsync() == 1, "The host treated frame zero as a gameplay tick.");
            await frames.InitializeAsync(frameOptions);
            await frames.SubscribeAsync(reference);

            var first = (await frames.GetMetricsAsync()).CurrentFrame + tickRate;
            for (var index = 0; index < modes.Length; index++)
            {
                var frame = first + index;
                var accepted = await frames.SubmitInputWithResultAsync(worldId, frame, Input(modes[index]));
                Require(accepted.Accepted, $"Probe input rejected: frame={frame}, reason={accepted.Reason}");
            }

            for (var index = 0; index < modes.Length; index++)
            {
                var frame = first + index;
                FramePushedEvent push;
                do { push = await observer.Frames.Reader.ReadAsync(timeout.Token); } while (push.Frame < frame);
                Require(push.Frame == frame && push.WorldId == worldId, $"Missing broadcast for frame {frame}");
                Require(push.Inputs.Single().Payload.Single() == modes[index], $"Broadcast changed input for frame {frame}");
                Require(push.StateHash == expectedHashes[index], $"Unexpected diagnostic hash for frame {frame}: {push.StateHash}");
                Console.WriteLine($"FRAME_COMMIT_PROBE_PUSH frame={frame} mode={modes[index]} diagnosticHash={push.StateHash}");
            }

            var last = first + modes.Length - 1;
            var metrics = await frames.GetMetricsAsync();
            Require(metrics.SettlementGeneration == init.InitialCheckpoint!.RuntimeGeneration,
                "The relay settled frames against a different runtime generation.");
            Require(metrics.CurrentFrame >= last + 1 && metrics.SettlementGeneration != Guid.Empty &&
                metrics.LastSettledFrame == first,
                $"Settlement incorrectly crossed or failed to reach the prefix: expected={first}, actual={metrics.LastSettledFrame}.");
            await frames.InitializeAsync(frameOptions);
            var afterRetry = await frames.GetMetricsAsync();
            Require(afterRetry.SettlementGeneration == metrics.SettlementGeneration && afterRetry.LastSettledFrame == first,
                "An idempotent relay initialization changed the settlement prefix.");

            var late = await frames.SubmitInputWithResultAsync(worldId, first + 1, Input(modes[1]));
            Require(!late.Accepted && late.Reason == FrameInputSubmitReason.FrameAlreadyProcessed,
                "The relay cursor did not close the failed frame to late inputs.");
            var catchUp = await frames.RequestCatchUpAsync(new FrameSyncCatchUpRequest(worldId, worldId, first - 1, last));
            Require(catchUp != null && catchUp.StartFrame == first && catchUp.FrameInputs.Count == modes.Length &&
                catchUp.FrameInputs.Select(inputs => inputs.Single().Payload.Single()).SequenceEqual(modes),
                "CatchUp did not preserve the input sequence across the failed frames.");
            var coldCatchUp = await frames.RequestCatchUpAsync(new FrameSyncCatchUpRequest(worldId, worldId, -1, last));
            Require(coldCatchUp != null && coldCatchUp.StartFrame == 0 && coldCatchUp.FrameInputs.Count == last + 1 &&
                coldCatchUp.FrameInputs.Take(first).All(inputs => inputs.Count == 0) &&
                coldCatchUp.FrameInputs.Skip(first).Select(inputs => inputs.Single().Payload.Single()).SequenceEqual(modes),
                "Cold CatchUp did not preserve the empty frame zero and original input history.");
            if (verifyReplacement)
            {
                await host.DestroyAsync();
                var replacement = await host.InitializeBattleWithResultAsync(initParams, "probe");
                Require(replacement.Succeeded, $"Replacement host initialization rejected: {replacement.Error}");
                FrameSyncMetrics afterReplacement;
                do
                {
                    await Task.Delay(20, timeout.Token);
                    afterReplacement = await frames.GetMetricsAsync();
                } while (afterReplacement.SettlementGeneration == metrics.SettlementGeneration);
                Require(afterReplacement.SettlementGeneration != Guid.Empty && afterReplacement.LastSettledFrame == null,
                    "A replacement runtime inherited or rebuilt a settlement prefix without a matching checkpoint.");
                Require(replacement.InitialCheckpoint?.RuntimeGeneration == afterReplacement.SettlementGeneration,
                    "The replacement host did not supply its own initial checkpoint.");
                await frames.InitializeAsync(frameOptions with { InitialCheckpoint = replacement.InitialCheckpoint });
                var fresh = await frames.GetMetricsAsync();
                Require(fresh.SettlementGeneration == afterReplacement.SettlementGeneration && fresh.LastSettledFrame >= 0 &&
                    fresh.LastSettledFrame == fresh.CurrentFrame - 1 && fresh.CurrentFrame < first && fresh.TotalInputsReceived == 0,
                    "Trusted replacement initialization retained the previous generation's cursor or inputs.");
            }

            Console.WriteLine($"FRAME_COMMIT_PROBE_PASSED range={first}-{last} fault={modes[1]} settled={metrics.LastSettledFrame} lateInput={late.Reason} catchUpFrames={modes.Length} replacement={verifyReplacement}");
        }
        finally
        {
            await frames.DestroyAsync();
            await host.DestroyAsync();
            client.DeleteObjectReference<IFrameSyncObserver>(reference);
        }
    }

    private static FrameInputItem Input(byte mode) => new(1, 0, new[] { mode });

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ProbeObserver : IFrameSyncObserver
    {
        internal Channel<FramePushedEvent> Frames { get; } = Channel.CreateUnbounded<FramePushedEvent>();

        public void OnFramePushed(FramePushedEvent evt) => Frames.Writer.TryWrite(evt);
    }

    private sealed class ProbeAdapter : IBattleRuntimeAdapter
    {
        public string RoomType => GameplayRoomTypes.Moba;

        public IBattleRuntimeSession CreateSession(string battleId) => new ProbeSession();
    }

    private sealed class ProbeSession : IBattleRuntimeSession, IBattleRuntimeStateHashProvider, IBattleRuntimeFrameCompletion,
        IBattleRuntimeExternalFrameClock
    {
        private byte _mode;
        private int _nextFrame = 1;

        public bool HasInitialFrameCheckpoint => true;

        public bool TryPrepareExternalFrame(int frame, float fixedDeltaTime)
        {
            if (frame != _nextFrame) return false;
            _nextFrame++;
            return true;
        }

        public BattleRuntimeStartResult Start(BattleInitParams initParams) => BattleRuntimeStartResult.Success();

        public int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
        {
            _mode = inputs.Count == 0 ? Complete : (inputs.Single().Payload ??
                throw new InvalidOperationException("Frame commit probe input has no payload.")).Single();
            return _mode == RejectInput ? 0 : inputs.Count;
        }

        public bool Tick(int frame, int tickRate, float deltaTime) => _mode switch
        {
            SkipWorld => false,
            ThrowInWorld => throw new InvalidOperationException("Frame commit probe: injected world failure."),
            _ => true
        };

        public uint ComputeStateHash() => ProbeHash;

        public bool CanFinalizeFrame(int frame) => _mode is Complete or RejectInput;

        public BattleSnapshot? GetSnapshot(int frame) => null;

        public BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame) => null;

        public BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame) => throw new NotSupportedException();

        public BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame) => throw new NotSupportedException();

        public BattleDiagnosticEventsResult QueryDiagnosticEvents(BattleDiagnosticEventsQuery query) => throw new NotSupportedException();

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
