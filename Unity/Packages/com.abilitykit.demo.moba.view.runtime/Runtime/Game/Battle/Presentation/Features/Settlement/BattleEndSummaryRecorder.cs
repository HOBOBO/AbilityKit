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
        private readonly BattleEndSettlementCollector _collector = new BattleEndSettlementCollector();
        private BattleContext _ctx;
        private MobaPlayerActorMapService _playerMap;
        private MobaActorLookupService _actorLookup;
        private string _localPlayerId;
        private int _startFrame;
        private int _lastFrame;

        public void OnAttach(in GamePhaseContext ctx)
        {
            if (!ctx.Features.TryGet(out _ctx)) _ctx = null;
            _startFrame = _ctx?.LastFrame ?? 0;
            _lastFrame = _startFrame;
            _localPlayerId = _ctx?.ResolveLocalControlPlayerId() ?? string.Empty;
            ResolveRuntimeServices(_ctx, out _playerMap, out _actorLookup);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            BattleEndSummaryCache.Current = CaptureSummary();
            _collector.Reset();
            _playerMap = null;
            _actorLookup = null;
            _localPlayerId = string.Empty;
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx != null) _lastFrame = _ctx.LastFrame;
        }

        private BattleEndSummary CaptureSummary()
        {
            _collector.Reset();
            var players = _ctx?.Plan.LaunchSpec.Players;
            if (players != null)
            {
                foreach (var loadout in players)
                {
                    var playerId = loadout.PlayerId.Value ?? string.Empty;
                    var finalHp = 0;
                    var maxHp = 0;
                    var isAlive = true;
                    var actorId = 0;

                    if (_playerMap != null && !string.IsNullOrEmpty(playerId))
                    {
                        _playerMap.TryGetActorId(loadout.PlayerId, out actorId);
                    }

                    if (_actorLookup != null && actorId > 0 &&
                        _actorLookup.TryGetActorEntity(actorId, out var entity) && entity != null)
                    {
                        ReadEntityHp(entity, out var current, out var maximum, out isAlive);
                        finalHp = (int)Math.Round(current);
                        maxHp = (int)Math.Round(maximum);
                    }

                    var input = new BattleEndPlayerProjectionInput(
                        playerId,
                        loadout.TeamId,
                        loadout.HeroId,
                        string.Equals(playerId, _localPlayerId, StringComparison.OrdinalIgnoreCase),
                        finalHp,
                        maxHp,
                        isAlive);
                    _collector.AddPlayer(in input);
                }
            }

            return ToSummary(_collector.Build(_startFrame, _lastFrame));
        }

        private static BattleEndSummary ToSummary(BattleEndSettlementProjection projection)
        {
            var summary = new BattleEndSummary
            {
                MatchDurationFrames = projection.MatchDurationFrames,
                MatchDurationSeconds = projection.MatchDurationSeconds,
                WinningTeamId = projection.WinningTeamId,
                LocalPlayerVictory = projection.LocalPlayerVictory,
            };

            for (var i = 0; i < projection.Players.Count; i++)
            {
                var player = projection.Players[i];
                summary.Players.Add(new BattleEndPlayerRow
                {
                    PlayerId = player.PlayerId,
                    TeamId = player.TeamId,
                    HeroId = player.HeroId,
                    IsLocalPlayer = player.IsLocalPlayer,
                    FinalHp = player.FinalHp,
                    MaxHp = player.MaxHp,
                    IsAlive = player.IsAlive,
                });
            }

            return summary;
        }

        private static void ResolveRuntimeServices(
            BattleContext context,
            out MobaPlayerActorMapService playerMap,
            out MobaActorLookupService actorLookup)
        {
            playerMap = null;
            actorLookup = null;
            if (context?.Session == null ||
                !context.Session.TryGetWorld(out var world) ||
                world?.Services == null)
            {
                return;
            }

            world.Services.TryResolve(out playerMap);
            world.Services.TryResolve(out actorLookup);
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