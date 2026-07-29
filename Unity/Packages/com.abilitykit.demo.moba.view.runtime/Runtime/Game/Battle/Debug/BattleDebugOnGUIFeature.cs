using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleDebugOnGUIFeature : IGamePhaseFeature, IOnGUIFeature
    {
        private BattleContext _ctx;
        private BattleHudFeature _hud;
        private BattleViewFeature _view;
        private ConfirmedBattleViewFeature _confirmedView;

        public void OnAttach(in GamePhaseContext ctx)
        {
            RefreshFeatures(in ctx);
            BattleFlowDebugProvider.Current = _ctx;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ReferenceEquals(BattleFlowDebugProvider.Current, _ctx))
            {
                BattleFlowDebugProvider.Current = null;
            }
            if (ReferenceEquals(BattleFlowDebugProvider.CurrentHud, _hud))
            {
                BattleFlowDebugProvider.CurrentHud = null;
            }
            if (ReferenceEquals(BattleFlowDebugProvider.CurrentView, _view))
            {
                BattleFlowDebugProvider.CurrentView = null;
            }
            if (ReferenceEquals(BattleFlowDebugProvider.CurrentConfirmedView, _confirmedView))
            {
                BattleFlowDebugProvider.CurrentConfirmedView = null;
            }
            _confirmedView = null;
            _view = null;
            _hud = null;
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }

        public void OnGUI(in GamePhaseContext ctx)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RefreshFeatures(in ctx);
            if (!ctx.Entry.DebugEnabled) return;

            var sink = ctx.Entry.Get<IFlowCommandSink>();
            if (sink == null || sink.CurrentRootPhase != MobaRootState.Battle) return;

            GUILayout.BeginArea(new Rect(10, 10, 170, 110), GUI.skin.window);
            if (GUILayout.Button("Exit Battle", GUILayout.Height(34)))
            {
                sink.RequestReturnLobby();
            }

            if (GUILayout.Button("Rebind Views", GUILayout.Height(34)))
            {
                if (ctx.Features.TryGet(out BattleViewFeature view) && view != null)
                {
                    view.RebindAll();
                }
                if (ctx.Features.TryGet(out ConfirmedBattleViewFeature confirmed) && confirmed != null)
                {
                    confirmed.RebindAll();
                }
            }
            GUILayout.EndArea();
#endif
        }

        private void RefreshFeatures(in GamePhaseContext ctx)
        {
            if (ctx.Features.TryGet(out BattleContext current) && current != null && !ReferenceEquals(current, _ctx))
            {
                _ctx = current;
                BattleFlowDebugProvider.Current = _ctx;
            }

            if (ctx.Features.TryGet(out BattleHudFeature hud) && hud != null && !ReferenceEquals(hud, _hud))
            {
                _hud = hud;
                BattleFlowDebugProvider.CurrentHud = _hud;
            }

            if (ctx.Features.TryGet(out BattleViewFeature view) && view != null && !ReferenceEquals(view, _view))
            {
                _view = view;
                BattleFlowDebugProvider.CurrentView = _view;
            }

            if (ctx.Features.TryGet(out ConfirmedBattleViewFeature confirmed) && confirmed != null && !ReferenceEquals(confirmed, _confirmedView))
            {
                _confirmedView = confirmed;
                BattleFlowDebugProvider.CurrentConfirmedView = _confirmedView;
            }
        }

    }

    internal readonly struct BattleDebugHeroOption
    {
        public readonly int HeroId;
        public readonly string DisplayName;

        public BattleDebugHeroOption(int heroId, string displayName)
        {
            HeroId = heroId;
            DisplayName = displayName ?? string.Empty;
        }
    }

    internal sealed class BattleLocalDebugController
    {
        private static readonly BattleDebugHeroOption[] EmptyHeroOptions = Array.Empty<BattleDebugHeroOption>();

        private readonly Func<BattleContext> _ctxResolver;
        private readonly Func<BattleHudFeature> _hudResolver;
        private BattleDebugHeroOption[] _heroOptions = EmptyHeroOptions;
        private MobaConfigDatabase _heroOptionsDatabase;
        private long _heroOptionsVersion = -1;

        public BattleLocalDebugController(Func<BattleContext> ctxResolver, Func<BattleHudFeature> hudResolver)
        {
            _ctxResolver = ctxResolver;
            _hudResolver = hudResolver;
        }

        private BattleContext Context => _ctxResolver?.Invoke();

        public bool IsAvailable
        {
            get
            {
                var ctx = Context;
                return ctx != null && ctx.Session != null && ctx.Plan.HostMode == BattleStartConfig.BattleHostMode.Local;
            }
        }

        public bool HasSession
        {
            get
            {
                var ctx = Context;
                return ctx != null && ctx.Session != null;
            }
        }

        public string HostModeName
        {
            get
            {
                var ctx = Context;
                return ctx != null ? ctx.Plan.HostMode.ToString() : "战斗上下文缺失";
            }
        }

        public string UnavailableReason
        {
            get
            {
                var ctx = Context;
                if (ctx == null) return "战斗上下文缺失";
                if (ctx.Session == null) return "战斗会话缺失";
                if (ctx.Plan.HostMode != BattleStartConfig.BattleHostMode.Local) return $"非本地模式（{ctx.Plan.HostMode}）";
                return "就绪";
            }
        }

        public string CurrentPlayerId
        {
            get
            {
                var ctx = Context;
                return ctx != null ? ctx.ResolveLocalControlPlayerId() : string.Empty;
            }
        }

        public int CurrentActorId
        {
            get
            {
                var ctx = Context;
                return ctx != null ? ctx.LocalActorId : 0;
            }
        }

        public bool IsEnemyAiEnabled
        {
            get
            {
                var ctx = Context;
                if (!TryResolveEnemyActorServices(
                        ctx,
                        out var loadouts,
                        out var localTeamId,
                        out var playerActors,
                        out var actors))
                {
                    return false;
                }

                for (var i = 0; i < loadouts.Length; i++)
                {
                    var loadout = loadouts[i];
                    if (loadout.TeamId == localTeamId || loadout.BrainId <= 0) continue;
                    if (!playerActors.TryGetActorId(loadout.PlayerId, out var actorId)) continue;
                    if (!actors.TryGetActorEntity(actorId, out var actor) || actor == null) continue;
                    if (actor.hasActorBrain && actor.actorBrain.SourceKind == MobaBrainSourceKinds.BattleTemplate)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public BattleDebugHeroOption[] HeroOptions
        {
            get
            {
                RefreshHeroOptions();
                return _heroOptions;
            }
        }

        public int CurrentHeroId
        {
            get
            {
                var ctx = Context;
                if (ctx == null) return 0;

                var playerId = CurrentPlayerId;
                var loadouts = ctx.BuildEffectivePlayerLoadouts();
                if (loadouts == null) return 0;

                foreach (var loadout in loadouts)
                {
                    if (string.Equals(loadout.PlayerId.Value, playerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadout.HeroId;
                    }
                }

                return 0;
            }
        }

        private void RefreshHeroOptions()
        {
            var ctx = Context;
            if (!TryResolveWorldService(ctx, out MobaConfigDatabase config) || config == null)
            {
                _heroOptions = EmptyHeroOptions;
                _heroOptionsDatabase = null;
                _heroOptionsVersion = -1;
                return;
            }

            if (ReferenceEquals(config, _heroOptionsDatabase) && config.Version == _heroOptionsVersion) return;

            var options = new System.Collections.Generic.List<BattleDebugHeroOption>();
            foreach (var character in config.GetAllCharacters())
            {
                if (character == null ||
                    !MobaHeroLoadoutResolver.TryResolveHeroConfig(
                        config,
                        character.Id,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    continue;
                }

                var displayName = string.IsNullOrEmpty(character.Name)
                    ? character.Id.ToString()
                    : $"{character.Id} {character.Name}";
                options.Add(new BattleDebugHeroOption(character.Id, displayName));
            }

            options.Sort((left, right) => left.HeroId.CompareTo(right.HeroId));
            _heroOptions = options.ToArray();
            _heroOptionsDatabase = config;
            _heroOptionsVersion = config.Version;
        }

        public bool TrySwitchControl(out string message)
        {
            message = string.Empty;
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            var players = ctx.Plan.LaunchSpec.Players;
            if (players == null || players.Length <= 1)
            {
                message = "至少需要两名玩家";
                return false;
            }

            var current = CurrentPlayerId;
            var currentIndex = 0;
            for (var i = 0; i < players.Length; i++)
            {
                if (string.Equals(players[i].PlayerId.Value, current, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            for (var step = 1; step <= players.Length; step++)
            {
                var next = players[(currentIndex + step) % players.Length];
                if (TrySetControlPlayer(next.PlayerId, out message))
                {
                    return true;
                }
            }

            if (string.IsNullOrEmpty(message)) message = "未找到可控制的玩家";
            return false;
        }

        public bool TrySetControlPlayer(PlayerId playerId, out string message)
        {
            message = string.Empty;
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (string.IsNullOrEmpty(playerId.Value))
            {
                message = "玩家 ID 为空";
                return false;
            }

            if (!TryResolveWorldService(ctx, out MobaPlayerActorMapService playerActors) || playerActors == null)
            {
                message = "玩家与角色映射服务缺失";
                return false;
            }

            if (!playerActors.TryGetActorId(playerId, out var actorId) || actorId <= 0)
            {
                message = $"未找到玩家 {playerId.Value} 的角色";
                return false;
            }

            ctx.LocalControlPlayerId = playerId.Value;
            ctx.LocalActorId = actorId;
            _hudResolver?.Invoke()?.RefreshLocalControlSkillTemplates();
            message = $"已切换至玩家 {playerId.Value}，角色 ID={actorId}";
            return true;
        }

        public bool TryResetCooldowns(out string message)
        {
            message = string.Empty;
            var ctx = Context;
            if (!TryResolveCurrentActor(out var actor, out message)) return false;
            if (!TryResolveWorldService(ctx, out MobaActorLookupService actors) || actors == null)
            {
                message = "角色查询服务缺失";
                return false;
            }

            if (!actor.hasSkillLoadout || actor.skillLoadout.ActiveSkills == null)
            {
                message = "角色主动技能缺失";
                return false;
            }

            var count = 0;
            var skills = actor.skillLoadout.ActiveSkills;
            for (var i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                if (MobaSkillRuntimeAccess.TrySetActiveSkillCooldown(actors, ctx.LocalActorId, i + 1, skill.SkillId, 0L, 0))
                {
                    count++;
                }
            }

            message = $"已重置 {count} 个技能的冷却时间";
            return count > 0;
        }

        public bool TryToggleEnemyAi(out string message)
        {
            message = string.Empty;
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (!TryResolveWorldService(ctx, out MobaBrainService brains) || brains == null)
            {
                message = "AI 服务缺失";
                return false;
            }

            if (!TryResolveEnemyActorServices(
                    ctx,
                    out var loadouts,
                    out var localTeamId,
                    out var playerActors,
                    out var actors))
            {
                message = "无法解析敌方角色";
                return false;
            }

            var disable = IsEnemyAiEnabled;
            var configuredCount = 0;
            var changedCount = 0;
            for (var i = 0; i < loadouts.Length; i++)
            {
                var loadout = loadouts[i];
                if (loadout.TeamId == localTeamId || loadout.BrainId <= 0) continue;
                configuredCount++;

                if (!playerActors.TryGetActorId(loadout.PlayerId, out var actorId) ||
                    !actors.TryGetActorEntity(actorId, out var actor) ||
                    actor == null)
                {
                    continue;
                }

                if (disable)
                {
                    if (actor.hasActorBrain &&
                        actor.actorBrain.SourceKind == MobaBrainSourceKinds.BattleTemplate &&
                        brains.DeactivateBrain(actor))
                    {
                        changedCount++;
                    }
                }
                else if (brains.ActivateBrain(
                             actor,
                             loadout.BrainId,
                             MobaBrainSourceKinds.BattleTemplate,
                             loadout.SpawnIndex))
                {
                    changedCount++;
                }
            }

            if (configuredCount == 0)
            {
                message = "敌方槽位未配置 AI";
                return false;
            }

            message = disable
                ? $"已关闭 {changedCount} 个敌方角色的 AI"
                : $"已开启 {changedCount}/{configuredCount} 个敌方角色的 AI";
            return changedCount > 0;
        }

        private static bool TryResolveEnemyActorServices(
            BattleContext ctx,
            out MobaPlayerLoadout[] loadouts,
            out int localTeamId,
            out MobaPlayerActorMapService playerActors,
            out MobaActorLookupService actors)
        {
            loadouts = null;
            localTeamId = 0;
            playerActors = null;
            actors = null;
            if (ctx == null ||
                !TryResolveWorldService(ctx, out playerActors) ||
                !TryResolveWorldService(ctx, out actors))
            {
                return false;
            }

            loadouts = ctx.BuildEffectivePlayerLoadouts();
            if (loadouts == null || loadouts.Length == 0) return false;

            var localPlayerId = ctx.Plan.LaunchSpec.LocalPlayerId.Value;
            if (string.IsNullOrEmpty(localPlayerId)) localPlayerId = ctx.ResolveLocalControlPlayerId();

            for (var i = 0; i < loadouts.Length; i++)
            {
                if (!string.Equals(loadouts[i].PlayerId.Value, localPlayerId, StringComparison.OrdinalIgnoreCase)) continue;
                localTeamId = loadouts[i].TeamId;
                return true;
            }

            return false;
        }

        public bool TryReplaceHero(int heroId, out string message)
        {
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (heroId <= 0)
            {
                message = "英雄 ID 必须大于 0";
                return false;
            }

            var playerId = new PlayerId(CurrentPlayerId);
            if (string.IsNullOrEmpty(playerId.Value))
            {
                message = "当前控制玩家 ID 为空";
                return false;
            }

            var worldId = BattleInputSessionIdentity.ResolveWorldId(in ctx.Plan);
            var nextFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in ctx.Plan);
            var command = BattleInputCommandFactory.CreateDebugReplaceHero(
                nextFrame,
                playerId,
                heroId);
            var submitter = new BattleInputSubmitter(ctx, playerId, worldId);
            submitter.Submit(in command);

            message = $"英雄替换命令已提交，目标英雄 ID={heroId}";
            return true;
        }

        public bool TrySpawnAlly(out string message)
        {
            return TrySubmitSpawnUnit(MobaDebugSpawnUnitRelation.Ally, out message);
        }

        public bool TrySpawnEnemy(out string message)
        {
            return TrySubmitSpawnUnit(MobaDebugSpawnUnitRelation.Enemy, out message);
        }

        private bool TrySubmitSpawnUnit(MobaDebugSpawnUnitRelation relation, out string message)
        {
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            var playerId = BattleInputSessionIdentity.ResolvePlayerId(ctx);
            if (string.IsNullOrEmpty(playerId.Value))
            {
                message = "当前控制玩家 ID 为空";
                return false;
            }

            var worldId = BattleInputSessionIdentity.ResolveWorldId(in ctx.Plan);
            var nextFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in ctx.Plan);
            var command = BattleInputCommandFactory.CreateDebugSpawnUnit(nextFrame, playerId, relation);
            var submitter = new BattleInputSubmitter(ctx, playerId, worldId);
            submitter.Submit(in command);

            message = relation == MobaDebugSpawnUnitRelation.Enemy
                ? "敌方单位生成命令已提交"
                : "己方单位生成命令已提交";
            return true;
        }

        private bool TryResolveCurrentActor(out global::ActorEntity actor, out string message)
        {
            actor = null;
            message = string.Empty;
            var ctx = Context;
            if (!IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (ctx.LocalActorId <= 0)
            {
                if (!TryRefreshCurrentActorId(out message)) return false;
            }

            if (!TryResolveWorldService(ctx, out MobaActorLookupService actors) || actors == null)
            {
                message = "角色查询服务缺失";
                return false;
            }

            if (!actors.TryGetActorEntity(ctx.LocalActorId, out actor) || actor == null)
            {
                message = $"角色缺失，ID={ctx.LocalActorId}";
                return false;
            }

            return true;
        }

        private bool TryRefreshCurrentActorId(out string message)
        {
            var playerId = new PlayerId(CurrentPlayerId);
            return TrySetControlPlayer(playerId, out message);
        }

        private static bool TryResolveWorldService<T>(BattleContext ctx, out T service) where T : class
        {
            service = null;
            if (ctx == null ||
                !ctx.TryGetRuntimeWorld(out var world) ||
                world.Services == null)
            {
                return false;
            }

            return world.Services.TryResolve(out service) && service != null;
        }
    }
}
