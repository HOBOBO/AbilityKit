#nullable enable

using System.Collections.Generic;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterSpatialTargetIndex
    {
        private const float CellSize = 6f;
        private const int MaxSearchRadius = 64;
        private const int DirectScanThreshold = 16;
        private const int FullScanFallbackThreshold = 256;

        private readonly ShooterSpatialHashGrid _grid = new(CellSize);
        private readonly Dictionary<int, int> _recordIndicesByPlayerId = new();
        private readonly List<ShooterSpatialTargetRecord> _records = new(16);
        private readonly List<int> _candidateBuffer = new(32);
        private int _lastRebuildFrame = -1;

        public int CellCount => _grid.CellCount;

        public int IndexedPlayerCount => _records.Count;

        public int LargestCellOccupancy => _grid.LargestCellOccupancy;

        public void Rebuild(ISveltoWorldContext context, int frame)
        {
            if (_lastRebuildFrame == frame)
            {
                return;
            }

            _lastRebuildFrame = frame;
            var playerCollection = context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var count);
            Rebuild(players, count);
        }

        public void Rebuild(NB<ShooterSveltoPlayerComponent> players, int count, int frame = -1)
        {
            // 帧去重：同一帧内多个系统（敌人移动/敌人攻击/BotAI）共享一份索引，
            // 首个调用方重建，后续调用直接复用（高单位量优化：省 O(n) 字典重建）。
            if (frame >= 0 && _lastRebuildFrame == frame)
            {
                return;
            }

            _lastRebuildFrame = frame;
            _grid.Clear();
            _recordIndicesByPlayerId.Clear();
            _records.Clear();

            for (var i = 0; i < count; i++)
            {
                if (!players[i].Alive || players[i].Hp <= 0)
                {
                    continue;
                }

                var player = players[i];
                var recordIndex = _records.Count;
                _records.Add(new ShooterSpatialTargetRecord(i, in player));
                _recordIndicesByPlayerId[player.PlayerId] = recordIndex;
                _grid.Add(player.X, player.Y, recordIndex);
            }
        }

        public bool TryGetLivePlayer(int playerId, out ShooterSveltoPlayerComponent player)
        {
            player = default;
            if (!TryGetLivePlayerRecord(playerId, out var record))
            {
                return false;
            }

            player = record.Player;
            return true;
        }

        public bool TryGetLivePlayerIndex(int playerId, out int playerIndex, out ShooterSveltoPlayerComponent player)
        {
            playerIndex = -1;
            player = default;
            if (!TryGetLivePlayerRecord(playerId, out var record))
            {
                return false;
            }

            playerIndex = record.PlayerIndex;
            player = record.Player;
            return true;
        }

        public bool TryGetLivePlayerByPosition(float selfX, float selfY, out ShooterSveltoPlayerComponent player)
        {
            player = default;
            return TryFindNearestTarget(selfX, selfY, selfPlayerId: 0, out var targetPlayerId, out _, out _, out _)
                && TryGetLivePlayer(targetPlayerId, out player);
        }

        public bool TryGetOnlyLivePlayer(out ShooterSveltoPlayerComponent player)
        {
            player = default;
            if (_records.Count != 1)
            {
                return false;
            }

            var record = _records[0];
            if (!record.Player.Alive || record.Player.Hp <= 0)
            {
                return false;
            }

            player = record.Player;
            return true;
        }

        public bool TryGetOnlyLivePlayer(out int playerIndex, out ShooterSveltoPlayerComponent player)
        {
            playerIndex = -1;
            player = default;
            if (_records.Count != 1)
            {
                return false;
            }

            var record = _records[0];
            if (!record.Player.Alive || record.Player.Hp <= 0)
            {
                return false;
            }

            playerIndex = record.PlayerIndex;
            player = record.Player;
            return true;
        }

        public bool TryFindNearestTarget(float selfX, float selfY, int selfPlayerId, out int targetPlayerId, out float targetX, out float targetY, out float targetDistanceSq)
        {
            if (!TryFindNearestRecord(selfX, selfY, selfPlayerId, out var targetRecord, out targetDistanceSq))
            {
                targetPlayerId = 0;
                targetX = 0f;
                targetY = 0f;
                return false;
            }

            targetPlayerId = targetRecord.PlayerId;
            targetX = targetRecord.X;
            targetY = targetRecord.Y;
            return true;
        }

        public bool TryFindNearestPlayer(float selfX, float selfY, int selfPlayerId, out int playerIndex, out ShooterSveltoPlayerComponent player, out float distanceSquared)
        {
            if (!TryFindNearestRecord(selfX, selfY, selfPlayerId, out var targetRecord, out distanceSquared))
            {
                playerIndex = -1;
                player = default;
                return false;
            }

            playerIndex = targetRecord.PlayerIndex;
            player = targetRecord.Player;
            return true;
        }

        private bool TryGetLivePlayerRecord(int playerId, out ShooterSpatialTargetRecord record)
        {
            record = default;
            if (!_recordIndicesByPlayerId.TryGetValue(playerId, out var recordIndex))
            {
                return false;
            }

            record = _records[recordIndex];
            return record.Player.Alive && record.Player.Hp > 0;
        }

        private bool TryFindNearestRecord(float selfX, float selfY, int selfPlayerId, out ShooterSpatialTargetRecord targetRecord, out float targetDistanceSq)
        {
            targetRecord = default;
            targetDistanceSq = float.MaxValue;

            if (_records.Count == 0)
            {
                return false;
            }

            // Shooter rooms normally have only a handful of players. For that
            // shape a short contiguous scan is substantially cheaper than a
            // dictionary-backed expanding-ring query for every enemy.
            if (_records.Count <= DirectScanThreshold)
            {
                return TryFindBestRecord(
                    selfX,
                    selfY,
                    selfPlayerId,
                    ref targetRecord,
                    ref targetDistanceSq);
            }

            var cellX = _grid.ComputeCellX(selfX);
            var cellY = _grid.ComputeCellY(selfY);
            var found = false;

            for (var radius = 0; radius <= MaxSearchRadius; radius++)
            {
                _candidateBuffer.Clear();
                _grid.CollectRingByCell(cellX, cellY, radius, _candidateBuffer);
                found |= TryFindBestCandidate(selfX, selfY, selfPlayerId, _candidateBuffer, ref targetRecord, ref targetDistanceSq);
                if (found && IsSearchRadiusComplete(radius, targetDistanceSq))
                {
                    return true;
                }
            }

            if (!found || _records.Count <= FullScanFallbackThreshold)
            {
                found |= TryFindBestRecord(selfX, selfY, selfPlayerId, ref targetRecord, ref targetDistanceSq);
            }

            return found;
        }

        private static bool IsSearchRadiusComplete(int radius, float bestDistanceSq)
        {
            if (radius <= 0 || bestDistanceSq == float.MaxValue)
            {
                return false;
            }

            var guaranteedDistance = (radius - 1) * CellSize;
            return guaranteedDistance * guaranteedDistance >= bestDistanceSq;
        }

        private bool TryFindBestCandidate(
            float selfX,
            float selfY,
            int selfPlayerId,
            List<int> candidateIndices,
            ref ShooterSpatialTargetRecord targetRecord,
            ref float bestDistanceSq)
        {
            var found = false;
            for (var i = 0; i < candidateIndices.Count; i++)
            {
                var candidateIndex = candidateIndices[i];
                var candidate = _records[candidateIndex];
                if (candidate.PlayerId == selfPlayerId)
                {
                    continue;
                }

                var dx = candidate.X - selfX;
                var dy = candidate.Y - selfY;
                var distanceSq = dx * dx + dy * dy;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                targetRecord = candidate;
                found = true;
            }

            return found;
        }

        private bool TryFindBestRecord(
            float selfX,
            float selfY,
            int selfPlayerId,
            ref ShooterSpatialTargetRecord targetRecord,
            ref float bestDistanceSq)
        {
            var found = false;
            for (var i = 0; i < _records.Count; i++)
            {
                var candidate = _records[i];
                if (candidate.PlayerId == selfPlayerId)
                {
                    continue;
                }

                var dx = candidate.X - selfX;
                var dy = candidate.Y - selfY;
                var distanceSq = dx * dx + dy * dy;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                targetRecord = candidate;
                found = true;
            }

            return found;
        }

        private readonly struct ShooterSpatialTargetRecord
        {
            public ShooterSpatialTargetRecord(int playerIndex, in ShooterSveltoPlayerComponent player)
            {
                PlayerIndex = playerIndex;
                Player = player;
            }

            public int PlayerIndex { get; }
            public ShooterSveltoPlayerComponent Player { get; }
            public int PlayerId => Player.PlayerId;
            public float X => Player.X;
            public float Y => Player.Y;
        }
    }
}
