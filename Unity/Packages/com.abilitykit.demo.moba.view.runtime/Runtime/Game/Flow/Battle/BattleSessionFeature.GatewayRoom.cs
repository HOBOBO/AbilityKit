using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private bool ShouldPrepareGatewayRoom()
        {
            if (_plan.HostMode != BattleStartConfig.BattleHostMode.GatewayRemote) return false;
            if (!_plan.UseGatewayTransport) return false;
            if (!_plan.GatewayAutoCreateRoom && !_plan.GatewayAutoJoinRoom) return false;
            return true;
        }

        private void StartGatewayRoomPreparation()
        {
            StopGatewayRoomPreparation();

            var connOptions = new ConnectionOptions
            {
                FrameCodec = LengthPrefixedFrameCodec.Instance,
                KickPushOpCode = 9000
            };

            _gatewayRoomConn = new ConnectionManager(() => new TcpTransport(), connOptions, _unityDispatcher, _networkIoDispatcher);
            _gatewayRoomConn.Open(_plan.GatewayHost, _plan.GatewayPort);

            var opCodes = new GatewayRoomOpCodes(_plan.GatewayCreateRoomOpCode, _plan.GatewayJoinRoomOpCode);
            _gatewayRoomClient = new GatewayRoomClient(_gatewayRoomConn, opCodes);

            _gatewayRoomTask = PrepareRoomAsync();
        }

        private void StopTimeSyncLoop()
        {
            if (_timeSyncCts != null)
            {
                if (!_timeSyncCts.IsCancellationRequested)
                {
                    _timeSyncCts.Cancel();
                }

                _timeSyncCts.Dispose();
                _timeSyncCts = null;
            }

            _timeSyncTask = null;
            _hasClockSync = false;
            _clockOffsetSecondsEwma = 0;
            _rttSecondsEwma = 0;
            _timeSyncSamples = 0;

            BattleFlowDebugProvider.TimeSyncStats = null;
            BattleFlowDebugProvider.TimeSyncStatsByWorld = null;
        }

        private async Task PrepareRoomAsync()
        {
            // Wait until connected.
            while (_gatewayRoomConn != null && _gatewayRoomConn.State == ConnectionState.Connecting)
            {
                await Task.Yield();
            }

            if (_gatewayRoomConn == null || _gatewayRoomConn.State != ConnectionState.Connected)
            {
                throw new InvalidOperationException($"Gateway room connection not connected. state={_gatewayRoomConn?.State}");
            }

            Log.Info($"[BattleSessionFeature] GatewayRoom connected: {_plan.GatewayHost}:{_plan.GatewayPort}");

            const uint GuestLoginOpCode = 100;
            var sessionToken = _plan.GatewaySessionToken;
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                Log.Info("[BattleSessionFeature] GatewayRoom GuestLogin...");
                sessionToken = await _gatewayRoomClient.GuestLoginAsync(GuestLoginOpCode);
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    throw new InvalidOperationException("Gateway guest login failed: sessionToken is empty.");
                }

                Log.Info("[BattleSessionFeature] GatewayRoom GuestLogin ok.");

                _plan = new BattleStartPlan(
                    worldId: _plan.WorldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: _plan.NumericRoomId,
                    gatewaySessionToken: sessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableClientPrediction: _plan.EnableClientPrediction,
                    enableConfirmedAuthorityWorld: _plan.EnableConfirmedAuthorityWorld,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);
            }

            if (_plan.GatewayAutoCreateRoom)
            {
                Log.Info("[BattleSessionFeature] GatewayRoom CreateRoom...");
                var result = await _gatewayRoomClient.CreateRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomType: string.IsNullOrEmpty(_plan.WorldType) ? "battle" : _plan.WorldType,
                    title: string.Empty,
                    isPublic: true,
                    maxPlayers: 10,
                    tags: null);

                Log.Info($"[BattleSessionFeature] GatewayRoom CreateRoom ok. roomId='{result.RoomId}' numericRoomId={result.NumericRoomId}");

                if (result.NumericRoomId == 0)
                {
                    throw new InvalidOperationException($"Gateway CreateRoom returned invalid NumericRoomId. roomId='{result.RoomId}'");
                }

                var worldId = result.NumericRoomId.ToString();

                _plan = new BattleStartPlan(
                    worldId: worldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: result.NumericRoomId,
                    gatewaySessionToken: _plan.GatewaySessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableClientPrediction: _plan.EnableClientPrediction,
                    enableConfirmedAuthorityWorld: _plan.EnableConfirmedAuthorityWorld,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);

                var joinResult = await _gatewayRoomClient.JoinRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomId: string.IsNullOrWhiteSpace(result.RoomId) ? _plan.NumericRoomId.ToString() : result.RoomId);

                var wid = new WorldId(_plan.WorldId);
                if (joinResult.WorldStartAnchor.ServerTickFrequency != 0)
                {
                    _gatewayWorldStartAnchors[wid] = joinResult.WorldStartAnchor;
                }
                StartTimeSyncLoop();

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={_plan.NumericRoomId}");
                return;
            }

            if (_plan.GatewayAutoJoinRoom)
            {
                var joinRoomId = _plan.GatewayJoinRoomId;
                if (string.IsNullOrWhiteSpace(joinRoomId))
                {
                    joinRoomId = _plan.NumericRoomId != 0 ? _plan.NumericRoomId.ToString() : _plan.WorldId;
                }
                if (string.IsNullOrWhiteSpace(joinRoomId))
                {
                    throw new InvalidOperationException("GatewayAutoJoinRoom requires JoinRoomId or WorldId.");
                }

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom... roomId='{joinRoomId}'");
                var result = await _gatewayRoomClient.JoinRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomId: joinRoomId);

                var tmpWid = new WorldId(_plan.WorldId);
                if (result.WorldStartAnchor.ServerTickFrequency != 0)
                {
                    _gatewayWorldStartAnchors[tmpWid] = result.WorldStartAnchor;
                }
                StartTimeSyncLoop();

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={result.NumericRoomId}");

                if (result.NumericRoomId == 0)
                {
                    throw new InvalidOperationException($"Gateway JoinRoom returned invalid NumericRoomId. roomId='{joinRoomId}'");
                }

                var worldId = result.NumericRoomId.ToString();

                _plan = new BattleStartPlan(
                    worldId: worldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: result.NumericRoomId,
                    gatewaySessionToken: _plan.GatewaySessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableClientPrediction: _plan.EnableClientPrediction,
                    enableConfirmedAuthorityWorld: _plan.EnableConfirmedAuthorityWorld,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);
                return;
            }
        }

        private void StopGatewayRoomPreparation()
        {
            _gatewayRoomTask = null;
            _gatewayRoomClient = null;

            StopTimeSyncLoop();
            _gatewayWorldStartAnchors.Clear();

            if (_gatewayRoomConn != null)
            {
                _gatewayRoomConn.Dispose();
                _gatewayRoomConn = null;
            }
        }

        private void StartTimeSyncLoop()
        {
            if (_gatewayRoomClient == null) return;
            if (_timeSyncTask != null && !_timeSyncTask.IsCompleted) return;

            _timeSyncCts = new CancellationTokenSource();
            var token = _timeSyncCts.Token;

            _timeSyncTask = Task.Run(async () =>
            {
                var alpha = _plan.TimeSyncAlpha;
                if (alpha < 0) alpha = 0;
                if (alpha > 1) alpha = 1;

                var intervalMs = _plan.TimeSyncIntervalMs;
                if (intervalMs <= 0) intervalMs = 1000;

                var opCode = _plan.TimeSyncOpCode;
                var timeoutMs = _plan.TimeSyncTimeoutMs;
                if (timeoutMs <= 0) timeoutMs = 2000;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var t0 = Stopwatch.GetTimestamp();
                        var res = await _gatewayRoomClient.TimeSyncAsync(timeSyncOpCode: opCode, clientSendTicks: t0, timeout: TimeSpan.FromMilliseconds(timeoutMs), cancellationToken: token);
                        var t2 = Stopwatch.GetTimestamp();

                        var localFreq = (double)Stopwatch.Frequency;
                        var rtt = (t2 - t0) / localFreq;
                        if (rtt < 0) rtt = 0;

                        var serverNowSeconds = res.ServerNowTicks / (double)res.ServerTickFrequency;
                        var localNowSeconds = t2 / localFreq;
                        var serverNowEstimatedAtReceive = serverNowSeconds + (rtt * 0.5);
                        var offsetSeconds = localNowSeconds - serverNowEstimatedAtReceive;

                        if (!_hasClockSync)
                        {
                            _hasClockSync = true;
                            _clockOffsetSecondsEwma = offsetSeconds;
                            _rttSecondsEwma = rtt;
                            _timeSyncSamples = 1;
                        }
                        else
                        {
                            _clockOffsetSecondsEwma = (alpha * offsetSeconds) + ((1.0 - alpha) * _clockOffsetSecondsEwma);
                            _rttSecondsEwma = (alpha * rtt) + ((1.0 - alpha) * _rttSecondsEwma);
                            _timeSyncSamples++;
                        }

                        BattleFlowDebugProvider.TimeSyncStats = new TimeSyncStatsSnapshot
                        {
                            OpCode = opCode,
                            IntervalMs = intervalMs,
                            Alpha = alpha,
                            TimeoutMs = timeoutMs,

                            HasAnchor = TryGetWorldStartAnchor(_plan.WorldId != null ? new WorldId(_plan.WorldId) : default, out var anchor),
                            AnchorStartServerTicks = anchor.StartServerTicks,
                            AnchorServerTickFrequency = anchor.ServerTickFrequency,
                            AnchorStartFrame = anchor.StartFrame,
                            AnchorFixedDeltaSeconds = anchor.FixedDeltaSeconds,

                            HasClockSync = _hasClockSync,
                            OffsetSecondsEwma = _clockOffsetSecondsEwma,
                            RttSecondsEwma = _rttSecondsEwma,
                            Samples = _timeSyncSamples,

                            IdealFrameRaw = ResolveIdealFrameRaw(_plan.WorldId != null ? new WorldId(_plan.WorldId) : default),
                            IdealFrameSafetyMarginFrames = ResolveIdealFrameSafetyMarginFrames(_plan.WorldId != null ? new WorldId(_plan.WorldId) : default),
                            IdealFrameLimit = ResolveIdealFrameLimit(_plan.WorldId != null ? new WorldId(_plan.WorldId) : default)
                        };

                        if (BattleFlowDebugProvider.TimeSyncStatsByWorld == null)
                        {
                            BattleFlowDebugProvider.TimeSyncStatsByWorld = new Dictionary<string, TimeSyncStatsSnapshot>();
                        }

                        // Update per-world snapshots for all known anchors (multi-world).
                        foreach (var kv in _gatewayWorldStartAnchors)
                        {
                            var wid = kv.Key;
                            var snap = new TimeSyncStatsSnapshot
                            {
                                OpCode = opCode,
                                IntervalMs = intervalMs,
                                Alpha = alpha,
                                TimeoutMs = timeoutMs,

                                HasAnchor = kv.Value.ServerTickFrequency != 0,
                                AnchorStartServerTicks = kv.Value.StartServerTicks,
                                AnchorServerTickFrequency = kv.Value.ServerTickFrequency,
                                AnchorStartFrame = kv.Value.StartFrame,
                                AnchorFixedDeltaSeconds = kv.Value.FixedDeltaSeconds,

                                HasClockSync = _hasClockSync,
                                OffsetSecondsEwma = _clockOffsetSecondsEwma,
                                RttSecondsEwma = _rttSecondsEwma,
                                Samples = _timeSyncSamples,

                                IdealFrameRaw = ResolveIdealFrameRaw(wid),
                                IdealFrameSafetyMarginFrames = ResolveIdealFrameSafetyMarginFrames(wid),
                                IdealFrameLimit = ResolveIdealFrameLimit(wid)
                            };

                            BattleFlowDebugProvider.TimeSyncStatsByWorld[wid.Value] = snap;
                        }

                        // Backward compatible: also keep the current plan world as the default entry.
                        if (_plan.WorldId != null)
                        {
                            BattleFlowDebugProvider.TimeSyncStatsByWorld[_plan.WorldId] = BattleFlowDebugProvider.TimeSyncStats;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[BattleSessionFeature] TimeSync loop error");
                    }

                    try
                    {
                        await Task.Delay(intervalMs, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        private bool TryGetWorldStartAnchor(WorldId worldId, out GatewayWorldStartAnchor anchor)
        {
            anchor = default;
            if (string.IsNullOrEmpty(worldId.Value)) return false;
            return _gatewayWorldStartAnchors.TryGetValue(worldId, out anchor) && anchor.ServerTickFrequency != 0;
        }

        private int ResolveIdealFrameRaw(WorldId worldId)
        {
            if (!TryGetWorldStartAnchor(worldId, out var anchor)) return 0;
            if (!_hasClockSync) return 0;

            var localNowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

            var startServerSeconds = anchor.StartServerTicks / (double)anchor.ServerTickFrequency;
            var localStartSeconds = startServerSeconds + _clockOffsetSecondsEwma;

            var elapsed = localNowSeconds - localStartSeconds;
            if (elapsed < 0) elapsed = 0;

            var dt = anchor.FixedDeltaSeconds;
            if (dt <= 0) return 0;

            var frames = (int)Math.Floor(elapsed / dt);
            return anchor.StartFrame + frames;
        }

        private int ResolveIdealFrameSafetyMarginFrames(WorldId worldId)
        {
            if (!TryGetWorldStartAnchor(worldId, out var anchor)) return 0;
            if (!_hasClockSync) return 0;

            var dt = anchor.FixedDeltaSeconds;
            if (dt <= 0) return 0;

            var constMargin = _plan.IdealFrameSafetyConstMarginFrames;
            if (constMargin < 0) constMargin = 0;

            var rttFactor = _plan.IdealFrameSafetyRttFactor;
            if (rttFactor < 0) rttFactor = 0;

            var rttFrames = (int)Math.Ceiling((_rttSecondsEwma / dt) * rttFactor);
            if (rttFrames < 0) rttFrames = 0;

            var margin = constMargin;
            if (rttFrames > margin) margin = rttFrames;

            var minMargin = _plan.IdealFrameSafetyMinMarginFrames;
            var maxMargin = _plan.IdealFrameSafetyMaxMarginFrames;
            if (minMargin < 0) minMargin = 0;
            if (maxMargin < minMargin) maxMargin = minMargin;

            if (margin < minMargin) margin = minMargin;
            if (margin > maxMargin) margin = maxMargin;

            return margin;
        }

        private int ResolveIdealFrameLimit(WorldId worldId)
        {
            if (!TryGetWorldStartAnchor(worldId, out var anchor)) return 0;
            if (!_hasClockSync) return 0;

            var localNowSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

            var startServerSeconds = anchor.StartServerTicks / (double)anchor.ServerTickFrequency;
            var localStartSeconds = startServerSeconds + _clockOffsetSecondsEwma;

            var elapsed = localNowSeconds - localStartSeconds;
            if (elapsed < 0) elapsed = 0;

            var dt = anchor.FixedDeltaSeconds;
            if (dt <= 0) return 0;

            var frames = (int)Math.Floor(elapsed / dt);
            var idealRaw = anchor.StartFrame + frames;

            var margin = ResolveIdealFrameSafetyMarginFrames(worldId);

            var limit = idealRaw - margin;
            if (limit < anchor.StartFrame) limit = anchor.StartFrame;
            return limit;
        }
    }
}
