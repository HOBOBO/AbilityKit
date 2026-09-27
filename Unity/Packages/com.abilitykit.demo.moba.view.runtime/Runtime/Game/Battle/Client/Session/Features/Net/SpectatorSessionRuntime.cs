using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync.CatchUp;
using AbilityKit.Ability.Host.Extensions.FrameSync.Spectator;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;

namespace AbilityKit.Game.Flow
{
    internal sealed class SpectatorSessionRuntime : IDisposable
    {
        private sealed class StartOperation
        {
            internal int Generation;
            internal INetworkClient Client;
            internal CancellationTokenSource Cancellation;
            internal CancellationToken CancellationToken;
            internal Action<uint, byte[]> PushHandler;
            internal SpectatorWorldDriver Driver;
            internal bool IsSubscribed;
            internal bool IsCancellationRequested;
            internal bool IsCancellationDisposed;
            internal Task StartTask;
        }

        private readonly Func<SpectatorWorldDriver> _driverFactory;
        private readonly object _operationGate = new object();
        private readonly SemaphoreSlim _startGate = new SemaphoreSlim(1, 1);
        private StartOperation _operation;
        private SpectatorWorldDriver _driver;
        private int _generation;
        private Task _pendingStopTask = Task.CompletedTask;

        internal SpectatorSessionRuntime(Func<SpectatorWorldDriver> driverFactory = null)
        {
            _driverFactory = driverFactory ?? (() => new SpectatorWorldDriver());
        }

        internal SpectatorWorldDriver Driver => _driver;
        internal IWorld World => _driver?.World;
        internal bool IsStarting => _operation != null && _driver == null;
        internal bool IsSpectating => _driver != null;

        internal Task StartAsync(
            INetworkClient client,
            ulong roomId,
            Func<IWorld> worldFactory)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (worldFactory == null) throw new ArgumentNullException(nameof(worldFactory));

            return StartReplacingAsync(client, roomId, worldFactory);
        }

        private async Task StartReplacingAsync(
            INetworkClient client,
            ulong roomId,
            Func<IWorld> worldFactory)
        {
            Task startTask;
            await _startGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCurrentOperationAsync().ConfigureAwait(false);

                var cancellation = new CancellationTokenSource();
                var operation = new StartOperation
                {
                    Generation = ++_generation,
                    Client = client,
                    Cancellation = cancellation,
                    CancellationToken = cancellation.Token,
                };
                operation.PushHandler = (opCode, payload) => HandlePush(operation, opCode, payload);

                _operation = operation;
                client.OnServerPush += operation.PushHandler;
                operation.IsSubscribed = true;

                operation.StartTask = StartCoreAsync(operation, roomId, worldFactory);
                startTask = operation.StartTask;
            }
            finally
            {
                _startGate.Release();
            }

            await startTask.ConfigureAwait(false);
        }

        internal void Tick(int stepsBudget)
        {
            if (stepsBudget <= 0 || _driver is not { IsReady: true }) return;

            var stepped = 0;
            while (stepped < stepsBudget && _driver.TryTick())
            {
                stepped++;
            }
        }

        internal void Stop()
        {
            SessionAsyncOperation.RequireCompleted(
                StopAsync(),
                "Spectator session stop");
        }

        internal Task StopAsync()
        {
            lock (_operationGate)
            {
                if (!_pendingStopTask.IsCompleted)
                {
                    return _pendingStopTask;
                }

                _pendingStopTask = StopAfterPendingStartAsync();
                return _pendingStopTask;
            }
        }

        private async Task StopAfterPendingStartAsync()
        {
            await _startGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCurrentOperationAsync().ConfigureAwait(false);
            }
            finally
            {
                _startGate.Release();
            }
        }

        private Task StopCurrentOperationAsync()
        {
            var operation = _operation;
            if (operation == null) return Task.CompletedTask;

            _generation++;
            var cancellationFailure = CancelOperation(operation);
            return StopCoreAsync(operation, cancellationFailure);
        }

        private async Task StopCoreAsync(
            StartOperation operation,
            Exception cancellationFailure)
        {
            var failures = new List<Exception>();
            if (cancellationFailure != null) failures.Add(cancellationFailure);

            try
            {
                await (operation.StartTask ?? Task.CompletedTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            var cleanupFailure = CleanupOperation(operation);
            if (cleanupFailure != null) failures.Add(cleanupFailure);
            if (failures.Count == 0 && ReferenceEquals(_operation, operation))
            {
                _operation = null;
            }

            if (failures.Count == 1) throw failures[0];
            if (failures.Count > 1)
            {
                throw new AggregateException(
                    "Failed to cancel and stop spectator session resources.",
                    failures);
            }
        }

        public void Dispose()
        {
            SessionAsyncOperation.RequireCompleted(
                StopAsync(),
                "Spectator session dispose");
        }

        private async Task StartCoreAsync(
            StartOperation operation,
            ulong roomId,
            Func<IWorld> worldFactory)
        {
            Exception failure = null;
            try
            {
                var subscribePayload = BitConverter.GetBytes(roomId);
                var response = await AwaitWithCancellationAsync(
                        operation.Client.SendRequestAsync(
                            OpCodes.SpectatorSubscribe,
                            subscribePayload,
                            operation.CancellationToken),
                        operation.CancellationToken)
                    .ConfigureAwait(false);
                ThrowIfStale(operation);

                if (response == null || response.Length == 0)
                {
                    throw new InvalidOperationException("SpectatorSubscribe returned an empty response.");
                }

                var subscription = MobaProtocolDecoderModule.DecodeSpectatorSubscribeResponse(response);
                var driver = _driverFactory();
                if (driver == null)
                {
                    throw new InvalidOperationException("The spectator driver factory returned null.");
                }

                operation.Driver = driver;
                driver.Initialize(subscription.WorldId, subscription.TickRate, worldFactory);
                ThrowIfStale(operation);

                if (subscription.CurrentFrame > 0)
                {
                    var request = new WireCatchUpRequest(roomId, subscription.WorldId, -1, subscription.CurrentFrame);
                    var payload = WireCustomBinary.Serialize(request);
                    await AwaitWithCancellationAsync(
                            operation.Client.SendRequestAsync(
                                OpCodes.CatchUpRequest,
                                payload.Array ?? Array.Empty<byte>(),
                                operation.CancellationToken),
                            operation.CancellationToken)
                        .ConfigureAwait(false);
                    ThrowIfStale(operation);
                }

                _driver = driver;
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                if (failure != null)
                {
                    var cleanupFailure = CleanupOperation(operation);
                    if (cleanupFailure == null && ReferenceEquals(_operation, operation))
                    {
                        _operation = null;
                    }

                    if (cleanupFailure != null)
                    {
                        throw new AggregateException(
                            "Failed to start and clean up the spectator session.",
                            failure,
                            cleanupFailure);
                    }
                }
            }
        }

        private void HandlePush(StartOperation operation, uint opCode, byte[] payload)
        {
            if (!IsCurrent(operation)) return;
            var driver = operation.Driver;
            if (driver is not { IsReady: true }) return;

            switch (opCode)
            {
                case OpCodes.FramePushed:
                    FeedFrame(driver, payload);
                    break;
                case OpCodes.CatchUpPayloadPush:
                    FeedCatchUp(driver, payload);
                    break;
            }
        }

        private static void FeedFrame(SpectatorWorldDriver driver, byte[] payload)
        {
            var pushed = WireCustomBinary.DeserializeFramePushedPush(new ArraySegment<byte>(payload));
            var inputs = pushed.Inputs;
            var commands = new PlayerInputCommand[inputs?.Length ?? 0];
            for (var i = 0; i < commands.Length; i++)
            {
                var input = inputs[i];
                commands[i] = new PlayerInputCommand(
                    new FrameIndex(pushed.Frame),
                    new PlayerId(input.PlayerId.ToString()),
                    input.OpCode,
                    input.Payload ?? Array.Empty<byte>());
            }

            driver.FeedFrameInputs(pushed.Frame, commands);
        }

        private static void FeedCatchUp(SpectatorWorldDriver driver, byte[] payload)
        {
            var pushed = WireCustomBinary.DeserializeCatchUpPayloadPush(new ArraySegment<byte>(payload));
            var frames = pushed.Frames;
            var allInputs = new PlayerInputCommand[frames?.Length ?? 0][];
            for (var i = 0; i < allInputs.Length; i++)
            {
                var frame = frames[i];
                var inputs = frame.Inputs;
                var commands = new PlayerInputCommand[inputs?.Length ?? 0];
                for (var j = 0; j < commands.Length; j++)
                {
                    var input = inputs[j];
                    commands[j] = new PlayerInputCommand(
                        new FrameIndex(frame.Frame),
                        new PlayerId(input.PlayerId.ToString()),
                        input.OpCode,
                        input.Payload ?? Array.Empty<byte>());
                }

                allInputs[i] = commands;
            }

            driver.FeedCatchUpPayload(new FrameSyncCatchUpPayload(
                new WorldId(pushed.WorldId.ToString()),
                new FrameIndex(pushed.StartFrame),
                allInputs));
        }

        private void ThrowIfStale(StartOperation operation)
        {
            if (!IsCurrent(operation)) throw new OperationCanceledException(operation.CancellationToken);
        }

        private bool IsCurrent(StartOperation operation)
        {
            return ReferenceEquals(_operation, operation) &&
                   operation.Generation == _generation &&
                   !operation.CancellationToken.IsCancellationRequested;
        }

        private static Exception CancelOperation(StartOperation operation)
        {
            if (operation.IsCancellationRequested || operation.IsCancellationDisposed) return null;

            try
            {
                operation.Cancellation.Cancel();
                operation.IsCancellationRequested = true;
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static async Task<T> AwaitWithCancellationAsync<T>(
            Task<T> operationTask,
            CancellationToken cancellationToken)
        {
            if (operationTask == null)
            {
                throw new InvalidOperationException("The spectator network request returned no task.");
            }

            if (operationTask.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return await operationTask.ConfigureAwait(false);
            }

            var cancellationTask = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                       state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
                       cancellationTask))
            {
                var completed = await Task.WhenAny(operationTask, cancellationTask.Task)
                    .ConfigureAwait(false);
                if (!ReferenceEquals(completed, operationTask))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await operationTask.ConfigureAwait(false);
        }

        private Exception CleanupOperation(StartOperation operation)
        {
            List<Exception> failures = null;

            if (operation.IsSubscribed)
            {
                try
                {
                    operation.Client.OnServerPush -= operation.PushHandler;
                    operation.IsSubscribed = false;
                }
                catch (Exception ex)
                {
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            var driver = operation.Driver;
            if (driver != null)
            {
                try
                {
                    driver.Dispose();
                    if (ReferenceEquals(operation.Driver, driver)) operation.Driver = null;
                    if (ReferenceEquals(_driver, driver)) _driver = null;
                }
                catch (Exception ex)
                {
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            if (!operation.IsCancellationDisposed)
            {
                try
                {
                    operation.Cancellation.Dispose();
                    operation.IsCancellationDisposed = true;
                }
                catch (Exception ex)
                {
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            if (failures == null) return null;
            return failures.Count == 1
                ? failures[0]
                : new AggregateException("Failed to stop spectator session resources.", failures);
        }
    }
}
