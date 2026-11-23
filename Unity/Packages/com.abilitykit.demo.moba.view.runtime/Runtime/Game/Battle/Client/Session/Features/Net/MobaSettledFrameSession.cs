using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Network.Battle;
using AbilityKit.Protocol.Moba.SettledFrames;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 为单个正式战斗世界维护结算流订阅和补帧，不持有模拟快照。
    /// </summary>
    internal sealed class MobaSettledFrameSession : IDisposable
    {
        private const int CatchUpPageFrames = 128;
        private const int MaxQueuedPushes = 4096;
        private readonly object _gate = new object();
        private readonly Queue<WireSettledFrame> _pushes = new Queue<WireSettledFrame>();
        private readonly WireSettledSubscribeRequest _request;
        private readonly int _tickRate;
        private readonly int _inputDelayFrames;
        private NetworkTransport _transport;
        private INetworkClient _spectatorClient;
        private CancellationToken _spectatorCancellation;
        private Task<byte[]> _fetch;
        private int _from;
        private int _to;
        private bool _disposed;
        private WireSettledIdentity _identity;

        internal MobaSettledFrameSession(BattleStartPlan plan, bool spectator = false)
        {
            _request = new WireSettledSubscribeRequest
            {
                Version = SettledFrameProtocol.Version,
                RoomId = plan.Gateway.NumericRoomId,
                WorldId = ulong.Parse(plan.World.WorldId, CultureInfo.InvariantCulture),
                BattleId = plan.Gateway.BattleId,
                Spectator = spectator
            };
            _tickRate = plan.World.TickRate;
            _inputDelayFrames = SessionSimRuntimeTuning.NormalizeInputDelayFrames(plan.World.InputDelayFrames);
        }

        internal MobaSettledFrameReceiver Receiver { get; private set; }
        internal ulong WorldId => _request.WorldId;
        internal int RecoveryTarget { get; private set; }
        internal int CatchUpPayloadCount { get; private set; }
        internal int CatchUpFrameCount { get; private set; }
        internal bool CatchUpStarted { get; private set; }
        internal string Fault { get; private set; }
        private bool IsConnected => _request.Spectator
            ? _spectatorClient?.IsConnected == true && !_spectatorCancellation.IsCancellationRequested
            : _transport?.IsAuthenticated == true;
        internal bool CanAdvance => !_disposed && Fault == null && IsConnected &&
            Receiver?.HasCheckpoint == true;
        internal bool CanSubmit => !_request.Spectator && CanAdvance && Receiver.LastConsumedFrame >= RecoveryTarget;

        internal async Task BindSpectatorAsync(INetworkClient client, CancellationToken cancellationToken)
        {
            if (!_request.Spectator || _spectatorClient != null)
                throw new InvalidOperationException("A settled spectator session can only bind once.");
            _spectatorClient = client;
            _spectatorCancellation = cancellationToken;
            client.OnServerPush += OnSpectatorPush;
            client.OnDisconnected += OnSpectatorDisconnected;
            var response = await client.SendRequestAsync(SettledFrameProtocol.Subscribe,
                SettledFrameProtocol.Encode(_request), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSubscription(new ArraySegment<byte>(response));
        }

        private void OnSpectatorPush(uint opCode, byte[] payload) => OnPush(opCode, new ArraySegment<byte>(payload));

        private void OnSpectatorDisconnected(string reason)
        {
            lock (_gate)
                if (!_disposed) SetFault(new InvalidOperationException($"Settled spectator connection closed: {reason}"));
        }

        internal void Configure(NetworkTransportOptions options)
        {
            options.OpPostAuthentication = SettledFrameProtocol.Subscribe;
            options.SerializePostAuthentication = () => new ArraySegment<byte>(SettledFrameProtocol.Encode(_request));
            options.SerializePostAuthenticationWithReliableEventCursor = null;
            options.ValidatePostAuthenticationResponse = ValidateSubscription;
            options.OpFramePushed = 0;
            options.DeserializeFramePushed = null;
        }

        internal void Attach(NetworkTransport transport)
        {
            _transport = transport;
            transport.RawServerPushReceived += OnPush;
            transport.ConnectionClosed += OnDisconnected;
            transport.AuthenticationFailed += OnAuthenticationFailed;
        }

        private void ValidateSubscription(ArraySegment<byte> payload)
        {
            var subscription = SettledFrameProtocol.Decode<WireSettledSubscribeResponse>(payload);
            var identity = subscription.Identity;
            lock (_gate)
            {
                if (_disposed || Fault != null || identity.Version != _request.Version ||
                    identity.RoomId != _request.RoomId || identity.WorldId != _request.WorldId ||
                    identity.BattleId != _request.BattleId || subscription.TickRate != _tickRate ||
                    (Receiver != null && (identity.RuntimeGeneration != _identity.RuntimeGeneration ||
                        subscription.LastSettledFrame < Receiver.LastConsumedFrame)))
                    throw new InvalidOperationException("Invalid settled subscription for the current battle world.");

                Receiver ??= new MobaSettledFrameReceiver(subscription);
                _identity = identity;
                RecoveryTarget = subscription.LastSettledFrame;
                CatchUpStarted = false;
                CatchUpPayloadCount = 0;
                CatchUpFrameCount = 0;
            }
        }

        private void OnPush(uint opCode, ArraySegment<byte> payload)
        {
            if (opCode != SettledFrameProtocol.FramePushed) return;
            lock (_gate)
            {
                if (_disposed || Fault != null) return;
                try
                {
                    if (_pushes.Count >= MaxQueuedPushes)
                        throw new InvalidOperationException("Settled push queue capacity exceeded.");
                    _pushes.Enqueue(SettledFrameProtocol.Decode<WireSettledFrame>(payload));
                }
                catch (Exception exception) { SetFault(exception); }
            }
        }

        private void OnDisconnected()
        {
            lock (_gate)
            {
                _pushes.Clear();
                AbandonFetch();
            }
        }

        private void OnAuthenticationFailed(Exception exception) => ReportFault(exception);

        internal void ReportFault(Exception exception)
        {
            lock (_gate)
                if (!_disposed) SetFault(exception);
        }

        internal void Tick()
        {
            lock (_gate)
            {
                if (_disposed || Fault != null || !IsConnected || Receiver == null) return;
                try
                {
                    Receiver.DelayFrames = CanSubmit ? _inputDelayFrames : 0;
                    if (_fetch?.IsCompleted == true)
                    {
                        var response = SettledFrameProtocol.Decode<WireSettledCatchUpResponse>(
                            new ArraySegment<byte>(_fetch.GetAwaiter().GetResult()));
                        _fetch = null;
                        Receiver.ReceiveCatchUp(response, _from, _to);
                        CatchUpPayloadCount++;
                        CatchUpFrameCount += response.Frames.Length;
                    }

                    while (_pushes.Count != 0) Receiver.Receive(_pushes.Dequeue());
                    if (_fetch != null) return;

                    var consumed = Receiver.LastConsumedFrame;
                    var target = Math.Max(RecoveryTarget, Receiver.TargetFrame);
                    if (Receiver.HasCheckpoint && (consumed >= target || Receiver.TryGet(consumed + 1, out _))) return;

                    _from = Receiver.HasCheckpoint ? consumed : -1;
                    _to = Math.Min(target, _from + CatchUpPageFrames);
                    var request = new WireSettledCatchUpRequest
                    {
                        Identity = _identity,
                        FromFrameExclusive = _from,
                        ToFrameInclusive = _to
                    };
                    CatchUpStarted = true;
                    var payload = SettledFrameProtocol.Encode(request);
                    _fetch = _request.Spectator
                        ? FetchSpectatorPageAsync(payload)
                        : _transport.SendBattleRecoveryRequestAsync(SettledFrameProtocol.CatchUp, payload, TimeSpan.FromSeconds(10));
                }
                catch (Exception exception) { SetFault(exception); }
            }
        }

        private async Task<byte[]> FetchSpectatorPageAsync(byte[] payload)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_spectatorCancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await _spectatorClient.SendRequestAsync(SettledFrameProtocol.CatchUp, payload, timeout.Token)
                .ConfigureAwait(false);
        }

        private void AbandonFetch()
        {
            if (_fetch != null)
                _ = _fetch.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _fetch = null;
        }

        private void SetFault(Exception exception)
        {
            Fault ??= exception.Message;
            AbandonFetch();
            _pushes.Clear();
            UnityEngine.Debug.LogError($"[MobaSettledFrameSession] {Fault}");
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                if (_spectatorClient != null)
                {
                    _spectatorClient.OnServerPush -= OnSpectatorPush;
                    _spectatorClient.OnDisconnected -= OnSpectatorDisconnected;
                    _spectatorClient = null;
                }
                if (_transport != null)
                {
                    _transport.RawServerPushReceived -= OnPush;
                    _transport.ConnectionClosed -= OnDisconnected;
                    _transport.AuthenticationFailed -= OnAuthenticationFailed;
                    _transport = null;
                }
                AbandonFetch();
                _pushes.Clear();
                Receiver?.Dispose();
            }
        }
    }
}
