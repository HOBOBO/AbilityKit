using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Battle.Presentation.Features.Settlement
{
    /// <summary>
    /// 战斗结算数据采集器。挂载在 <c>Battle.InMatch</c> 阶段。
    /// - OnAttach: 初始化玩家统计
    /// - OnDetach: 把统计结果写入 <see cref="BattleEndSummaryCache"/>，供后续阶段读取
    ///
    /// 因为 <c>Battle.End</c> 配置了 <c>clearBeforeEnter: true</c>，<c>IGameFeatureStore</c> 会被清空，
    /// 所以采集结果只能存在静态缓存里（<see cref="BattleEndSummaryCache"/>）。
    /// </summary>
    public sealed class BattleEndSummaryRecorder : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private int _startFrame;
        private int _lastFrame;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (!ctx.Features.TryGet(out _ctx)) _ctx = null;
            _startFrame = _ctx?.LastFrame ?? 0;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            BattleEndSummaryCache.Current = CaptureSummary();
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx != null) _lastFrame = _ctx.LastFrame;
        }

        private BattleEndSummary CaptureSummary()
        {
            var summary = new BattleEndSummary();
            summary.MatchDurationFrames = Math.Max(0, _lastFrame - _startFrame);
            summary.MatchDurationSeconds = summary.MatchDurationFrames / 30; // 假定 30fps tick rate（MVP 估值）

            if (_ctx == null || _ctx.Plan.LaunchSpec.Players == null)
            {
                return summary;
            }

            // 解析 world 服务
            MobaPlayerActorMapService playerMap = null;
            MobaActorLookupService actorLookup = null;
            if (_ctx.Session != null
                && _ctx.Session.TryGetWorld(out var world)
                && world?.Services != null)
            {
                world.Services.TryResolve(out playerMap);
                world.Services.TryResolve(out actorLookup);
            }

            var localPlayer = _ctx.ResolveLocalControlPlayerId();

            foreach (var loadout in _ctx.Plan.LaunchSpec.Players)
            {
                int actorId = 0;
                if (playerMap != null && !string.IsNullOrEmpty(loadout.PlayerId.Value))
                {
                    playerMap.TryGetActorId(loadout.PlayerId, out actorId);
                }

                var row = new BattleEndPlayerRow
                {
                    PlayerId = loadout.PlayerId.Value ?? string.Empty,
                    TeamId = loadout.TeamId,
                    HeroId = loadout.HeroId,
                    IsLocalPlayer = string.Equals(loadout.PlayerId.Value, localPlayer, StringComparison.OrdinalIgnoreCase),
                };

                if (actorLookup != null && actorId > 0
                    && actorLookup.TryGetActorEntity(actorId, out var entity) && entity != null)
                {
                    ReadEntityHp(entity, out var cur, out var max, out var alive);
                    row.FinalHp = (int)Math.Round(cur);
                    row.MaxHp = (int)Math.Round(max);
                    row.IsAlive = alive;
                }

                summary.Players.Add(row);
            }

            // 胜负判定（MVP 简化）：本地玩家存活即胜利。
            // 真实实现：由 Host / Server 推送 winningTeamId，覆盖此处判断。
            var localRow = summary.Players.Find(r => r.IsLocalPlayer);
            if (localRow != null)
            {
                summary.WinningTeamId = localRow.TeamId;
                summary.LocalPlayerVictory = localRow.IsAlive;
            }

            return summary;
        }

        private static void ReadEntityHp(ActorEntity entity, out float current, out float max, out bool alive)
        {
            current = 0f;
            max = 0f;
            alive = true;

            if (entity == null) { alive = false; return; }

            if (entity.hasResourceContainer && entity.resourceContainer.Value != null)
            {
                current = entity.GetMobaResource(ResourceType.Hp);
            }

            if (entity.hasAttributeGroup && entity.attributeGroup.Group != null)
            {
                max = entity.attributeGroup.Group.GetValue(MobaAttributeIds.MAX_HP);
            }

            if (current <= 0f && max > 0f) alive = false;
        }
    }
}