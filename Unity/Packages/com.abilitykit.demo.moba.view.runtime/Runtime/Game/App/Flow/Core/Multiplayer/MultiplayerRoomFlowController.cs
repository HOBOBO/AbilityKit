#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 多人房间流程状态，供 UI 和 Flow 观察。
    /// </summary>
    public enum MultiplayerRoomFlowState
    {
        /// <summary>未开始。</summary>
        Idle = 0,
        /// <summary>正在登录。</summary>
        LoggingIn = 1,
        /// <summary>正在创建房间。</summary>
        CreatingRoom = 2,
        /// <summary>正在加入房间。</summary>
        JoiningRoom = 3,
        /// <summary>在大厅，等待选英雄/Ready。</summary>
        InLobby = 4,
        /// <summary>正在加载资源。</summary>
        LoadingAssets = 5,
        /// <summary>等待服务端开战。</summary>
        WaitingForBattle = 6,
        /// <summary>已进入战斗。</summary>
        InBattle = 7,
        /// <summary>失败。</summary>
        Failed = 8,
        /// <summary>Waiting for the authoritative room leave command.</summary>
        LeavingRoom = 9
    }

    /// <summary>
    /// 多人房间流程的快照视图（纯 C#，零 Unity 依赖）。
    /// 由 <see cref="IRoomSnapshotProvider"/> 投影，供控制器与 UI 共享。
    /// </summary>
    public sealed class MultiplayerRoomSnapshot
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerAccountId { get; set; } = string.Empty;
        public ulong NumericRoomId { get; set; }
        public MultiplayerRoomPhase Phase { get; set; }
        public string PhaseReason { get; set; } = string.Empty;
        public bool CanStart { get; set; }
        public string BattleId { get; set; } = string.Empty;
        public ulong WorldId { get; set; }
        public long LaunchGeneration { get; set; }
        public int LaunchManifestVersion { get; set; }
        public string LaunchManifestHash { get; set; } = string.Empty;
        public long LoadingDeadlineUnixMs { get; set; }
        public long RoomRevision { get; set; }
        public long LastEventSequence { get; set; }
        public string LastStartFailureCode { get; set; } = string.Empty;
        public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
        public IReadOnlyList<MultiplayerRoomPlayerSnapshot> Players { get; set; } = Array.Empty<MultiplayerRoomPlayerSnapshot>();

        public bool AllPlayersReady
        {
            get
            {
                if (Players == null || Players.Count == 0) return false;
                for (var i = 0; i < Players.Count; i++)
                {
                    var player = Players[i];
                    if (!player.IsOnline || player.HeroId <= 0 || !player.LobbyReady) return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Accepts an authoritative battle identity once so normal start and reconnect use the same entry gate.
    /// </summary>
    public sealed class MultiplayerBattleEntryGate
    {
        private string _roomId = string.Empty;
        private string _battleId = string.Empty;
        private ulong _worldId;
        private long _launchGeneration = -1;

        public bool TryAccept(
            MultiplayerRoomFlowState flowState,
            MultiplayerRoomSnapshot? snapshot)
        {
            if (!CanEnter(flowState, snapshot)) return false;
            if (string.Equals(_roomId, snapshot!.RoomId, StringComparison.Ordinal) &&
                string.Equals(_battleId, snapshot.BattleId, StringComparison.Ordinal) &&
                _worldId == snapshot.WorldId &&
                _launchGeneration == snapshot.LaunchGeneration)
            {
                return false;
            }

            _roomId = snapshot.RoomId;
            _battleId = snapshot.BattleId;
            _worldId = snapshot.WorldId;
            _launchGeneration = snapshot.LaunchGeneration;
            return true;
        }

        public void Reset()
        {
            _roomId = string.Empty;
            _battleId = string.Empty;
            _worldId = 0UL;
            _launchGeneration = -1;
        }

        public static bool CanEnter(
            MultiplayerRoomFlowState flowState,
            MultiplayerRoomSnapshot? snapshot)
        {
            return flowState == MultiplayerRoomFlowState.InBattle &&
                   snapshot?.Phase == MultiplayerRoomPhase.InBattle &&
                   !string.IsNullOrWhiteSpace(snapshot.RoomId) &&
                   snapshot.NumericRoomId > 0UL &&
                   !string.IsNullOrWhiteSpace(snapshot.BattleId) &&
                   snapshot.WorldId > 0UL;
        }
    }

    public sealed class MultiplayerRoomPlayerSnapshot
    {
        public string AccountId { get; set; } = string.Empty;
        public uint PlayerId { get; set; }
        public int TeamId { get; set; }
        public int HeroId { get; set; }
        public int SpawnPointId { get; set; }
        public int Level { get; set; }
        public int AttributeTemplateId { get; set; }
        public int BasicAttackSkillId { get; set; }
        public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
        public bool LobbyReady { get; set; }
        public bool AssetsLoaded { get; set; }
        public int LoadingProgress { get; set; }
        public bool IsOnline { get; set; }
        public long JoinOrdinal { get; set; }
        public int LoadedManifestVersion { get; set; }
        public string LoadedManifestHash { get; set; } = string.Empty;
        public long LastSeenTicks { get; set; }
        public long OfflineSinceTicks { get; set; }
    }

    /// <summary>
    /// 多人房间阶段（与服务端 RoomPhase 对齐的纯 C# 镜像）。
    /// </summary>
    public enum MultiplayerRoomPhase
    {
        Lobby = 0,
        Loading = 1,
        Starting = 2,
        InBattle = 3,
        Closing = 4,
        Closed = 5,
        Expired = 6
    }

    public enum MultiplayerRoomRestoreNextStep
    {
        None = 0,
        SetReadyAndBeginLoading = 1,
        ReportAssetsLoaded = 2,
        WaitForBattleStart = 3,
        EnterBattle = 4
    }

    public enum MultiplayerRoomRestoreStatus
    {
        Restored = 0,
        NoActiveRoom = 1,
        NotMember = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        Failed = 7
    }

    public enum MultiplayerRoomRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Timeout = 6,
        InternalError = 7
    }

    public enum MultiplayerRoomEntryKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public readonly struct MultiplayerRoomRestoreResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly uint PlayerId;
        public readonly MultiplayerRoomPhase Phase;
        public readonly MultiplayerRoomRestoreNextStep NextStep;
        public readonly MultiplayerRoomEntryKind EntryKind;
        public readonly bool CanStart;
        public readonly string Message;
        public readonly MultiplayerRoomRestoreStatus Status;
        public readonly MultiplayerRoomRestoreErrorCode ErrorCode;

        public MultiplayerRoomRestoreResult(
            string roomId,
            ulong numericRoomId,
            uint playerId,
            MultiplayerRoomPhase phase,
            MultiplayerRoomRestoreNextStep nextStep,
            MultiplayerRoomEntryKind entryKind,
            bool canStart,
            string message,
            MultiplayerRoomRestoreStatus status,
            MultiplayerRoomRestoreErrorCode errorCode)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
            Phase = phase;
            NextStep = nextStep;
            EntryKind = entryKind;
            CanStart = canStart;
            Message = message ?? string.Empty;
            Status = status;
            ErrorCode = errorCode;
        }

        public bool HasActiveRoom =>
            Status == MultiplayerRoomRestoreStatus.Restored &&
            !string.IsNullOrWhiteSpace(RoomId);

        public bool CanRetry =>
            Status == MultiplayerRoomRestoreStatus.Timeout ||
            (Status == MultiplayerRoomRestoreStatus.Failed &&
             ErrorCode == MultiplayerRoomRestoreErrorCode.InternalError);
    }

    public readonly struct MultiplayerRoomJoinResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly uint PlayerId;

        public MultiplayerRoomJoinResult(string roomId, ulong numericRoomId, uint playerId)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// 选英雄/配置出战的参数。
    /// </summary>
    public readonly struct MultiplayerLoadoutSpec
    {
        public readonly int HeroId;
        public readonly int TeamId;
        public readonly int SpawnPointId;
        public readonly int Level;
        public readonly int AttributeTemplateId;
        public readonly int BasicAttackSkillId;
        public readonly int[] SkillIds;

        public MultiplayerLoadoutSpec(
            int heroId,
            int teamId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            int[]? skillIds)
        {
            HeroId = heroId;
            TeamId = teamId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds ?? Array.Empty<int>();
        }
    }

    /// <summary>
    /// 创建/加入房间的启动参数（对应 RoomGatewayLaunchSpec 的纯 C# 子集）。
    /// </summary>
    public sealed class MultiplayerRoomLaunchSpec
    {
        public string SessionToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public string RoomType { get; set; } = "default";
        public string RoomTitle { get; set; } = string.Empty;
        public int MaxPlayers { get; set; } = 2;
        public int MinPlayers { get; set; } = 2;
        public int GameplayId { get; set; } = 1;
        public int RuleSetId { get; set; } = 1;
        public int ConfigVersion { get; set; } = 1;
        public int ProtocolVersion { get; set; } = 1;
        public string WorldType { get; set; } = "moba";
        public string ClientId { get; set; } = "moba-client";
    }

    /// <summary>
    /// 抽象 RoomGatewaySessionFlow 的分阶段 API，使控制器可测试（零 host.extension 依赖）。
    /// </summary>
    public interface IMultiplayerRoomSession
    {
        /// <summary>Restores the authoritative room and reports the first unfinished stage.</summary>
        Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken);

        /// <summary>阶段 1：创建房间，返回 roomId。</summary>
        Task<string> CreateRoomAsync(MultiplayerRoomLaunchSpec spec, CancellationToken cancellationToken);

        /// <summary>阶段 2：加入房间。</summary>
        Task<MultiplayerRoomJoinResult> JoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken);

        /// <summary>Leaves the authoritative room membership.</summary>
        Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 3：配置出战（PickHero）。</summary>
        Task ConfigureLoadoutAsync(string roomId, MultiplayerLoadoutSpec loadout, CancellationToken cancellationToken);

        /// <summary>阶段 4：设置准备状态。</summary>
        Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken);

        /// <summary>阶段 5：Owner 发起资源加载阶段。</summary>
        Task BeginLoadingAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 6：成员上报资源加载完成。</summary>
        Task ReportAssetsLoadedAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>Reports monotonic local loading progress to the authoritative room snapshot.</summary>
        Task ReportLoadingProgressAsync(string roomId, int progress, CancellationToken cancellationToken);

        /// <summary>Owner cancels the current loading generation and returns the room to Lobby.</summary>
        Task CancelLoadingAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>阶段 7：等待战斗开始（轮询直到 Phase 进入 Starting/InBattle 或超时）。</summary>
        Task WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 抽象 ClientRoomStore 的快照订阅能力，使控制器可测试。
    /// </summary>
    public interface IRoomSnapshotProvider
    {
        /// <summary>当前快照（或 null）。</summary>
        MultiplayerRoomSnapshot? Current { get; }

        /// <summary>快照变更事件。</summary>
        event Action<MultiplayerRoomSnapshot>? OnSnapshotChanged;
    }

    public interface IMultiplayerBattleAssetLoader
    {
        Task LoadAsync(
            MultiplayerRoomSnapshot snapshot,
            IProgress<MultiplayerAssetLoadProgress> progress,
            CancellationToken cancellationToken);
        void Release();
    }

    public readonly struct MultiplayerAssetLoadProgress
    {
        public readonly int Progress;
        public readonly int LoadedCount;
        public readonly int TotalCount;
        public readonly string CurrentAssetKey;

        public MultiplayerAssetLoadProgress(int progress, int loadedCount, int totalCount, string currentAssetKey)
        {
            Progress = Math.Max(0, Math.Min(100, progress));
            LoadedCount = loadedCount;
            TotalCount = totalCount;
            CurrentAssetKey = currentAssetKey ?? string.Empty;
        }
    }

    /// <summary>
    /// 多人房间流程控制器：编排 登录→建房/入房→选英雄→Ready→BeginLoading→ReportAssets→WaitForBattle。
    /// <para>
    /// 纯 C#，零 Unity 依赖。通过 <see cref="IMultiplayerRoomSession"/> 与 <see cref="IRoomSnapshotProvider"/>
    /// 抽象与外部（RoomGatewaySessionFlow / ClientRoomStore）交互，使其可在无 Unity/host.extension 的测试项目中测试。
    /// </para>
    /// </summary>
    internal sealed class MultiplayerRoomFlowController : IDisposable
    {
        private readonly IMultiplayerRoomSession _session;
        private readonly IRoomSnapshotProvider _snapshotProvider;
        private readonly IMultiplayerBattleAssetLoader? _assetLoader;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _stageGate = new object();
        private readonly object _loadingProgressGate = new object();
        private CancellationTokenSource? _stageCancellation;
        private Task _stageTask = Task.CompletedTask;
        private long _stageGeneration = -1;
        private bool _disposed;
        private int _localLoadingProgress;
        private string _currentLoadingAssetKey = string.Empty;
        private bool _localLoadingCompleted;
        private bool _createdRoomOwner;

        /// <summary>状态变更回调。每次 <see cref="CurrentState"/> 变化时触发。</summary>
        public event Action<MultiplayerRoomFlowState>? StateChanged;

        /// <summary>当前状态。</summary>
        public MultiplayerRoomFlowState CurrentState { get; private set; }

        /// <summary>当前房间快照（从 IRoomSnapshotProvider 投影）。</summary>
        public MultiplayerRoomSnapshot? CurrentSnapshot { get; private set; }

        /// <summary>最近一次错误信息（进入 Failed 状态时设置）。</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>当前房间 Id（创建/加入成功后设置）。</summary>
        public string CurrentRoomId { get; private set; } = string.Empty;

        public uint LocalPlayerId { get; private set; }

        public string LocalAccountId { get; private set; } = string.Empty;

        public bool IsLocalRoomOwner
        {
            get
            {
                var snapshot = CurrentSnapshot;
                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.OwnerAccountId))
                {
                    if (!string.IsNullOrWhiteSpace(LocalAccountId))
                    {
                        return string.Equals(
                            LocalAccountId,
                            snapshot.OwnerAccountId,
                            StringComparison.Ordinal);
                    }

                    if (LocalPlayerId == 0u || snapshot.Players == null)
                    {
                        return _createdRoomOwner;
                    }

                    for (var i = 0; i < snapshot.Players.Count; i++)
                    {
                        var player = snapshot.Players[i];
                        if (player.PlayerId == LocalPlayerId)
                        {
                            return string.Equals(
                                player.AccountId,
                                snapshot.OwnerAccountId,
                                StringComparison.Ordinal);
                        }
                    }
                }

                return _createdRoomOwner;
            }
        }

        public bool CanLeaveCurrentRoom
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CurrentRoomId)) return false;
                var phase = CurrentSnapshot?.Phase;
                return phase == MultiplayerRoomPhase.Lobby ||
                       phase == MultiplayerRoomPhase.Loading;
            }
        }

        public MultiplayerRoomRestoreResult? LastRestoreResult { get; private set; }

        public MultiplayerRoomLaunchSpec? CurrentLaunchSpec { get; private set; }

        public int LocalLoadingProgress
        {
            get { lock (_loadingProgressGate) return _localLoadingProgress; }
        }

        public string CurrentLoadingAssetKey
        {
            get { lock (_loadingProgressGate) return _currentLoadingAssetKey; }
        }

        public MultiplayerRoomFlowController(
            IMultiplayerRoomSession session,
            IRoomSnapshotProvider snapshotProvider,
            IMultiplayerBattleAssetLoader? assetLoader = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _assetLoader = assetLoader;
            _snapshotProvider.OnSnapshotChanged += HandleSnapshotChanged;
            CurrentSnapshot = _snapshotProvider.Current;
        }

        /// <summary>
        /// 启动创建房间流程：Idle → LoggingIn → CreatingRoom → InLobby。
        /// </summary>
        public async Task StartCreateRoomAsync(MultiplayerRoomLaunchSpec spec, CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;
            _createdRoomOwner = true;
            LocalPlayerId = 0u;
            await RunAsync(
                async ct =>
                {
                    Transition(MultiplayerRoomFlowState.LoggingIn);
                    Transition(MultiplayerRoomFlowState.CreatingRoom);
                    var roomId = await _session.CreateRoomAsync(spec, ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(roomId))
                    {
                        throw new InvalidOperationException("创建房间成功但未返回 roomId。");
                    }

                    var joined = await _session.JoinRoomAsync(spec, roomId, ct).ConfigureAwait(false);
                    ApplyJoinResult(roomId, in joined);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 启动加入房间流程：Idle → LoggingIn → JoiningRoom → InLobby。
        /// </summary>
        public async Task StartJoinRoomAsync(MultiplayerRoomLaunchSpec spec, string roomId, CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId 不能为空。", nameof(roomId));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;
            _createdRoomOwner = false;
            LocalPlayerId = 0u;
            await RunAsync(
                async ct =>
                {
                    Transition(MultiplayerRoomFlowState.LoggingIn);
                    Transition(MultiplayerRoomFlowState.JoiningRoom);
                    var joined = await _session.JoinRoomAsync(spec, roomId, ct).ConfigureAwait(false);
                    ApplyJoinResult(roomId, in joined);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken = default)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (fallbackPlayerId == 0u) throw new ArgumentOutOfRangeException(nameof(fallbackPlayerId));
            CurrentLaunchSpec = spec;
            LocalAccountId = spec.AccountId?.Trim() ?? string.Empty;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastError = string.Empty;
                Transition(MultiplayerRoomFlowState.LoggingIn);
                var restored = await _session.RestoreAsync(
                    spec,
                    fallbackPlayerId,
                    cancellationToken).ConfigureAwait(false);
                LastRestoreResult = restored;
                _createdRoomOwner = false;

                if (!restored.HasActiveRoom)
                {
                    CurrentRoomId = string.Empty;
                    LocalPlayerId = 0u;
                    CurrentSnapshot = null;
                    if (restored.Status == MultiplayerRoomRestoreStatus.NoActiveRoom)
                    {
                        Transition(MultiplayerRoomFlowState.Idle);
                    }
                    else
                    {
                        Fail(string.IsNullOrWhiteSpace(restored.Message)
                            ? $"Room restore failed: {restored.Status}/{restored.ErrorCode}."
                            : restored.Message);
                    }

                    return restored;
                }

                if (restored.PlayerId == 0u)
                {
                    throw new InvalidOperationException(
                        "Room restore succeeded without an authoritative player id.");
                }

                CurrentRoomId = restored.RoomId;
                LocalPlayerId = restored.PlayerId;
                var nextState = MapRestoreNextStepToState(restored.NextStep);
                if (nextState == MultiplayerRoomFlowState.Failed)
                {
                    Fail(string.IsNullOrWhiteSpace(restored.Message)
                        ? $"Room restore cannot continue from phase {restored.Phase}."
                        : restored.Message);
                }
                else
                {
                    Transition(nextState);
                    StartPendingStage();
                }
                return restored;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 选英雄/配置出战。仅在 InLobby 状态可用。
        /// </summary>
        public Task PickHeroAsync(MultiplayerLoadoutSpec loadout, CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    await _session.ConfigureLoadoutAsync(CurrentRoomId, loadout, ct).ConfigureAwait(false);
                },
                cancellationToken);
        }

        /// <summary>
        /// 设置准备状态。仅在 InLobby 状态可用。
        /// </summary>
        public Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    await _session.SetReadyAsync(CurrentRoomId, ready, ct).ConfigureAwait(false);
                },
                cancellationToken);
        }

        /// <summary>
        /// Owner 发起资源加载：InLobby → LoadingAssets。
        /// </summary>
        public Task BeginLoadingAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.InLobby);
                    if (!IsLocalRoomOwner)
                    {
                        throw new InvalidOperationException("Only the room owner can begin loading.");
                    }
                    if (CurrentSnapshot?.CanStart != true)
                    {
                        throw new InvalidOperationException("Room is not ready to begin loading.");
                    }
                    await _session.BeginLoadingAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.LoadingAssets);
                    if (_assetLoader != null)
                    {
                        await ResumePendingStageAsync(ct).ConfigureAwait(false);
                    }
                },
                cancellationToken);
        }

        /// <summary>
        /// 成员上报资源加载完成：LoadingAssets → WaitingForBattle。
        /// </summary>
        public Task ReportAssetsLoadedAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.LoadingAssets);
                    await _session.ReportAssetsLoadedAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.WaitingForBattle);
                },
                cancellationToken);
        }

        public Task CancelLoadingAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    if (CurrentState != MultiplayerRoomFlowState.LoadingAssets &&
                        CurrentState != MultiplayerRoomFlowState.WaitingForBattle)
                    {
                        throw new InvalidOperationException(
                            $"Cannot cancel loading while flow is {CurrentState}.");
                    }

                    if (!IsLocalRoomOwner)
                    {
                        throw new InvalidOperationException("Only the room owner can cancel loading.");
                    }

                    CancelPendingStage(releaseAssets: true);
                    await _session.CancelLoadingAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.InLobby);
                },
                cancellationToken);
        }

        public async Task LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanLeaveCurrentRoom)
            {
                throw new InvalidOperationException(
                    $"Cannot leave the room while flow is {CurrentState} and phase is {CurrentSnapshot?.Phase}.");
            }

            var previousState = CurrentState;
            LastError = string.Empty;
            Transition(MultiplayerRoomFlowState.LeavingRoom);
            try
            {
                await _session.LeaveRoomAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                CancelPendingStage(releaseAssets: true);
                CurrentSnapshot = null;
                CurrentRoomId = string.Empty;
                LocalPlayerId = 0u;
                LocalAccountId = string.Empty;
                _createdRoomOwner = false;
                LastRestoreResult = null;
                CurrentLaunchSpec = null;
                Transition(MultiplayerRoomFlowState.Idle);
            }
            catch (OperationCanceledException)
            {
                Transition(previousState);
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message ?? string.Empty;
                Transition(previousState);
                throw;
            }
        }

        /// <summary>
        /// 等待服务端开战：WaitingForBattle → InBattle。
        /// </summary>
        public Task WaitForBattleStartAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                async ct =>
                {
                    EnsureState(MultiplayerRoomFlowState.WaitingForBattle);
                    await _session.WaitForBattleStartAsync(CurrentRoomId, ct).ConfigureAwait(false);
                    Transition(MultiplayerRoomFlowState.InBattle);
                },
                cancellationToken);
        }

        /// <summary>
        /// 取消当前流程，回到 Idle。
        /// </summary>
        public void Cancel()
        {
            CancelPendingStage(releaseAssets: true);
            CurrentSnapshot = null;
            CurrentRoomId = string.Empty;
            LocalPlayerId = 0u;
            LocalAccountId = string.Empty;
            _createdRoomOwner = false;
            LastError = string.Empty;
            LastRestoreResult = null;
            CurrentLaunchSpec = null;
            Transition(MultiplayerRoomFlowState.Idle);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            CancelPendingStage(releaseAssets: true);
            _snapshotProvider.OnSnapshotChanged -= HandleSnapshotChanged;
            _lifetime.Dispose();
        }

        public Task ResumePendingStageAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (CurrentState == MultiplayerRoomFlowState.InBattle)
            {
                return Task.CompletedTask;
            }

            var snapshot = CurrentSnapshot;
            if (snapshot == null) return Task.CompletedTask;
            if (snapshot.Phase != MultiplayerRoomPhase.Loading &&
                snapshot.Phase != MultiplayerRoomPhase.Starting)
            {
                return Task.CompletedTask;
            }

            lock (_stageGate)
            {
                if (!_stageTask.IsCompleted &&
                    _stageGeneration == snapshot.LaunchGeneration)
                {
                    return _stageTask;
                }

                var previousTask = _stageTask;
                CancelPendingStageLocked(releaseAssets: false);
                _stageGeneration = snapshot.LaunchGeneration;
                _stageCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetime.Token,
                    cancellationToken);
                _stageTask = ResumeAfterPreviousStageAsync(
                    previousTask,
                    snapshot,
                    _stageCancellation.Token);
                return _stageTask;
            }
        }

        private async Task ResumeAfterPreviousStageAsync(
            Task previousTask,
            MultiplayerRoomSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            try
            {
                await previousTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // A newer authoritative launch generation supersedes the previous failure.
            }

            cancellationToken.ThrowIfCancellationRequested();
            await ResumePendingStageCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }

        private async Task ResumePendingStageCoreAsync(
            MultiplayerRoomSnapshot initialSnapshot,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield();
                if (initialSnapshot.Phase == MultiplayerRoomPhase.Loading)
                {
                    if (_assetLoader == null)
                    {
                        throw new InvalidOperationException(
                            "No multiplayer battle asset loader is registered.");
                    }

                    if (initialSnapshot.LaunchGeneration <= 0)
                    {
                        throw new InvalidOperationException("Loading snapshot has no launch generation.");
                    }

                    if (initialSnapshot.LoadingDeadlineUnixMs > 0 &&
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= initialSnapshot.LoadingDeadlineUnixMs)
                    {
                        throw new TimeoutException("The room loading deadline has elapsed.");
                    }

                    Transition(MultiplayerRoomFlowState.LoadingAssets);
                    ResetLocalLoadingProgress();
                    using var progressCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var progressRelay = new ClientLoadingProgressRelay();
                    var progressTask = progressRelay.UploadUntilCompletedAsync(
                        (value, ct) => _session.ReportLoadingProgressAsync(initialSnapshot.RoomId, value, ct),
                        cancellationToken: progressCancellation.Token);
                    try
                    {
                        await _assetLoader.LoadAsync(
                            initialSnapshot,
                            new ImmediateProgress<MultiplayerAssetLoadProgress>(value =>
                            {
                                UpdateLocalLoadingProgress(value);
                                progressRelay.Report(new ClientLoadingProgress(
                                    value.CurrentAssetKey,
                                    value.Progress,
                                    value.Progress / 100f));
                            }),
                            cancellationToken).ConfigureAwait(false);
                        CompleteLocalLoadingProgress();
                        progressRelay.Complete("complete");
                        await progressTask.ConfigureAwait(false);
                    }
                    catch
                    {
                        progressCancellation.Cancel();
                        try
                        {
                            await progressTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (progressCancellation.IsCancellationRequested)
                        {
                        }
                        throw;
                    }
                    cancellationToken.ThrowIfCancellationRequested();

                    var current = CurrentSnapshot;
                    if (current == null ||
                        current.Phase != MultiplayerRoomPhase.Loading ||
                        current.LaunchGeneration != initialSnapshot.LaunchGeneration)
                    {
                        return;
                    }

                    await _session.ReportAssetsLoadedAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                }

                var latest = CurrentSnapshot;
                if (latest == null ||
                    latest.LaunchGeneration != initialSnapshot.LaunchGeneration ||
                    (latest.Phase != MultiplayerRoomPhase.Loading &&
                     latest.Phase != MultiplayerRoomPhase.Starting))
                {
                    return;
                }

                Transition(MultiplayerRoomFlowState.WaitingForBattle);
                await _session.WaitForBattleStartAsync(CurrentRoomId, cancellationToken).ConfigureAwait(false);
                Transition(MultiplayerRoomFlowState.InBattle);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        private void StartPendingStage()
        {
            if (_assetLoader == null) return;
            _ = ResumePendingStageAsync();
        }

        private void CancelPendingStage(bool releaseAssets)
        {
            lock (_stageGate)
            {
                CancelPendingStageLocked(releaseAssets);
            }
        }

        private void CancelPendingStageLocked(bool releaseAssets)
        {
            try
            {
                _stageCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _stageCancellation?.Dispose();
            _stageCancellation = null;
            _stageGeneration = -1;
            ResetLocalLoadingProgress();
            if (releaseAssets) _assetLoader?.Release();
        }

        private void UpdateLocalLoadingProgress(MultiplayerAssetLoadProgress progress)
        {
            lock (_loadingProgressGate)
            {
                if (progress.Progress < _localLoadingProgress) return;
                _localLoadingProgress = progress.Progress;
                _currentLoadingAssetKey = progress.CurrentAssetKey;
            }
        }

        private void CompleteLocalLoadingProgress()
        {
            lock (_loadingProgressGate)
            {
                _localLoadingProgress = 100;
                _localLoadingCompleted = true;
            }
        }

        private void ResetLocalLoadingProgress()
        {
            lock (_loadingProgressGate)
            {
                _localLoadingProgress = 0;
                _currentLoadingAssetKey = string.Empty;
                _localLoadingCompleted = false;
            }
        }

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public ImmediateProgress(Action<T> report)
            {
                _report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public void Report(T value) => _report(value);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MultiplayerRoomFlowController));
        }

        private void ApplyJoinResult(
            string requestedRoomId,
            in MultiplayerRoomJoinResult result)
        {
            if (result.PlayerId == 0u)
            {
                throw new InvalidOperationException(
                    "Room join succeeded without an authoritative player id.");
            }

            if (!string.IsNullOrWhiteSpace(result.RoomId) &&
                !string.Equals(result.RoomId, requestedRoomId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Room join returned unexpected room id '{result.RoomId}' for '{requestedRoomId}'.");
            }

            CurrentRoomId = string.IsNullOrWhiteSpace(result.RoomId)
                ? requestedRoomId
                : result.RoomId;
            LocalPlayerId = result.PlayerId;
        }

        /// <summary>
        /// 从快照恢复：根据当前快照 Phase 推断控制器状态。
        /// </summary>
        public void RestoreFromSnapshot()
        {
            var snapshot = _snapshotProvider.Current;
            if (snapshot == null)
            {
                Cancel();
                return;
            }

            CurrentSnapshot = snapshot;
            CurrentRoomId = snapshot.RoomId;
            Transition(MapPhaseToState(snapshot.Phase));
        }

        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                throw;
            }
        }

        private void HandleSnapshotChanged(MultiplayerRoomSnapshot snapshot)
        {
            var previous = CurrentSnapshot;
            CurrentSnapshot = snapshot;
            if (!string.IsNullOrEmpty(snapshot.RoomId) && string.IsNullOrEmpty(CurrentRoomId))
            {
                CurrentRoomId = snapshot.RoomId;
            }

            var loadingTimedOut = previous != null &&
                                  (previous.Phase == MultiplayerRoomPhase.Loading ||
                                   previous.Phase == MultiplayerRoomPhase.Starting) &&
                                  snapshot.Phase == MultiplayerRoomPhase.Lobby &&
                                  string.Equals(
                                      snapshot.PhaseReason,
                                      "LoadingTimeout",
                                      StringComparison.Ordinal);
            if (loadingTimedOut)
            {
                LastError = "Room loading timed out before all players finished loading.";
            }
            else if (snapshot.Phase == MultiplayerRoomPhase.Loading)
            {
                LastError = string.Empty;
            }

            // 仅在活跃流程中根据服务端 Phase 同步状态，避免覆盖用户驱动的中间态（LoggingIn/CreatingRoom 等）。
            if (IsActiveFlowState(CurrentState))
            {
                var mapped = MapPhaseToState(snapshot.Phase);
                if (mapped != CurrentState)
                {
                    Transition(mapped);
                }
            }

            if (previous != null &&
                previous.LaunchGeneration != snapshot.LaunchGeneration)
            {
                CancelPendingStage(releaseAssets: true);
            }

            if (snapshot.Phase == MultiplayerRoomPhase.Loading ||
                snapshot.Phase == MultiplayerRoomPhase.Starting)
            {
                StartPendingStage();
            }
            else if (snapshot.Phase == MultiplayerRoomPhase.Lobby)
            {
                CancelPendingStage(releaseAssets: true);
            }
        }

        private void Transition(MultiplayerRoomFlowState next)
        {
            if (CurrentState == next) return;
            CurrentState = next;
            StateChanged?.Invoke(next);
        }

        private void Fail(string message)
        {
            LastError = message ?? string.Empty;
            Transition(MultiplayerRoomFlowState.Failed);
        }

        private void EnsureState(MultiplayerRoomFlowState expected)
        {
            if (CurrentState != expected)
            {
                throw new InvalidOperationException(
                    $"当前状态不支持该操作：期望 {expected}，实际 {CurrentState}。");
            }
        }

        private static bool IsActiveFlowState(MultiplayerRoomFlowState state)
        {
            return state == MultiplayerRoomFlowState.InLobby ||
                   state == MultiplayerRoomFlowState.LoadingAssets ||
                   state == MultiplayerRoomFlowState.WaitingForBattle ||
                   state == MultiplayerRoomFlowState.InBattle;
        }

        private static MultiplayerRoomFlowState MapPhaseToState(MultiplayerRoomPhase phase)
        {
            switch (phase)
            {
                case MultiplayerRoomPhase.Lobby:
                    return MultiplayerRoomFlowState.InLobby;
                case MultiplayerRoomPhase.Loading:
                    return MultiplayerRoomFlowState.LoadingAssets;
                case MultiplayerRoomPhase.Starting:
                    return MultiplayerRoomFlowState.WaitingForBattle;
                case MultiplayerRoomPhase.InBattle:
                    return MultiplayerRoomFlowState.InBattle;
                case MultiplayerRoomPhase.Closed:
                case MultiplayerRoomPhase.Expired:
                case MultiplayerRoomPhase.Closing:
                    return MultiplayerRoomFlowState.Failed;
                default:
                    return MultiplayerRoomFlowState.Idle;
            }
        }

        private static MultiplayerRoomFlowState MapRestoreNextStepToState(
            MultiplayerRoomRestoreNextStep nextStep)
        {
            switch (nextStep)
            {
                case MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading:
                    return MultiplayerRoomFlowState.InLobby;
                case MultiplayerRoomRestoreNextStep.ReportAssetsLoaded:
                    return MultiplayerRoomFlowState.LoadingAssets;
                case MultiplayerRoomRestoreNextStep.WaitForBattleStart:
                    return MultiplayerRoomFlowState.WaitingForBattle;
                case MultiplayerRoomRestoreNextStep.EnterBattle:
                    return MultiplayerRoomFlowState.InBattle;
                default:
                    return MultiplayerRoomFlowState.Failed;
            }
        }
    }
}
