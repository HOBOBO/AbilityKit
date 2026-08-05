using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 客户端 Room 快照应用结果。
    /// </summary>
    public enum ClientRoomSnapshotApplyResult
    {
        /// <summary>首次应用或更新到更新 revision。事件已触发。</summary>
        Applied,
        /// <summary>相同 revision 的重复 push，幂等忽略，不触发事件。</summary>
        DuplicateIgnored,
        /// <summary>旧 revision 的乱序 push，忽略，不触发事件。</summary>
        StaleIgnored
    }

    /// <summary>
    /// A structured membership delta derived from two authoritative snapshots.
    /// </summary>
    public sealed class ClientRoomMembershipChange
    {
        public ClientRoomMembershipChange(
            string roomId,
            long previousRevision,
            long currentRevision,
            IReadOnlyList<string> joinedAccountIds,
            IReadOnlyList<string> leftAccountIds,
            string previousOwnerAccountId,
            string currentOwnerAccountId)
        {
            RoomId = roomId ?? string.Empty;
            PreviousRevision = previousRevision;
            CurrentRevision = currentRevision;
            JoinedAccountIds = joinedAccountIds ?? Array.Empty<string>();
            LeftAccountIds = leftAccountIds ?? Array.Empty<string>();
            PreviousOwnerAccountId = previousOwnerAccountId ?? string.Empty;
            CurrentOwnerAccountId = currentOwnerAccountId ?? string.Empty;
        }

        public string RoomId { get; }
        public long PreviousRevision { get; }
        public long CurrentRevision { get; }
        public IReadOnlyList<string> JoinedAccountIds { get; }
        public IReadOnlyList<string> LeftAccountIds { get; }
        public string PreviousOwnerAccountId { get; }
        public string CurrentOwnerAccountId { get; }
        public bool OwnerChanged => !string.Equals(
            PreviousOwnerAccountId,
            CurrentOwnerAccountId,
            StringComparison.Ordinal);

        internal bool HasChanges =>
            JoinedAccountIds.Count > 0 ||
            LeftAccountIds.Count > 0 ||
            OwnerChanged;
    }

    public sealed class ClientRoomPlayerStateChange
    {
        public ClientRoomPlayerStateChange(
            string accountId,
            bool previousOnline,
            bool currentOnline,
            bool previousReady,
            bool currentReady,
            int previousHeroId,
            int currentHeroId)
        {
            AccountId = accountId ?? string.Empty;
            PreviousOnline = previousOnline;
            CurrentOnline = currentOnline;
            PreviousReady = previousReady;
            CurrentReady = currentReady;
            PreviousHeroId = previousHeroId;
            CurrentHeroId = currentHeroId;
        }

        public string AccountId { get; }
        public bool PreviousOnline { get; }
        public bool CurrentOnline { get; }
        public bool PreviousReady { get; }
        public bool CurrentReady { get; }
        public int PreviousHeroId { get; }
        public int CurrentHeroId { get; }
        public bool OnlineChanged => PreviousOnline != CurrentOnline;
        public bool ReadyChanged => PreviousReady != CurrentReady;
        public bool LoadoutChanged => PreviousHeroId != CurrentHeroId;
    }

    public sealed class ClientRoomPlayerStateChanges
    {
        public ClientRoomPlayerStateChanges(
            string roomId,
            long previousRevision,
            long currentRevision,
            IReadOnlyList<ClientRoomPlayerStateChange> changes)
        {
            RoomId = roomId ?? string.Empty;
            PreviousRevision = previousRevision;
            CurrentRevision = currentRevision;
            Changes = changes ?? Array.Empty<ClientRoomPlayerStateChange>();
        }

        public string RoomId { get; }
        public long PreviousRevision { get; }
        public long CurrentRevision { get; }
        public IReadOnlyList<ClientRoomPlayerStateChange> Changes { get; }
    }

    /// <summary>
    /// 单一权威客户端 Room 状态仓库。
    /// <para>
    /// - 按 <see cref="ClientRoomSnapshot.RoomRevision"/> 单调递增应用（拒绝旧 revision）。
    /// - 通过 <see cref="ClientRoomSnapshot.LastEventSequence"/> 检测事件缺口，标记 <see cref="IsStale"/>。
    /// - 线程安全（lock）。
    /// </para>
    /// </summary>
    public sealed class ClientRoomStore
    {
        private readonly object _gate = new object();
        private ClientRoomSnapshot _current;
        private bool _stale;

        /// <summary>
        /// 快照变更事件（仅在真正应用新 revision 时触发；重复/旧 revision 不触发）。
        /// </summary>
        public event Action<ClientRoomSnapshot> OnSnapshotChanged;

        public event Action<ClientRoomMembershipChange> OnMembershipChanged;

        public event Action<ClientRoomPlayerStateChanges> OnPlayerStateChanged;

        /// <summary>
        /// 当前最新快照（或 null）。
        /// </summary>
        public ClientRoomSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        /// <summary>
        /// 是否检测到事件缺口（收到的 push EventSequence > 本地 + 1），提示需要补拉。
        /// </summary>
        public bool IsStale
        {
            get
            {
                lock (_gate)
                {
                    return _stale;
                }
            }
        }

        /// <summary>
        /// 应用一个快照。
        /// <para>
        /// - 旧 revision（小于 current）忽略。
        /// - 相同 revision 的重复 push 幂等忽略（不触发事件）。
        /// - 新 revision 接受，并检测 EventSequence 缺口。
        /// </para>
        /// </summary>
        public ClientRoomSnapshotApplyResult ApplySnapshot(ClientRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ClientRoomSnapshot toPublish = null;
            ClientRoomMembershipChange membershipChange = null;
            ClientRoomPlayerStateChanges playerStateChanges = null;

            lock (_gate)
            {
                if (_current == null)
                {
                    // 首次应用：检测 EventSequence 缺口（>0 起点视为可能缺口）。
                    // A complete first snapshot establishes the local baseline regardless of
                    // how many authoritative room events occurred before this client bound.
                    _stale = false;
                    _current = snapshot;
                    toPublish = snapshot;
                }
                else
                {
                    if (snapshot.RoomRevision < _current.RoomRevision)
                    {
                        // 旧 revision：忽略。
                        return ClientRoomSnapshotApplyResult.StaleIgnored;
                    }

                    var sameRoom = string.Equals(
                        snapshot.RoomId,
                        _current.RoomId,
                        StringComparison.Ordinal);
                    if (sameRoom && snapshot.NumericRoomId == 0UL)
                    {
                        snapshot.NumericRoomId = _current.NumericRoomId;
                    }

                    if (snapshot.RoomRevision == _current.RoomRevision)
                    {
                        if (sameRoom &&
                            _current.NumericRoomId == 0UL &&
                            snapshot.NumericRoomId != 0UL)
                        {
                            _current = snapshot;
                            toPublish = snapshot;
                        }
                        else
                        {
                            // 相同 revision 且没有新增权威元数据：幂等忽略。
                            return ClientRoomSnapshotApplyResult.DuplicateIgnored;
                        }
                    }
                    else
                    {
                        // 新 revision：检测 EventSequence 缺口。
                        var previous = _current;
                        var expectedNext = _current.LastEventSequence + 1L;
                        _stale = snapshot.LastEventSequence > expectedNext;
                        _current = snapshot;
                        toPublish = snapshot;
                        if (sameRoom && HasPotentialMembershipChange(previous, snapshot))
                        {
                            membershipChange = BuildMembershipChange(previous, snapshot);
                        }
                        if (sameRoom)
                        {
                            playerStateChanges = BuildPlayerStateChanges(previous, snapshot);
                        }
                    }
                }
            }

            // 在锁外触发事件，避免回调内再次进入 store 造成死锁。
            OnSnapshotChanged?.Invoke(toPublish);
            if (membershipChange?.HasChanges == true)
            {
                OnMembershipChanged?.Invoke(membershipChange);
            }
            if (playerStateChanges?.Changes.Count > 0)
            {
                OnPlayerStateChanged?.Invoke(playerStateChanges);
            }
            return ClientRoomSnapshotApplyResult.Applied;
        }

        private static ClientRoomPlayerStateChanges BuildPlayerStateChanges(
            ClientRoomSnapshot previous,
            ClientRoomSnapshot current)
        {
            var previousPlayers = previous.Players ?? Array.Empty<ClientRoomPlayer>();
            var currentPlayers = current.Players ?? Array.Empty<ClientRoomPlayer>();
            if (previousPlayers.Count == 0 || currentPlayers.Count == 0) return null;

            var previousByAccount = new Dictionary<string, ClientRoomPlayer>(StringComparer.Ordinal);
            for (var i = 0; i < previousPlayers.Count; i++)
            {
                var player = previousPlayers[i];
                if (!string.IsNullOrWhiteSpace(player.AccountId))
                {
                    previousByAccount[player.AccountId] = player;
                }
            }

            var changes = new List<ClientRoomPlayerStateChange>();
            for (var i = 0; i < currentPlayers.Count; i++)
            {
                var currentPlayer = currentPlayers[i];
                if (string.IsNullOrWhiteSpace(currentPlayer.AccountId) ||
                    !previousByAccount.TryGetValue(currentPlayer.AccountId, out var previousPlayer))
                {
                    continue;
                }

                var previousReady = IsPrepared(previousPlayer);
                var currentReady = IsPrepared(currentPlayer);
                if (previousPlayer.IsOnline == currentPlayer.IsOnline &&
                    previousReady == currentReady &&
                    previousPlayer.HeroId == currentPlayer.HeroId)
                {
                    continue;
                }

                changes.Add(new ClientRoomPlayerStateChange(
                    currentPlayer.AccountId,
                    previousPlayer.IsOnline,
                    currentPlayer.IsOnline,
                    previousReady,
                    currentReady,
                    previousPlayer.HeroId,
                    currentPlayer.HeroId));
            }

            return changes.Count == 0
                ? null
                : new ClientRoomPlayerStateChanges(
                    current.RoomId,
                    previous.RoomRevision,
                    current.RoomRevision,
                    changes);
        }

        private static bool IsPrepared(ClientRoomPlayer player)
        {
            return player != null && player.IsOnline && player.LobbyReady && player.HeroId > 0;
        }

        private static ClientRoomMembershipChange BuildMembershipChange(
            ClientRoomSnapshot previous,
            ClientRoomSnapshot current)
        {
            return new ClientRoomMembershipChange(
                current.RoomId,
                previous.RoomRevision,
                current.RoomRevision,
                BuildDifference(current.Members, previous.Members),
                BuildDifference(previous.Members, current.Members),
                previous.OwnerAccountId,
                current.OwnerAccountId);
        }

        private static bool HasPotentialMembershipChange(
            ClientRoomSnapshot previous,
            ClientRoomSnapshot current)
        {
            if (!string.Equals(
                    previous.OwnerAccountId,
                    current.OwnerAccountId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            var previousMembers = previous.Members ?? Array.Empty<string>();
            var currentMembers = current.Members ?? Array.Empty<string>();
            if (previousMembers.Count != currentMembers.Count) return true;
            for (var i = 0; i < previousMembers.Count; i++)
            {
                if (!string.Equals(previousMembers[i], currentMembers[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> BuildDifference(
            IReadOnlyList<string> source,
            IReadOnlyList<string> excluded)
        {
            source ??= Array.Empty<string>();
            excluded ??= Array.Empty<string>();

            var excludedSet = new HashSet<string>(excluded, StringComparer.Ordinal);
            var added = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var accountId = source[i];
                if (!excludedSet.Contains(accountId) && added.Add(accountId))
                {
                    result.Add(accountId);
                }
            }

            return result.Count == 0 ? Array.Empty<string>() : result;
        }

        /// <summary>
        /// 补拉成功后清除 stale 标记。
        /// </summary>
        public void MarkRefreshed()
        {
            lock (_gate)
            {
                _stale = false;
            }
        }

        /// <summary>
        /// 重置仓库（清空当前快照与 stale 标记）。
        /// </summary>
        public void Reset()
        {
            lock (_gate)
            {
                _current = null;
                _stale = false;
            }
        }
    }
}
