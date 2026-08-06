using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using AbilityKit.Network.Battle;
namespace AbilityKit.Game.Battle.Transport.Moba
{
    /// <summary>
    /// 会话连接状态
    /// </summary>
    public enum SessionState
    {
        Disconnected,
        Connecting,
        Authenticating,
        InRoom,
        Connected,
        Disposing
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    public sealed class LoginResult
    {
        public bool Success { get; init; }
        public string SessionToken { get; init; } = string.Empty;
        public string AccountId { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 房间加入结果
    /// </summary>
    public sealed class RoomJoinResult
    {
        public bool Success { get; init; }
        public string RoomId { get; init; } = string.Empty;
        public ulong NumericRoomId { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 房间创建结果
    /// </summary>
    public sealed class RoomCreateResult
    {
        public bool Success { get; init; }
        public string RoomId { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 推送消息处理器
    /// </summary>
    public interface IServerPushHandler
    {
        uint OpCode { get; }
        void Handle(byte[] payload);
    }

    /// <summary>
    /// 服务器推送接口
    /// </summary>
    public interface IServerPushReceiver
    {
        void Subscribe(IServerPushHandler handler);
        void Unsubscribe(uint opCode);
    }

    /// <summary>
    /// 网络会话接口
    /// 抽象与服务器的连接和会话管理
    /// </summary>
    public interface INetworkSession : IDisposable
    {
        SessionState State { get; }
        string SessionToken { get; }
        string AccountId { get; }
        string RoomId { get; }

        event Action<SessionState> OnStateChanged;
        event Action<Exception> OnError;

        Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
        Task DisconnectAsync();

        Task<LoginResult> LoginAsGuestAsync(string guestId = null, CancellationToken cancellationToken = default);
        Task<LoginResult> LoginWithTokenAsync(string sessionToken, CancellationToken cancellationToken = default);

        Task<RoomJoinResult> JoinRoomAsync(string roomId, CancellationToken cancellationToken = default);
        Task<RoomCreateResult> CreateRoomAsync(string roomId = null, CancellationToken cancellationToken = default);
        Task LeaveRoomAsync(CancellationToken cancellationToken = default);

        Task SubmitFrameInputAsync(ulong roomId, ulong worldId, int frame,
            uint playerId, int inputOpCode, byte[] inputPayload = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 网络会话实现
    /// 管理与服务器的连接、认证和房间会话
    /// </summary>
    public sealed class NetworkSession : INetworkSession
    {
        private readonly INetworkClient _client;
        private readonly ConcurrentDictionary<uint, IServerPushHandler> _pushHandlers = new ConcurrentDictionary<uint, IServerPushHandler>();
        private SessionState _state = SessionState.Disconnected;

        public SessionState State => _state;
        public string SessionToken { get; private set; }
        public string AccountId { get; private set; }
        public string RoomId { get; private set; }

        public event Action<SessionState> OnStateChanged;
        public event Action<Exception> OnError;

        public NetworkSession(INetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.OnServerPush += HandleServerPush;
            _client.OnDisconnected += OnDisconnected;
            _client.OnError += ex => OnError?.Invoke(ex);
        }

        /// <summary>
        /// 创建使用 TCP 网络客户端的会话
        /// </summary>
        public static NetworkSession CreateWithTcp()
        {
            return new NetworkSession(new TcpNetworkClient());
        }

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            SetState(SessionState.Connecting);
            _client.Connect(host, port);

            await Task.Run(() =>
            {
                var spinWait = new SpinWait();
                while (_state == SessionState.Connecting && !cancellationToken.IsCancellationRequested)
                {
                    spinWait.SpinOnce();
                }
            }, cancellationToken);

            if (_state == SessionState.Connecting)
            {
                SetState(SessionState.Disconnected);
            }
        }

        public Task DisconnectAsync()
        {
            _client.Disconnect();
            SetState(SessionState.Disconnected);
            return Task.CompletedTask;
        }

        public async Task<LoginResult> LoginAsGuestAsync(string guestId = null, CancellationToken cancellationToken = default)
        {
            SetState(SessionState.Authenticating);

            try
            {
                var payload = NetworkProtocol.EncodeGuestLoginReq(guestId);
                var response = await _client.SendRequestAsync(NetworkOpCodes.GuestLogin, payload, cancellationToken);
                var result = NetworkProtocol.DecodeGuestLoginResp(response);

                if (result.Success)
                {
                    SessionToken = result.SessionToken;
                    AccountId = result.AccountId;
                    SetState(SessionState.Connected);
                }
                else
                {
                    SetState(SessionState.Disconnected);
                }

                return new LoginResult
                {
                    Success = result.Success,
                    SessionToken = result.SessionToken,
                    AccountId = result.AccountId,
                    Message = result.Message
                };
            }
            catch (Exception ex)
            {
                SetState(SessionState.Disconnected);
                return new LoginResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<LoginResult> LoginWithTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            SessionToken = sessionToken;
            return await Task.FromResult(new LoginResult { Success = true, SessionToken = sessionToken });
        }

        public async Task<RoomJoinResult> JoinRoomAsync(string roomId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(SessionToken))
            {
                return new RoomJoinResult { Success = false, Message = "Not logged in" };
            }

            try
            {
                var payload = NetworkProtocol.EncodeJoinRoomReq(SessionToken, roomId);
                var response = await _client.SendRequestAsync(NetworkOpCodes.JoinRoom, payload, cancellationToken);
                var result = NetworkProtocol.DecodeJoinRoomResp(response);

                if (result.Success)
                {
                    RoomId = result.RoomId;
                    SetState(SessionState.InRoom);
                }

                return new RoomJoinResult
                {
                    Success = result.Success,
                    RoomId = result.RoomId,
                    NumericRoomId = result.NumericRoomId,
                    Message = result.Message
                };
            }
            catch (Exception ex)
            {
                return new RoomJoinResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<RoomCreateResult> CreateRoomAsync(string roomId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(SessionToken))
            {
                return new RoomCreateResult { Success = false, Message = "Not logged in" };
            }

            try
            {
                var payload = NetworkProtocol.EncodeCreateRoomReq(SessionToken, roomId ?? Guid.NewGuid().ToString());
                var response = await _client.SendRequestAsync(NetworkOpCodes.CreateRoom, payload, cancellationToken);
                var result = NetworkProtocol.DecodeCreateRoomResp(response);

                if (result.Success)
                {
                    RoomId = result.RoomId;
                    SetState(SessionState.InRoom);
                }

                return new RoomCreateResult
                {
                    Success = result.Success,
                    RoomId = result.RoomId,
                    Message = result.Message
                };
            }
            catch (Exception ex)
            {
                return new RoomCreateResult { Success = false, Message = ex.Message };
            }
        }

        public Task LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            RoomId = null;
            SetState(SessionState.Connected);
            return Task.CompletedTask;
        }

        public void Subscribe(IServerPushHandler handler)
        {
            _pushHandlers[handler.OpCode] = handler;
        }

        public void Unsubscribe(uint opCode)
        {
            IServerPushHandler removed;
            _pushHandlers.TryRemove(opCode, out removed);
        }

        public async Task SubmitFrameInputAsync(ulong roomId, ulong worldId, int frame,
            uint playerId, int inputOpCode, byte[] inputPayload = null, CancellationToken cancellationToken = default)
        {
            var payload = NetworkProtocol.EncodeSubmitFrameInput(roomId, worldId, frame, playerId, inputOpCode, inputPayload);
            await _client.SendServerPushAsync(NetworkOpCodes.SubmitFrameInput, payload, cancellationToken);
        }

        private void HandleServerPush(uint opCode, byte[] payload)
        {
            IServerPushHandler handler;
            if (_pushHandlers.TryGetValue(opCode, out handler))
            {
                handler.Handle(payload);
            }
        }

        private void OnDisconnected(string reason)
        {
            SetState(SessionState.Disconnected);
        }

        private void SetState(SessionState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        public void Dispose()
        {
            SetState(SessionState.Disposing);
            _client.Dispose();
        }
    }
}
