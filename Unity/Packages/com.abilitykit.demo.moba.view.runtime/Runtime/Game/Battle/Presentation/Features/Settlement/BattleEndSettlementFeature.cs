using System;
using System.Collections.Generic;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    /// <summary>
    /// 战斗结算界面 Feature。挂载在 <c>Battle.End</c> 阶段：
    /// - 在 OnAttach 时读取权威玩法快照形成的本场对局投影
    /// - 在 OnGUI 渲染居中结算卡片：
    ///   * 胜负横幅（VICTORY / DEFEAT）
    ///   * 时长
    ///   * 按队伍分组的玩家名单
    ///   * 返回大厅按钮
    /// </summary>
    public sealed class BattleEndSettlementFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private IFlowCommandSink _flowSink;
        private BattleEndSummary _summary = BattleEndSummary.Empty;

        private Vector2 _scroll;
        private bool _show = true;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _flowSink = ctx.Entry.Get<IFlowCommandSink>();
            _summary = BattleEndSummaryProjector.Build(ctx.GameplayPresentation);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _summary = BattleEndSummary.Empty;
            _flowSink = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
            if (!_show) return;
            DrawSettlementCard();
        }

        // ===== Rendering =====

        private void DrawSettlementCard()
        {
            const float cardWidth = 720f;
            const float cardHeight = 540f;
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var rect = new Rect(cx - cardWidth * 0.5f, cy - cardHeight * 0.5f, cardWidth, cardHeight);

            // dim background
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(dim, Texture2D.whiteTexture);
            GUI.color = prevColor;

            GUILayout.BeginArea(rect, GUI.skin.box);

            DrawResultBanner();
            GUILayout.Space(8f);
            DrawMatchSummary();
            GUILayout.Space(8f);
            DrawPlayerTables();
            GUILayout.FlexibleSpace();
            DrawFooter();

            GUILayout.EndArea();
        }

        private void DrawResultBanner()
        {
            var localPlayer = _summary.Players.Find(r => r.IsLocalPlayer);
            bool victory = localPlayer != null && _summary.LocalPlayerVictory;
            var label = !_summary.HasAuthoritativeResult || localPlayer == null || _summary.WinningTeamId == 0
                ? "MATCH OVER"
                : (victory ? "VICTORY" : "DEFEAT");
            var color = !_summary.HasAuthoritativeResult || localPlayer == null || _summary.WinningTeamId == 0
                ? Color.gray
                : (victory ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.85f, 0.25f, 0.25f, 1f));

            var style = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var prev = GUI.color;
            GUI.color = color;
            GUILayout.Label(label, style, GUILayout.Height(48f));
            GUI.color = prev;
        }

        private void DrawMatchSummary()
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            var mins = _summary.MatchDurationSeconds / 60;
            var secs = _summary.MatchDurationSeconds % 60;
            GUILayout.Label($"Duration: {mins:00}:{secs:00}    |    Score: {_summary.Team1Score:0.##} : {_summary.Team2Score:0.##}    |    Players: {_summary.Players.Count}",
                style, GUILayout.Height(20f));
        }

        private void DrawPlayerTables()
        {
            // 按 team 分组
            var byTeam = new Dictionary<int, List<BattleEndPlayerRow>>();
            foreach (var row in _summary.Players)
            {
                if (!byTeam.TryGetValue(row.TeamId, out var list))
                {
                    list = new List<BattleEndPlayerRow>();
                    byTeam[row.TeamId] = list;
                }
                list.Add(row);
            }

            var sortedTeams = new List<int>(byTeam.Keys);
            sortedTeams.Sort();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(280f));
            foreach (var teamId in sortedTeams)
            {
                DrawTeamTable(teamId, byTeam[teamId]);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();
        }

        private void DrawTeamTable(int teamId, List<BattleEndPlayerRow> rows)
        {
            var header = $"Team {teamId}";
            if (_summary.WinningTeamId != 0 && teamId == _summary.WinningTeamId)
            {
                header += "  (Winning)";
            }

            var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            GUILayout.Label(header, headerStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Player", GUI.skin.box, GUILayout.Width(140f));
            GUILayout.Label("Hero", GUI.skin.box, GUILayout.Width(80f));
            GUILayout.EndHorizontal();

            foreach (var row in rows)
            {
                GUILayout.BeginHorizontal();
                var label = row.IsLocalPlayer ? $"* {row.PlayerId}" : row.PlayerId;
                GUILayout.Label(label, GUILayout.Width(140f));
                GUILayout.Label(row.HeroId.ToString(), GUILayout.Width(80f));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Return to Lobby", GUILayout.Width(220f), GUILayout.Height(40f)))
            {
                _flowSink?.RequestReturnLobby();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
