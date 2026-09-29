#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    /// <summary>
    /// 战斗会话使用的网关连接抽象：房间命令面、连接状态纪元与恢复确认。
    /// 与 <see cref="RoomGatewayConnectionSession"/> 的关系：会话只依赖本接口，
    /// 因此可以在网关前面套故障注入器或测试替身，而无需改变会话实现。
    /// </summary>
    public interface IRoomGatewayBattleConnection : IDisposable
    {
        IRoomGatewaySessionClient Rooms { get; }

        RoomGatewayConnectionState ConnectionState { get; }

        long ConnectionGeneration { get; }

        void Tick(float deltaTime);

        bool CompleteRestore(long generation);
    }

    /// <summary>
    /// 会话发起者的身份与请求期限：房间命令与恢复请求都从这里取 token/region/server。
    /// requestTimeout 小于等于零时回落到 <see cref="DefaultRequestTimeout"/>。
    /// </summary>
    public sealed class RoomGatewaySessionIdentity
    {
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

        public RoomGatewaySessionIdentity(
            string sessionToken,
            string region,
            string serverId,
            string accountId,
            TimeSpan requestTimeout)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            RequestTimeout = requestTimeout > TimeSpan.Zero ? requestTimeout : DefaultRequestTimeout;
        }

        public string SessionToken { get; }

        public string Region { get; }

        public string ServerId { get; }

        /// <summary>房主判定与玩家槽位解析使用的账号 id。</summary>
        public string AccountId { get; }

        public TimeSpan RequestTimeout { get; }
    }

    /// <summary>
    /// 玩法战斗会话的房间侧基类：持有房间生命周期（创建/加入/准备/加载/轮询/退出）、
    /// 恢复与重连纪元、命令 id 台账、Loading 阶段、快照游标与战斗绑定，并用
    /// binding revision + connection generation 保证<b>过期的异步响应永远不会改写
    /// 重新绑定后的会话</b>。玩法只需要提供自己的规则载荷与同步策略钩子。
    ///
    /// 语义约定（派生类不可破坏）：
    /// - 任何异步命令返回后都先通过 <see cref="IsCurrent"/> 系列守卫，再落地状态；
    /// - <see cref="BindRoom"/>、<see cref="InvalidatePendingCommands"/> 与
    ///   <see cref="Dispose"/> 递增 binding revision，使所有在途命令的结果作废；
    /// - 战斗订阅成功后进入基线等待（<see cref="WaitingForBaseline"/>），直到玩法
    ///   应用完整权威快照后调用 <see cref="ClearBaselineWaiting"/>。
    /// </summary>
    public abstract class RoomGatewayBattleSessionBase : IDisposable
    {
        private const string CreateRoomCommandKey = "create-room";
        private const string BeginLoadingCommandKey = "begin-loading";
        private const string LeaveRoomCommandKey = "leave-room";

        private readonly RoomGatewaySessionFlow _flow;
        private readonly RoomGatewayCommandIdLedger _commands = new RoomGatewayCommandIdLedger();
        private readonly RoomGatewayLoadingStage _loading;
        private string _roomId = string.Empty;
        private string _battleId = string.Empty;
        private ulong _worldId;
        private uint _playerId;
        private long _bindingRevision;
        private bool _awaitingBaseline;
        private bool _disposed;

        protected RoomGatewayBattleSessionBase(
            RoomGatewaySessionIdentity identity,
            IRoomGatewayBattleConnection connection,
            Func<RoomGatewaySnapshot, CancellationToken, Task>? prepareAssets = null)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _flow = new RoomGatewaySessionFlow(connection.Rooms);
            prepareAssets ??= static (room, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            };
            _loading = new RoomGatewayLoadingStage(_flow, _commands, prepareAssets);
        }

        protected RoomGatewaySessionIdentity Identity { get; }

        protected IRoomGatewayBattleConnection Connection { get; }

        protected TimeSpan RequestTimeout => Identity.RequestTimeout;

        /// <summary>
        /// 全量快照游标（当前世界与已接受帧）。公开给观测与测试：会话用它判定
        /// 哪些权威快照可以接受，玩法与诊断侧可能需要读取或重置它。
        /// </summary>
        public RoomGatewayFullSnapshotCursor SnapshotCursor { get; } = new RoomGatewayFullSnapshotCursor();

        /// <summary>已发出的全量快照请求数（观测用）。</summary>
        protected int FullSnapshotRequestCount { get; private set; }

        /// <summary>已订阅战斗但尚未应用完整权威快照。</summary>
        protected bool WaitingForBaseline => _awaitingBaseline;

        /// <summary>当前绑定的战斗世界 id；未绑定为 0。</summary>
        protected ulong BattleWorldId => _worldId;

        protected bool IsDisposed => _disposed;

        /// <summary>最近一次完成恢复确认的连接纪元。</summary>
        protected long ObservedConnectionGeneration { get; set; }

        /// <summary>当前 binding revision；玩法在发起长请求前捕获，返回后用于守卫。</summary>
        protected long CurrentBindingRevision => _bindingRevision;

        public string RoomId => _roomId;

        public string BattleId => _battleId;

        public uint PlayerId => _playerId;

        public RoomGatewaySnapshot? Room { get; private set; }

        public string Status { get; protected set; } = "Connected";

        public bool CanStart => Room?.CanStart == true && Room.OwnerAccountId == Identity.AccountId;

        public bool NeedsConnectionRestore =>
            Connection.ConnectionState == RoomGatewayConnectionState.RestoreRequired &&
            Connection.ConnectionGeneration != ObservedConnectionGeneration;

        public bool ConnectionUnavailable =>
            Connection.ConnectionState != RoomGatewayConnectionState.Connected;

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            Connection.Tick(deltaTime);
            OnTick(deltaTime);
        }

        /// <summary>连接 tick 之后的玩法侧推进：应用快照、驱动帧流或回合状态机。</summary>
        protected abstract void OnTick(float deltaTime);

        public async Task RestoreAsync(CancellationToken cancellationToken) =>
            await RestoreCoreAsync(cancellationToken);

        /// <summary>
        /// 恢复到服务端当前房间状态。空房间会清空本地绑定；战斗中的房间继续订阅。
        /// 过期结果（重连或重绑定后到达）直接丢弃并返回 false。
        /// </summary>
        protected async Task<bool> RestoreCoreAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var result = await _flow.RestoreWithoutPlayerIdAsync(
                Identity.SessionToken, Identity.Region, Identity.ServerId,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision)) return false;
            if (result.RestoreStatus == RoomGatewaySessionRestoreStatus.InvalidSession)
                throw new UnauthorizedAccessException(result.Message);
            if (result.CanRetry)
                throw new TimeoutException(result.Message);
            if (result.RestoreStatus == RoomGatewaySessionRestoreStatus.Failed)
                throw new InvalidOperationException(result.Message);
            if (!string.IsNullOrWhiteSpace(result.RoomId) &&
                result.NextStep != RoomGatewayStagedRestoreNextStep.None)
            {
                if (_roomId != result.RoomId) BindRoom(result.RoomId);
                SetRoom(result.Snapshot);
                revision = _bindingRevision;
                Status = DescribeRoomStatus(result.Snapshot, result.Phase);
                if (result.Phase == RoomGatewaySessionPhase.InBattle && result.Snapshot != null)
                {
                    await SubscribeToBattleAsync(result.Snapshot, cancellationToken);
                    if (!ReferenceEquals(Room, result.Snapshot) ||
                        _battleId != result.Snapshot.BattleId || _worldId != result.Snapshot.WorldId)
                        return false;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_roomId)) BindRoom(string.Empty);
                // BindRoom 递增了 revision；空房间路径的绑定本身就是本次恢复的结果，
                // 重新捕获后再做守卫（与重连/重绑定作废在途结果的语义区分开）。
                revision = _bindingRevision;
                Status = DescribeRestoreStatus(result.RestoreStatus);
            }
            return IsCurrent(generation, revision);
        }

        public async Task RecoverConnectionAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!NeedsConnectionRestore) return;
            var generation = Connection.ConnectionGeneration;
            if (!await RestoreCoreAsync(cancellationToken)) return;
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || generation != Connection.ConnectionGeneration) return;
            if (Connection.CompleteRestore(generation)) ObservedConnectionGeneration = generation;
        }

        /// <summary>
        /// 建房并加入。创建请求由 <see cref="CreateRoomRequest"/> 构造；被拒绝时命令 id
        /// 立即作废（下次重试生成新 id），超时等未确认结果保留命令 id 供重试复用。
        /// </summary>
        protected async Task CreateRoomCoreAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            if (!string.IsNullOrEmpty(_roomId))
                throw new InvalidOperationException(CreatePendingRoomMessage);
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var currentRoomId = _roomId;
            var result = await Connection.Rooms.CreateRoomAsync(
                CreateRoomRequest(_commands.GetOrCreate(CreateRoomCommandKey)),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, currentRoomId)) return;
            if (!result.Success)
            {
                _commands.Complete(CreateRoomCommandKey);
                OnCreateRoomRejected();
                throw new InvalidOperationException(result.Message);
            }
            await JoinRoomAsync(result.RoomId, cancellationToken);
        }

        protected abstract RoomGatewayCreateRequest CreateRoomRequest(string commandId);

        /// <summary>建房请求被服务端拒绝后的清理（例如清除待重试的建房参数）。</summary>
        protected virtual void OnCreateRoomRejected()
        {
        }

        public async Task JoinRoomAsync(string roomId, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("Room ID is required.", nameof(roomId));
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var currentRoomId = _roomId;
            var result = await Connection.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
                Identity.SessionToken, Identity.Region, Identity.ServerId, roomId.Trim()),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, currentRoomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(result.RoomId ?? roomId.Trim());
            Status = "Joined";
        }

        public async Task SetReadyAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await Connection.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
                Identity.SessionToken, roomId, true), timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Status = "Ready";
        }

        public async Task BeginLoadingAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ThrowIfConnectionUnavailable();
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await _flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
                Identity.SessionToken, roomId, null, _commands.GetOrCreate(BeginLoadingCommandKey)),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            _commands.Complete(BeginLoadingCommandKey);
            Status = "Loading";
        }

        public async Task PollAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_roomId)) return;
            var roomId = _roomId;
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var result = await _flow.GetSnapshotAsync(Identity.SessionToken, roomId,
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision) || roomId != _roomId) return;
            if (!result.Success || result.Snapshot == null)
                throw new InvalidOperationException(result.Message);
            var room = result.Snapshot;
            SetRoom(room);
            Status = DescribeRoomStatus(room, room.Phase);
            revision = _bindingRevision;
            await _loading.AdvanceAsync(Identity.SessionToken, room, cancellationToken, RequestTimeout);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentBattle(generation, revision, roomId, room)) return;
            if (room.Phase == RoomGatewaySessionPhase.InBattle &&
                (_battleId != room.BattleId || _worldId != room.WorldId))
                await SubscribeToBattleAsync(room, cancellationToken);
        }

        public async Task LeaveLobbyAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_roomId) || !string.IsNullOrEmpty(_battleId)) return;
            ThrowIfConnectionUnavailable();
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var result = await _flow.LeaveRoomAsync(new RoomGatewayLeaveRequest(
                Identity.SessionToken, roomId, null, _commands.GetOrCreate(LeaveRoomCommandKey)),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCommand(generation, revision, roomId)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            BindRoom(string.Empty);
        }

        /// <summary>
        /// 订阅战斗：校验启动清单、协商同步能力、解析玩家槽位、订阅状态推送，
        /// 可选订阅战斗通道（帧流），最后绑定战斗并请求完整基线。
        /// </summary>
        protected async Task SubscribeToBattleAsync(RoomGatewaySnapshot room, CancellationToken cancellationToken)
        {
            ValidateLaunchManifest(room);
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            var roomId = _roomId;
            var modelName = ResolveSubscriptionModelName(room);
            var profile = ResolveSubscriptionProfile(room);
            var binding = RoomGatewayNetworkSyncSessionBinding.Create(
                room.SyncCapabilities, modelName,
                NetworkSyncRemoteCapabilityPolicy.Require);
            var options = new NetworkSyncSessionOptions
            {
                RequiredProfile = profile,
                RequiredMinimumSchemaVersion = MinimumSchemaVersion,
                RequiredMaximumSchemaVersion = MaximumSchemaVersion,
                AvailableCapabilities = NetworkSyncCapabilities.FromProfile(in profile,
                    MinimumSchemaVersion, MaximumSchemaVersion)
            };
            if (!binding.Negotiate(options).IsRemoteNegotiated)
                throw new InvalidOperationException(SyncNotNegotiatedMessage);

            var playerId = ResolvePlayerSlot(room);
            var result = await Connection.Rooms.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(Identity.SessionToken, room.BattleId, roomId),
                timeout: RequestTimeout,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentBattle(generation, revision, roomId, room)) return;
            if (!result.Success) throw new InvalidOperationException(result.Message);
            await SubscribeBattleChannelsAsync(room, roomId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentBattle(generation, revision, roomId, room)) return;

            var previousBattleId = _battleId;
            var previousWorldId = _worldId;
            _battleId = room.BattleId;
            _worldId = room.WorldId;
            _playerId = playerId;
            SnapshotCursor.Reset(_worldId);
            _awaitingBaseline = true;
            OnBattleBound(room, playerId, previousBattleId, previousWorldId);
            await RequestFullSnapshotCoreAsync(BaselineRequestReason, cancellationToken);
        }

        /// <summary>请求完整权威快照；期限取 <see cref="RequestTimeout"/>，过期抛 <see cref="TimeoutException"/>。</summary>
        protected async Task RequestFullSnapshotCoreAsync(string reason, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(_battleId) || _worldId == 0)
                throw new InvalidOperationException(BattleNotSubscribedMessage);
            var generation = Connection.ConnectionGeneration;
            var revision = _bindingRevision;
            OnBeforeFullSnapshotRequest();
            FullSnapshotRequestCount++;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(RequestTimeout);
            WireRequestFullStateSyncRes response;
            try
            {
                response = await SendFullSnapshotRequestAsync(new WireRequestFullStateSyncReq
                {
                    SessionToken = Identity.SessionToken,
                    BattleId = _battleId,
                    RoomId = _roomId,
                    WorldId = _worldId,
                    ClientFrame = SnapshotCursor.LastFrame,
                    LastAuthoritativeFrame = SnapshotCursor.LastFrame,
                    Reason = reason ?? string.Empty
                }, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(FullSnapshotTimeoutMessage);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, revision)) return;
            if (!response.Success || !response.Accepted)
                throw new InvalidOperationException(response.Message);
        }

        /// <summary>清空房间绑定并让所有在途命令作废；玩法状态在 <see cref="OnRoomUnbound"/> 里清理。</summary>
        protected void BindRoom(string roomId)
        {
            _bindingRevision++;
            _roomId = roomId;
            _battleId = string.Empty;
            _worldId = 0;
            _playerId = 0;
            SnapshotCursor.Reset(0);
            _awaitingBaseline = false;
            _loading.Reset();
            _commands.Clear();
            Room = null;
            OnRoomUnbound();
        }

        /// <summary>让当前所有在途命令的结果作废（不改变绑定本身）。</summary>
        protected void InvalidatePendingCommands() => _bindingRevision++;

        /// <summary>重新进入基线等待（例如连接失效后丢弃本地预测）。</summary>
        protected void BeginBaselineWaiting() => _awaitingBaseline = true;

        /// <summary>玩法成功应用完整权威快照后解除基线等待。</summary>
        protected void ClearBaselineWaiting() => _awaitingBaseline = false;

        protected void SetRoom(RoomGatewaySnapshot? room)
        {
            _bindingRevision++;
            Room = room;
        }

        protected bool IsCurrent(long generation, long revision) =>
            !_disposed && generation == Connection.ConnectionGeneration && revision == _bindingRevision &&
            Connection.ConnectionState != RoomGatewayConnectionState.Reconnecting &&
            Connection.ConnectionState != RoomGatewayConnectionState.Exhausted;

        protected bool IsCurrentCommand(long generation, long revision, string roomId) =>
            IsCurrent(generation, revision) &&
            Connection.ConnectionState == RoomGatewayConnectionState.Connected &&
            roomId == _roomId;

        protected bool IsCurrentBattle(long generation, long revision, string roomId,
            RoomGatewaySnapshot room) =>
            IsCurrent(generation, revision) && roomId == _roomId &&
            ReferenceEquals(Room, room);

        protected void ThrowIfConnectionUnavailable()
        {
            if (ConnectionUnavailable)
                throw new InvalidOperationException("Room connection is not ready.");
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bindingRevision++;
            OnDisposing();
            Connection.Dispose();
        }

        private uint ResolvePlayerSlot(RoomGatewaySnapshot room)
        {
            var playerId = 0u;
            foreach (var player in room.Players)
                if (player.AccountId == Identity.AccountId) playerId = player.PlayerId;
            if (playerId == 0) throw new InvalidOperationException(PlayerSlotMissingMessage);
            return playerId;
        }

        /// <summary>校验启动清单（规则/资产标识），不一致时抛异常以阻止订阅。</summary>
        protected abstract void ValidateLaunchManifest(RoomGatewaySnapshot room);

        /// <summary>协商用的档案名（兼容模型名）；由玩法按房间能力决定。</summary>
        protected abstract string ResolveSubscriptionModelName(RoomGatewaySnapshot room);

        /// <summary>本玩法要求的 <see cref="NetworkSyncProfile"/>。</summary>
        protected abstract NetworkSyncProfile ResolveSubscriptionProfile(RoomGatewaySnapshot room);

        /// <summary>订阅状态推送之外的战斗通道（例如帧流）；默认无。</summary>
        protected virtual Task SubscribeBattleChannelsAsync(
            RoomGatewaySnapshot room, string roomId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <summary>战斗绑定完成后的玩法侧收尾（创建复制器、清空规则状态、刷新界面状态）。</summary>
        protected virtual void OnBattleBound(
            RoomGatewaySnapshot room, uint playerId, string previousBattleId, ulong previousWorldId)
        {
        }

        /// <summary>房间解绑后的玩法侧清理。</summary>
        protected virtual void OnRoomUnbound()
        {
        }

        /// <summary>连接释放前的解钩（事件退订等）。</summary>
        protected virtual void OnDisposing()
        {
        }

        /// <summary>发送全量快照请求；期限已由基类的 deadline token 表达。</summary>
        protected abstract Task<WireRequestFullStateSyncRes> SendFullSnapshotRequestAsync(
            WireRequestFullStateSyncReq request, CancellationToken cancellationToken);

        /// <summary>快照请求发出前的准备（例如冻结帧收件箱）。</summary>
        protected virtual void OnBeforeFullSnapshotRequest()
        {
        }

        protected virtual int MinimumSchemaVersion => 1;

        protected virtual int MaximumSchemaVersion => 1;

        protected virtual string CreatePendingRoomMessage => "Leave the current room before creating another.";

        protected virtual string SyncNotNegotiatedMessage => "Sync capabilities were not negotiated.";

        protected virtual string PlayerSlotMissingMessage => "Player slot is missing.";

        protected virtual string BattleNotSubscribedMessage => "Battle is not subscribed.";

        protected virtual string FullSnapshotTimeoutMessage => "Snapshot request timed out.";

        protected virtual string BaselineRequestReason => "Subscription baseline";

        /// <summary>轮询/恢复后的状态文案；玩法可叠加自己的子状态（如等待回合确认）。</summary>
        protected virtual string DescribeRoomStatus(RoomGatewaySnapshot? room, RoomGatewaySessionPhase phase) =>
            phase.ToString();

        protected virtual string DescribeRestoreStatus(RoomGatewaySessionRestoreStatus restoreStatus) =>
            restoreStatus.ToString();
    }
}
