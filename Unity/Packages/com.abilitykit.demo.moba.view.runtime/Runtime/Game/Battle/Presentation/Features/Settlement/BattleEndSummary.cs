using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    /// <summary>
    /// 玩家身份来自启动信息；战斗结果由权威玩法快照提供。
    /// </summary>
    public sealed class BattleEndPlayerRow
    {
        public string PlayerId { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public int HeroId { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int FinalHp { get; set; }
        public int MaxHp { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsAlive { get; set; } = true;

        public float Kda => Deaths <= 0 ? Kills + Assists : (Kills + Assists) / (float)Deaths;
    }

    /// <summary>
    /// 结算快照。包含所有玩家 + 胜负结果 + 时长。
    /// </summary>
    public sealed class BattleEndSummary
    {
        public string MatchId { get; set; } = string.Empty;
        public int GameplayId { get; set; }
        public bool HasAuthoritativeResult { get; set; }
        public int WinningTeamId { get; set; }       // 0 = 平局
        public double Team1Score { get; set; }
        public double Team2Score { get; set; }
        public string EndReason { get; set; } = string.Empty;
        public bool LocalPlayerVictory { get; set; }
        public int MatchDurationFrames { get; set; }
        public int MatchDurationSeconds { get; set; }
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

        public List<BattleEndPlayerRow> Players { get; } = new List<BattleEndPlayerRow>(10);

        public static BattleEndSummary Empty { get; } = new BattleEndSummary();
    }

}
