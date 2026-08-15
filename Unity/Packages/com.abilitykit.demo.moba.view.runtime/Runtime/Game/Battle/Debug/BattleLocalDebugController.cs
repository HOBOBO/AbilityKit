using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
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

    internal readonly struct BattleLocalDebugSnapshot
    {
        public readonly bool IsAvailable;
        public readonly string HostModeName;
        public readonly string UnavailableReason;
        public readonly string CurrentPlayerId;
        public readonly int CurrentActorId;
        public readonly bool IsEnemyAiEnabled;
        public readonly BattleDebugHeroOption[] HeroOptions;
        public readonly int CurrentHeroId;

        public BattleLocalDebugSnapshot(
            bool isAvailable,
            string hostModeName,
            string unavailableReason,
            string currentPlayerId,
            int currentActorId,
            bool isEnemyAiEnabled,
            BattleDebugHeroOption[] heroOptions,
            int currentHeroId)
        {
            IsAvailable = isAvailable;
            HostModeName = hostModeName ?? string.Empty;
            UnavailableReason = unavailableReason ?? string.Empty;
            CurrentPlayerId = currentPlayerId ?? string.Empty;
            CurrentActorId = currentActorId;
            IsEnemyAiEnabled = isEnemyAiEnabled;
            HeroOptions = heroOptions ?? Array.Empty<BattleDebugHeroOption>();
            CurrentHeroId = currentHeroId;
        }
    }

    internal sealed class BattleLocalDebugQuery
    {
        private static readonly BattleDebugHeroOption[] EmptyHeroOptions = Array.Empty<BattleDebugHeroOption>();

        private readonly Func<BattleContext> _ctxResolver;
        private BattleDebugHeroOption[] _heroOptions = EmptyHeroOptions;
        private MobaConfigDatabase _heroOptionsDatabase;
        private long _heroOptionsVersion = -1;

        public BattleLocalDebugQuery(Func<BattleContext> ctxResolver)
        {
            _ctxResolver = ctxResolver;
        }

        internal BattleContext Context => _ctxResolver?.Invoke();

        public bool IsAvailable
        {
            get
            {
                var ctx = Context;
                return ctx != null && ctx.Session != null && ctx.Plan.HostMode == BattleHostMode.Local;
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
                if (ctx.Plan.HostMode != BattleHostMode.Local) return $"非本地模式（{ctx.Plan.HostMode}）";
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

        public BattleLocalDebugSnapshot CaptureSnapshot()
        {
            return new BattleLocalDebugSnapshot(
                IsAvailable,
                HostModeName,
                UnavailableReason,
                CurrentPlayerId,
                CurrentActorId,
                IsEnemyAiEnabled,
                HeroOptions,
                CurrentHeroId);
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

    internal sealed class BattleLocalDebugCommandService : IBattleDebugCommandService
    {
        private readonly BattleLocalDebugQuery _query;
        private readonly Func<BattleHudFeature> _hudResolver;

        public BattleLocalDebugCommandService(
            BattleLocalDebugQuery query,
            Func<BattleHudFeature> hudResolver)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _hudResolver = hudResolver;
        }

        private BattleContext Context => _query.Context;

        public bool TrySwitchControl(out string message)
        {
            message = string.Empty;
            var ctx = Context;
            if (!_query.IsAvailable)
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

            var current = _query.CurrentPlayerId;
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
            if (!_query.IsAvailable)
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
            if (!_query.IsAvailable)
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

            var disable = _query.IsEnemyAiEnabled;
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
            if (!_query.IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (heroId <= 0)
            {
                message = "英雄 ID 必须大于 0";
                return false;
            }

            var playerId = new PlayerId(_query.CurrentPlayerId);
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
            if (!_query.IsAvailable)
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
            if (!_query.IsAvailable)
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
            var playerId = new PlayerId(_query.CurrentPlayerId);
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

    internal sealed class BattleLocalDebugController
    {
        private readonly BattleLocalDebugQuery _query;
        private readonly IBattleDebugCommandService _commands;

        public BattleLocalDebugController(
            Func<BattleContext> ctxResolver,
            Func<BattleHudFeature> hudResolver)
        {
            _query = new BattleLocalDebugQuery(ctxResolver);
            _commands = new BattleLocalDebugCommandService(_query, hudResolver);
        }

        public bool IsAvailable => _query.IsAvailable;
        public bool HasSession => _query.HasSession;
        public string HostModeName => _query.HostModeName;
        public string UnavailableReason => _query.UnavailableReason;
        public string CurrentPlayerId => _query.CurrentPlayerId;
        public int CurrentActorId => _query.CurrentActorId;
        public bool IsEnemyAiEnabled => _query.IsEnemyAiEnabled;
        public BattleDebugHeroOption[] HeroOptions => _query.HeroOptions;
        public int CurrentHeroId => _query.CurrentHeroId;

        public BattleLocalDebugSnapshot CaptureSnapshot() => _query.CaptureSnapshot();

        public bool TrySwitchControl(out string message) => _commands.TrySwitchControl(out message);

        public bool TrySetControlPlayer(PlayerId playerId, out string message) =>
            _commands.TrySetControlPlayer(playerId, out message);

        public bool TryResetCooldowns(out string message) => _commands.TryResetCooldowns(out message);
        public bool TryToggleEnemyAi(out string message) => _commands.TryToggleEnemyAi(out message);
        public bool TryReplaceHero(int heroId, out string message) => _commands.TryReplaceHero(heroId, out message);
        public bool TrySpawnAlly(out string message) => _commands.TrySpawnAlly(out message);
        public bool TrySpawnEnemy(out string message) => _commands.TrySpawnEnemy(out message);
    }
}
