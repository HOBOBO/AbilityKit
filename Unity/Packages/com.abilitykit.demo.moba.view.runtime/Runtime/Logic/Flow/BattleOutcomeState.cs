using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleOutcomeState
    {
        private int _gameplayId;
        private string _matchId = string.Empty;
        private int _lastFrame = -1;

        public bool HasAuthoritativeResult { get; private set; }
        public GameplayMatchData Result { get; private set; }

        /// <summary>
        /// 当前比分的最新观测值，供胜负规则兜底评估使用。
        /// 逻辑层快照每帧到达，这里只保留数值，不做判定。
        /// </summary>
        public double Team1Score { get; private set; }
        public double Team2Score { get; private set; }
        public int LatestFrame { get; private set; } = -1;

        /// <summary>本局注册的胜负规则与副本定义，用于在权威结果缺失时兜底评估。</summary>
        private readonly List<IBattleOutcomeRule> _outcomeRules = new List<IBattleOutcomeRule>();
        private MobaDungeonDef _dungeon;

        /// <summary>
        /// 绑定本局副本与胜负规则。由 <c>dungeon_context</c> feature 在 Attach 时调用；
        /// 未绑定时行为与之前一致，只认权威结果。
        /// </summary>
        public void BindRules(MobaDungeonDef dungeon, IEnumerable<IBattleOutcomeRule> rules)
        {
            _dungeon = dungeon;
            _outcomeRules.Clear();
            if (rules != null)
                foreach (var rule in rules)
                    if (rule != null) _outcomeRules.Add(rule);
        }

        public void SetIdentity(int gameplayId, string matchId)
        {
            if (gameplayId <= 0) return;
            if (_gameplayId != 0 && _gameplayId != gameplayId)
                throw new InvalidOperationException("Battle outcome gameplay identity changed.");
            if (!string.IsNullOrEmpty(_matchId) && !string.IsNullOrEmpty(matchId) &&
                !string.Equals(_matchId, matchId, StringComparison.Ordinal))
                throw new InvalidOperationException("Battle outcome match identity changed.");
            _gameplayId = gameplayId;
            if (!string.IsNullOrEmpty(matchId)) _matchId = matchId;
        }

        public bool Apply(in GameplayMatchData match)
        {
            if (match.GameplayId <= 0 || match.SnapshotFrame <= _lastFrame ||
                (_gameplayId != 0 && _gameplayId != match.GameplayId) ||
                (!string.IsNullOrEmpty(_matchId) &&
                 !string.Equals(_matchId, match.MatchId, StringComparison.Ordinal))) return false;
            SetIdentity(match.GameplayId, match.MatchId);
            _lastFrame = match.SnapshotFrame;
            Team1Score = match.Team1Score;
            Team2Score = match.Team2Score;
            LatestFrame = match.SnapshotFrame;
            if (match.Phase != GameplayClockPhase.Ended) return false;
            Result = match;
            HasAuthoritativeResult = true;
            return true;
        }

        /// <summary>
        /// 用已注册的胜负规则兜底判定胜负。仅在没有权威结果时生效，
        /// 因此逻辑层 trigger 的结论始终优先。返回 true 表示规则给出了胜方。
        /// </summary>
        public bool TryEvaluateRule(out int winningTeamId)
        {
            winningTeamId = 0;
            if (HasAuthoritativeResult || _dungeon == null || _outcomeRules.Count == 0)
                return false;
            var context = new BattleOutcomeEvaluationContext(
                _dungeon, _gameplayId, LatestFrame, 0, 0, Team1Score, Team2Score);
            foreach (var rule in _outcomeRules)
            {
                if (rule == null) continue;
                if (rule.GameplayId != 0 && rule.GameplayId != _gameplayId) continue;
                if (rule.TryEvaluate(context, out winningTeamId)) return true;
                winningTeamId = 0;
            }
            return false;
        }

        public void Reset()
        {
            _gameplayId = 0;
            _matchId = string.Empty;
            _lastFrame = -1;
            Team1Score = 0;
            Team2Score = 0;
            LatestFrame = -1;
            _dungeon = null;
            _outcomeRules.Clear();
            HasAuthoritativeResult = false;
            Result = default;
        }
    }
}
