using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Battle;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using StateSyncOpCodes = AbilityKit.Protocol.Moba.StateSync.OpCodes;

namespace AbilityKit.Game.Battle.Transport.Moba.Client
{
    /// <summary>
    /// 服务器推送处理器：快照
    /// </summary>
    public sealed class SnapshotPushHandler : IServerPushHandler
    {
        public uint OpCode => StateSyncOpCodes.SnapshotPushed;

        private readonly Action<IWorldSnapshot> _onSnapshot;

        public SnapshotPushHandler(Action<IWorldSnapshot> onSnapshot)
        {
            _onSnapshot = onSnapshot;
        }

        public void Handle(byte[] payload)
        {
            var snapshot = StateSyncCodec.DecodeWorldSnapshot(payload);
            _onSnapshot?.Invoke(snapshot);
        }
    }

    /// <summary>
    /// 服务器推送处理器：帧输入
    /// </summary>
    public sealed class FrameInputPushHandler : IServerPushHandler
    {
        public uint OpCode => OpCodes.FramePushed;

        private readonly Action<IFrameData> _onFrameInput;

        public FrameInputPushHandler(Action<IFrameData> onFrameInput)
        {
            _onFrameInput = onFrameInput;
        }

        public void Handle(byte[] payload)
        {
            var frameData = StateSyncCodec.DecodeFrameData(payload);
            _onFrameInput?.Invoke(frameData);
        }
    }

    /// <summary>
    /// 状态同步适配器实现
    /// 连接 Orleans Gateway，接收服务器推送的状态快照
    /// </summary>
    public sealed class StateSyncAdapter : IStateSyncAdapter
    {
        private NetworkSession _session;
        private readonly SnapshotPushHandler _snapshotHandler;
        private readonly FrameInputPushHandler _frameInputHandler;

        private IWorldSnapshot _latestSnapshot;
        private SyncMode _mode = SyncMode.StateSync;
        private bool _isConnected;
        private int _currentFrame;
        private int _localActorId;
        private string _roomId = string.Empty;
        private ulong _numericRoomId;
        private string _playerId = string.Empty;
        private CancellationTokenSource _syncCts;

        public SyncMode Mode => _mode;
        public bool IsConnected => _isConnected;
        public int CurrentFrame => _currentFrame;
        public int LocalActorId => _localActorId;

        private INetworkClient _gatewayClient;
        public INetworkClient GatewayClient
        {
            get => _gatewayClient;
            set
            {
                _gatewayClient = value;
                if (_gatewayClient != null && _session == null)
                {
                    CreateSession();
                }
            }
        }

        public event Action<bool> OnConnectionChanged;
        public event Action<int> OnFrameAdvanced;
        public event Action<IWorldSnapshot> OnSnapshotReceived;

        public StateSyncAdapter()
        {
            _snapshotHandler = new SnapshotPushHandler(HandleSnapshot);
            _frameInputHandler = new FrameInputPushHandler(HandleFrameInput);
        }

        private void CreateSession()
        {
            if (_gatewayClient == null) return;

            _session = new NetworkSession(_gatewayClient);
            _session.OnStateChanged += OnSessionStateChanged;
            _session.Subscribe(_snapshotHandler);
            _session.Subscribe(_frameInputHandler);
        }

        public void Initialize(object context, object config)
        {
            if (!(config is IBattleStartConfig battleConfig)) return;

            _localActorId = battleConfig.LocalPlayerId;
        }

        public void Connect(string host, int port, string roomId, string playerId)
        {
            _roomId = roomId;
            _playerId = playerId;

            ConnectAsync(host, port);
        }

        private async void ConnectAsync(string host, int port)
        {
            try
            {
                await _session.ConnectAsync(host, port);

                var loginResult = await _session.LoginAsGuestAsync(_playerId);
                if (!loginResult.Success)
                {
                    OnConnectionChanged?.Invoke(false);
                    return;
                }

                bool roomJoined = false;
                if (!string.IsNullOrEmpty(_roomId))
                {
                    var joinResult = await _session.JoinRoomAsync(_roomId);
                    roomJoined = joinResult.Success;
                    if (joinResult.Success)
                    {
                        _numericRoomId = joinResult.NumericRoomId;
                    }
                }

                if (!roomJoined)
                {
                    var createResult = await _session.CreateRoomAsync(_roomId);
                    roomJoined = createResult.Success;
                    if (createResult.Success)
                    {
                        var joinResult = await _session.JoinRoomAsync(createResult.RoomId);
                        if (joinResult.Success)
                        {
                            _numericRoomId = joinResult.NumericRoomId;
                        }
                    }
                }

                if (roomJoined)
                {
                    _isConnected = true;
                    _syncCts = new CancellationTokenSource();
                    OnConnectionChanged?.Invoke(true);
                }
            }
            catch (Exception)
            {
                OnConnectionChanged?.Invoke(false);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_syncCts != null)
            {
                _syncCts.Cancel();
                _syncCts.Dispose();
                _syncCts = null;
            }

            await _session.DisconnectAsync();
            _isConnected = false;
            OnConnectionChanged?.Invoke(false);
        }

        public void Disconnect() => DisconnectAsync().Wait();

        public async void SubmitInput(uint playerId, uint opCode, byte[] payload = null)
        {
            if (!_isConnected) return;

            await _session.SubmitFrameInputAsync(_numericRoomId, 0, _currentFrame, playerId, (int)opCode, payload);
        }

        public void Tick(float deltaTime)
        {
        }

        public IWorldSnapshot GetLatestSnapshot() => _latestSnapshot;

        private void HandleSnapshot(IWorldSnapshot snapshot)
        {
            _latestSnapshot = snapshot;
            _currentFrame = snapshot.Frame;
            OnSnapshotReceived?.Invoke(snapshot);
            OnFrameAdvanced?.Invoke(_currentFrame);
        }

        private void HandleFrameInput(IFrameData frameData)
        {
            _currentFrame = frameData.Frame;
            OnFrameAdvanced?.Invoke(_currentFrame);
        }

        private void OnSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Disconnected || state == SessionState.Disposing)
            {
                _isConnected = false;
                OnConnectionChanged?.Invoke(false);
            }
        }

        public void Dispose()
        {
            if (_syncCts != null)
            {
                _syncCts.Cancel();
                _syncCts.Dispose();
            }
            _session?.Dispose();
            _session = null;
        }
    }
}
