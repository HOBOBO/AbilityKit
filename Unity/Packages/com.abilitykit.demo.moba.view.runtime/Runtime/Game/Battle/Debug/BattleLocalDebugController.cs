using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Eventing;

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
        /// <summary>
        /// 主控英雄的当前等级；零表示无法解析可调试的英雄。
        /// </summary>
        public readonly int CurrentHeroLevel;
        public readonly bool IncludePlaceholderHeroes;

        public BattleLocalDebugSnapshot(
            bool isAvailable,
            string hostModeName,
            string unavailableReason,
            string currentPlayerId,
            int currentActorId,
            bool isEnemyAiEnabled,
            BattleDebugHeroOption[] heroOptions,
            int currentHeroId,
            int currentHeroLevel,
            bool includePlaceholderHeroes)
        {
            IsAvailable = isAvailable;
            HostModeName = hostModeName ?? string.Empty;
            UnavailableReason = unavailableReason ?? string.Empty;
            CurrentPlayerId = currentPlayerId ?? string.Empty;
            CurrentActorId = currentActorId;
            IsEnemyAiEnabled = isEnemyAiEnabled;
            HeroOptions = heroOptions ?? Array.Empty<BattleDebugHeroOption>();
            CurrentHeroId = currentHeroId;
            CurrentHeroLevel = currentHeroLevel;
            IncludePlaceholderHeroes = includePlaceholderHeroes;
        }
    }

    internal sealed class BattleLocalDebugQuery
    {
        private static readonly BattleDebugHeroOption[] EmptyHeroOptions = Array.Empty<BattleDebugHeroOption>();

        private readonly Func<BattleContext> _ctxResolver;
        private BattleDebugHeroOption[] _heroOptions = EmptyHeroOptions;
        private MobaConfigDatabase _heroOptionsDatabase;
        private long _heroOptionsVersion = -1;
        private bool _includePlaceholderHeroes;

        public BattleLocalDebugQuery(Func<BattleContext> ctxResolver)
        {
            _ctxResolver = ctxResolver;
        }

        internal BattleContext Context => _ctxResolver?.Invoke();

        internal bool TryResolveSkillRank(int slot, out ActiveSkillRuntime skill, out int maxRank)
        {
            skill = null;
            maxRank = 0;
            if (!TryResolveCurrentHero(out var actor) || !actor.hasSkillLoadout || actor.hasActorDespawnRequest ||
                !TryResolveWorldService(Context, out MobaConfigDatabase config)) return false;

            var skills = actor.skillLoadout.ActiveSkills;
            if (slot < 1 || skills == null || slot > skills.Length || skills[slot - 1] == null) return false;
            skill = skills[slot - 1];
            if (!config.TryGetSkill(skill.SkillId, out var definition) || definition == null ||
                definition.LevelTableId <= 0 || !config.TryGetSkillLevelTable(definition.LevelTableId, out var table) ||
                table?.Levels == null) return false;

            foreach (var row in table.Levels)
            {
                if (row == null) break;
                maxRank++;
            }
            return maxRank > 0;
        }

        internal void ReadTrainingOptions(out bool noCooldown, out bool infiniteMana)
        {
            noCooldown = false;
            infiniteMana = false;
            var ctx = Context;
            if (!IsAvailable || !TryResolveWorldService(ctx, out MobaSkillEconomyService economy)) return;
            noCooldown = economy.IsTrainingNoCooldown(ctx.LocalActorId);
            infiniteMana = economy.IsTrainingInfiniteMana(ctx.LocalActorId);
        }

        internal bool TryReadRanges(int skillSlot, int targetId, out Vec3 position, out float attackRange,
            out float skillRange, out int resolvedTargetId, out float distance)
        {
            position = default;
            attackRange = skillRange = 0f;
            resolvedTargetId = 0;
            distance = float.PositiveInfinity;
            var ctx = Context;
            if (!TryResolveCurrentHero(out var actor) || !actor.hasTransform || !actor.hasTeam ||
                !ctx.TryGetRuntimeWorld(out var world) || world.Services == null) return false;

            position = actor.transform.Value.Position;
            var skills = actor.hasSkillLoadout ? actor.skillLoadout.ActiveSkills : null;
            for (var slot = 1; skills != null && slot <= skills.Length; slot++)
            {
                if (!MobaSkillRuntimeAccess.TryReadCastRange(world.Services, actor, slot, out _, out var kind, out var range))
                    continue;
                if (kind == SkillType.NormalAttack) attackRange = Math.Max(attackRange, range);
                if (slot == skillSlot) skillRange = range;
            }

            if (!TryResolveWorldService(ctx, out MobaActorRegistry registry)) return true;
            foreach (var id in registry.CopyActorIdsInOrder())
            {
                if ((targetId > 0 && id != targetId) || !registry.TryGet(id, out var target) ||
                    !target.isEnabled || !target.hasTeam || target.team.Value == actor.team.Value ||
                    !target.hasTransform || !target.hasSkillLoadout || target.hasActorDespawnRequest) continue;
                var delta = target.transform.Value.Position - position;
                var planarDistance = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
                if (planarDistance >= distance) continue;
                distance = planarDistance;
                resolvedTargetId = id;
            }
            return true;
        }

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

        public bool IncludePlaceholderHeroes => _includePlaceholderHeroes;

        public void SetIncludePlaceholderHeroes(bool enabled)
        {
            if (_includePlaceholderHeroes == enabled) return;
            _includePlaceholderHeroes = enabled;
            _heroOptionsDatabase = null;
            _heroOptionsVersion = -1;
        }

        public bool IsHeroEnabled(int heroId)
        {
            if (!MobaHeroContentGroups.IsEnabled(heroId, _includePlaceholderHeroes)) return false;
            var ctx = Context;
            return TryResolveWorldService(ctx, out MobaConfigDatabase config) &&
                   config != null &&
                   MobaHeroLoadoutResolver.TryResolveHeroConfig(
                       config, heroId, out _, out _, out _, out _);
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
                CurrentHeroId,
                TryResolveCurrentHero(out var hero) ? hero.championLevel.Value : 0,
                IncludePlaceholderHeroes);
        }

        /// <summary>
        /// 解析本地调试面板当前控制且具有等级组件的英雄实体。
        /// </summary>
        internal bool TryResolveCurrentHero(out global::ActorEntity hero)
        {
            hero = null;
            var ctx = Context;
            if (ctx == null || ctx.Session == null || ctx.Plan.HostMode != BattleHostMode.Local ||
                ctx.LocalActorId <= 0 ||
                !TryResolveWorldService(ctx, out MobaActorLookupService actors) ||
                !actors.TryGetActorEntity(ctx.LocalActorId, out var actor) ||
                !actor.hasUnitSubType || actor.unitSubType.Value != UnitSubType.Hero || !actor.hasChampionLevel)
            {
                return false;
            }

            hero = actor;
            return true;
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
                    !MobaHeroContentGroups.IsEnabled(character.Id, _includePlaceholderHeroes) ||
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
        private readonly List<(int Id, int CreationIndex)> _dummies = new List<(int, int)>();
        private BattleContext _dummyContext;

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

            if (ctx.LocalActorId != actorId && TryResolveWorldService(ctx, out MobaSkillEconomyService training))
                training.SetLocalTraining(0, null, false, false);
            ctx.LocalControlPlayerId = playerId.Value;
            ctx.ApplyLocalActorIdentity(actorId);
            _hudResolver?.Invoke()?.RefreshLocalControlSkillTemplates();
            message = $"已切换至玩家 {playerId.Value}，角色 ID={actorId}";
            return true;
        }

        public bool TryResetCooldowns(out string message)
        {
            if (!TryResolveTraining(out var economy, out message)) return false;
            var count = economy.ResetLocalTrainingCooldowns(Context.LocalActorId);
            message = $"已重置 {count} 个技能的冷却时间";
            return count > 0;
        }

        public bool TrySetTrainingOptions(bool noCooldown, bool infiniteMana, out string message)
        {
            if (!TryResolveTraining(out var economy, out message)) return false;
            var ctx = Context;
            economy.SetLocalTraining(ctx.LocalActorId, () => _query.IsAvailable && ReferenceEquals(Context, ctx)
                ? ctx.LocalActorId : 0, noCooldown, infiniteMana);
            message = $"技能无冷却：{(noCooldown ? "开" : "关")}；无限法力：{(infiniteMana ? "开" : "关")}";
            return true;
        }

        public bool TryRestoreResources(out string message)
        {
            if (!TryResolveCurrentActor(out _, out message)) return false;
            var ctx = Context;
            if (!TryResolveWorldService(ctx, out MobaActorLookupService actors) ||
                !MobaSkillRuntimeAccess.TryRestoreSkillResources(actors, ctx.LocalActorId, out var restored))
            {
                message = "技能资源不可用";
                return false;
            }

            message = $"已补满 {restored} 项技能资源（不含生命）";
            return true;
        }

        /// <inheritdoc />
        public bool TrySetSkillRank(int slot, int rank, out string message)
        {
            if (!_query.TryResolveSkillRank(slot, out var skill, out var maximum))
            {
                message = "当前技能槽没有可调整的等级表";
                return false;
            }
            if (rank < 1 || rank > maximum || !TryResolveWorldService(Context, out MobaConfigDatabase config) ||
                !ResolvedSkillCastConfiguration.TryResolve(config, skill.SkillId, rank, out _, out _))
            {
                message = $"槽 {slot} 的技能 Rank 无效（可用范围 1–{maximum}）";
                return false;
            }

            skill.Level = rank;
            message = $"槽 {slot} 技能 Rank：{rank}/{maximum}";
            return true;
        }

        private bool TryResolveTraining(out MobaSkillEconomyService economy, out string message)
        {
            economy = null;
            if (!TryResolveCurrentActor(out _, out message)) return false;
            if (TryResolveWorldService(Context, out economy)) return true;
            message = "技能经济服务缺失";
            return false;
        }

        public bool TrySpawnDummy(float offsetX, float offsetZ, out string message)
        {
            if (!TryResolveCurrentActor(out var source, out message)) return false;
            var ctx = Context;
            if (!source.hasTransform || !source.hasTeam || !source.hasSkillLoadout ||
                !TryResolveWorldService(ctx, out MobaConfigDatabase config) ||
                !TryResolveWorldService(ctx, out IMobaActorSpawnService spawn) ||
                !TryResolveWorldService(ctx, out ActorEntityInitPipeline initializer) ||
                !TryResolveWorldService(ctx, out MobaActorRegistry registry) ||
                !TryResolveWorldService(ctx, out MobaActorSpawnSnapshotService snapshots))
            {
                message = "训练靶生成依赖缺失";
                return false;
            }
            if (float.IsNaN(offsetX) || float.IsNaN(offsetZ) || Math.Abs(offsetX) > 100f || Math.Abs(offsetZ) > 100f)
            {
                message = "偏移 X/Z 必须在 -100 到 100 之间";
                return false;
            }

            var position = source.transform.Value.Position + new Vec3(offsetX, 0f, offsetZ);
            var opposingTeam = (int)source.team.Value == (int)Team.Team1 ? (int)Team.Team2 : (int)Team.Team1;
            var level = source.hasChampionLevel ? source.championLevel.Value : 1;
            if (!MobaHeroLoadoutResolver.TryResolve(config, new PlayerId(_query.CurrentPlayerId), opposingTeam,
                    source.skillLoadout.HeroId, level, 0, in position, out var loadout, out message)) return false;

            var spec = MobaConverter.ToActorBuildSpec(0, in loadout);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.AllocateActorIdIfMissing = true;
            request.Initializer = (entity, _) =>
            {
                if (!initializer.TryInitializeFromLoadout(entity, in loadout, out var error))
                    throw new InvalidOperationException(error ?? "训练靶初始化失败");
                if (entity.hasActorBrain) entity.RemoveActorBrain();
            };
            if (!spawn.TrySpawn(in request, out var result) || !result.Success ||
                !registry.TryGet(result.ActorId, out var dummy))
            {
                message = result.Error ?? "训练靶生成失败";
                return false;
            }

            ResetDummyTrackingFor(ctx);
            if (!snapshots.TryEnqueueActorSpawn(registry, result.ActorId, SpawnEntityKind.Character,
                    loadout.HeroId, ctx.LocalActorId))
            {
                ActorLifecycleRequests.RequestDespawn(dummy, ctx.LastFrame, ActorDespawnReason.SceneCleanup);
                message = "训练靶生成事件发布失败";
                return false;
            }
            _dummies.Add((result.ActorId, dummy.creationIndex));
            message = $"已放置固定靶 #{result.ActorId}";
            return true;
        }

        public bool TryClearDummies(out string message)
        {
            var ctx = Context;
            if (!_query.IsAvailable || !TryResolveWorldService(ctx, out MobaActorLookupService actors))
            {
                message = "本地战斗不可用";
                return false;
            }
            ResetDummyTrackingFor(ctx);
            var count = 0;
            foreach (var dummy in _dummies)
            {
                if (actors.TryGetActorEntity(dummy.Id, out var actor) && actor.creationIndex == dummy.CreationIndex &&
                    ActorLifecycleRequests.RequestDespawn(actor, ctx.LastFrame, ActorDespawnReason.SceneCleanup)) count++;
            }
            _dummies.Clear();
            message = $"已请求清除 {count} 个训练靶";
            return true;
        }

        private void ResetDummyTrackingFor(BattleContext ctx)
        {
            if (ReferenceEquals(_dummyContext, ctx)) return;
            _dummyContext = ctx;
            _dummies.Clear();
        }

        /// <inheritdoc />
        public bool TryIncreaseHeroLevel(out string message) => TryChangeHeroLevel(reset: false, out message);

        /// <inheritdoc />
        public bool TryResetHeroLevel(out string message) => TryChangeHeroLevel(reset: true, out message);

        private bool TryChangeHeroLevel(bool reset, out string message)
        {
            if (!_query.IsAvailable)
            {
                message = "本地战斗不可用";
                return false;
            }

            if (!_query.TryResolveCurrentHero(out var hero))
            {
                message = "当前主控英雄尚未就绪";
                return false;
            }

            var previousLevel = hero.championLevel.Value;
            if (!reset && previousLevel == int.MaxValue)
            {
                message = "当前英雄等级无法继续增加";
                return false;
            }

            var level = reset ? 1 : previousLevel + 1;
            if (level != previousLevel)
            {
                if (!hero.hasSkillLoadout ||
                    !TryResolveWorldService(Context, out MobaConfigDatabase config) ||
                    !config.TryGetCharacter(hero.skillLoadout.HeroId, out var character) || character == null ||
                    !config.TryGetAttributeTemplate(character.AttributeTemplateId, out var template) || template == null)
                {
                    message = "当前主控英雄的属性模板缺失";
                    return false;
                }

                if (!MobaActorAttributeInitializer.TryApplyDebugLevel(hero, template, level))
                {
                    message = "当前主控英雄的属性或资源尚未就绪，等级未改变";
                    return false;
                }
            }
            message = reset ? "英雄等级已重置为 1 级" : $"英雄等级已提升至 {level} 级";
            return true;
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

            if (!_query.IsHeroEnabled(heroId))
            {
                message = "该英雄未启用，或缺少完整的战斗配置";
                return false;
            }

            var playerId = new PlayerId(_query.CurrentPlayerId);
            if (string.IsNullOrEmpty(playerId.Value))
            {
                message = "当前控制玩家 ID 为空";
                return false;
            }

            var plan = ctx.Plan;
            var worldId = BattleInputSessionIdentity.ResolveWorldId(in plan);
            var nextFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan);
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

            var playerId = BattleInputSessionIdentity.ResolvePlayerId(
                (IBattleInputSessionIdentityPort)ctx);
            if (string.IsNullOrEmpty(playerId.Value))
            {
                message = "当前控制玩家 ID 为空";
                return false;
            }

            var plan = ctx.Plan;
            var worldId = BattleInputSessionIdentity.ResolveWorldId(in plan);
            var nextFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan);
            var command = BattleInputCommandFactory.CreateDebugSpawnUnit(nextFrame, playerId, relation);
            var submitter = new BattleInputSubmitter(ctx, playerId, worldId);
            if (!submitter.Submit(in command))
            {
                message = "单位生成命令提交失败，请在战斗就绪后重试";
                return false;
            }

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
        public bool IncludePlaceholderHeroes => _query.IncludePlaceholderHeroes;

        public BattleLocalDebugSnapshot CaptureSnapshot() => _query.CaptureSnapshot();

        public void SetIncludePlaceholderHeroes(bool enabled) =>
            _query.SetIncludePlaceholderHeroes(enabled);

        public bool TrySwitchControl(out string message) => _commands.TrySwitchControl(out message);

        public bool TrySetControlPlayer(PlayerId playerId, out string message) =>
            _commands.TrySetControlPlayer(playerId, out message);

        public bool TryResetCooldowns(out string message) => _commands.TryResetCooldowns(out message);

        internal void ReadTrainingOptions(out bool noCooldown, out bool infiniteMana) =>
            _query.ReadTrainingOptions(out noCooldown, out infiniteMana);

        internal bool TrySetTrainingOptions(bool noCooldown, bool infiniteMana, out string message) =>
            _commands.TrySetTrainingOptions(noCooldown, infiniteMana, out message);

        internal bool TryRestoreResources(out string message) => _commands.TryRestoreResources(out message);

        internal bool TryReadSkillRank(int slot, out int rank, out int maximum)
        {
            var hasRank = _query.TryResolveSkillRank(slot, out var skill, out maximum);
            rank = skill?.Level ?? 0;
            return hasRank;
        }

        internal bool TrySetSkillRank(int slot, int rank, out string message) =>
            _commands.TrySetSkillRank(slot, rank, out message);

        internal bool TrySpawnDummy(float offsetX, float offsetZ, out string message) =>
            _commands.TrySpawnDummy(offsetX, offsetZ, out message);

        internal bool TryClearDummies(out string message) => _commands.TryClearDummies(out message);

        internal bool TryReadRanges(int skillSlot, int targetId, out Vec3 position, out float attackRange,
            out float skillRange, out int resolvedTargetId, out float distance) =>
            _query.TryReadRanges(skillSlot, targetId, out position, out attackRange, out skillRange,
                out resolvedTargetId, out distance);

        /// <summary>
        /// 将当前主控英雄的调试等级提高一级。
        /// </summary>
        public bool TryIncreaseHeroLevel(out string message) => _commands.TryIncreaseHeroLevel(out message);

        /// <summary>
        /// 将当前主控英雄的调试等级重置为一级。
        /// </summary>
        public bool TryResetHeroLevel(out string message) => _commands.TryResetHeroLevel(out message);

        public bool TryToggleEnemyAi(out string message) => _commands.TryToggleEnemyAi(out message);
        public bool TryReplaceHero(int heroId, out string message) => _commands.TryReplaceHero(heroId, out message);
        public bool TrySpawnAlly(out string message) => _commands.TrySpawnAlly(out message);
        public bool TrySpawnEnemy(out string message) => _commands.TrySpawnEnemy(out message);
    }

    /// <summary>
    /// 仅在本地训练 HUD 中使用的读数：累计已发布的伤害事件，读取既有施法诊断。
    /// </summary>
    internal sealed class BattleTrainingReadout : IDisposable
    {
        private BattleContext _context;
        private IEventBus _eventBus;
        private IDisposable _damageSubscription;
        private int _actorId;
        private int _entityVersion;
        private int _targetId;
        private int _firstFrame;
        private long _failureSequence;
        private long _lastRevision = -1;

        internal float LastHit { get; private set; }
        internal double TotalDamage { get; private set; }
        internal int HitCount { get; private set; }
        internal string FailureText { get; private set; }

        internal void Bind(BattleContext context)
        {
            IWorld world = null;
            IEventBus eventBus = null;
            if (context != null && context.TryGetRuntimeWorld(out world))
                world.Services.TryResolve(out eventBus);
            var actorId = context?.LocalActorId ?? 0;
            var version = context?.GetActorEntityVersion(actorId) ?? 0;
            if (!ReferenceEquals(context, _context) || !ReferenceEquals(eventBus, _eventBus))
            {
                _damageSubscription?.Dispose();
                _context = context;
                _eventBus = eventBus;
                _damageSubscription = eventBus?.Subscribe(
                    new EventKey<MobaHealthChangeResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted)),
                    AddDamage);
                _actorId = 0;
                _entityVersion = 0;
                _targetId = 0;
                ResetDamage();
            }
            if (_actorId != actorId || _entityVersion != version)
            {
                _actorId = actorId;
                _entityVersion = version;
                _firstFrame = (context?.LastFrame ?? 0);
                _failureSequence = 0;
                _lastRevision = -1;
                FailureText = null;
                ResetDamage();
                if (world != null && world.Services.TryResolve<IMobaBattleDiagnosticCaptureControl>(out var control))
                    _failureSequence = control.LastSequence;
            }
            if (context == null) FailureText = null;
        }

        internal void SetTarget(int targetId)
        {
            if (_targetId == targetId) return;
            _targetId = targetId;
            ResetDamage();
        }

        internal void ResetDamage()
        {
            LastHit = 0;
            TotalDamage = 0;
            HitCount = 0;
        }

        private void AddDamage(MobaHealthChangeResult result)
        {
            if (_actorId <= 0 || result.SourceActorId != _actorId || result.Kind != MobaHealthChangeKind.Damage ||
                (_targetId > 0 && result.TargetActorId != _targetId) || !(result.AppliedValue > 0) ||
                float.IsInfinity(result.AppliedValue)) return;
            LastHit = result.AppliedValue;
            TotalDamage += result.AppliedValue;
            HitCount++;
        }

        internal void RefreshFailure()
        {
            if (_actorId <= 0 || _context == null || !_context.TryGetRuntimeWorld(out var world) ||
                !world.Services.TryResolve<IBattleDiagnosticEventReadStore>(out var store)) return;
            var revision = store.Revision;
            if (revision == _lastRevision) return;
            _lastRevision = revision;
            var filter = new BattleDiagnosticFilter(new BattleDiagnosticFrameFilter(_firstFrame, int.MaxValue),
                BattleDiagnosticEventChannel.Skill, _actorId, BattleDiagnosticActorRelation.Source,
                failuresOnly: true, searchText: nameof(BattleDiagnosticEventKind.SkillFailure));
            var query = new BattleDiagnosticEventQuery(1, filter,
                new BattleDiagnosticPageRequest(revision, 0, 1), newestFirst: true);
            var events = store.Query(query).Items;
            if (events == null || events.Count == 0 || events[0].Sequence <= _failureSequence ||
                !events[0].Payload.TryGetSkillFailure(out var failure)) return;
            var failedEvent = events[0];
            _failureSequence = failedEvent.Sequence;
            var detail = FormatFailure(ReadEconomyRejection(store, revision, in failedEvent) ?? failure.Message,
                failure.Code);
            var source = failure.Slot > 0 ? $"槽 {failure.Slot}" : "技能";
            FailureText = $"{source} · {detail}（帧 {failedEvent.Frame}）";
        }

        private static string ReadEconomyRejection(IBattleDiagnosticEventReadStore store, long revision,
            in BattleDiagnosticEvent failedEvent)
        {
            if (!failedEvent.SkillRuntime.IsValid) return null;
            var filter = new BattleDiagnosticFilter(new BattleDiagnosticFrameFilter(failedEvent.Frame, failedEvent.Frame),
                BattleDiagnosticEventChannel.Skill, failedEvent.SourceActorId, BattleDiagnosticActorRelation.Source,
                skillRuntimeId: failedEvent.SkillRuntime.RuntimeId, failuresOnly: true,
                searchText: nameof(BattleDiagnosticSkillExecutionStage.EconomyRejected));
            var query = new BattleDiagnosticEventQuery(1, filter,
                new BattleDiagnosticPageRequest(revision, 0, 1), newestFirst: true);
            var events = store.Query(query).Items;
            if (events == null || events.Count == 0) return null;
            var rejection = events[0];
            return rejection.Kind == BattleDiagnosticEventKind.SkillEconomy &&
                   rejection.SkillRuntime.Equals(failedEvent.SkillRuntime) && rejection.Sequence < failedEvent.Sequence &&
                   rejection.Payload.TryGetSkillExecution(out var execution) &&
                   execution.Stage == BattleDiagnosticSkillExecutionStage.EconomyRejected &&
                   !string.IsNullOrWhiteSpace(execution.Detail) ? execution.Detail : null;
        }

        private static string FormatFailure(string detail, string code)
        {
            if (detail.IndexOf("within cast range", StringComparison.OrdinalIgnoreCase) >= 0)
                return "范围内没有有效目标";
            if (detail.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0 &&
                detail.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0) return "资源不足";
            if (detail.IndexOf("no available charges", StringComparison.OrdinalIgnoreCase) >= 0) return "充能不足";
            if (detail.IndexOf("cooling down", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0) return $"冷却中：{detail}";
            return string.IsNullOrWhiteSpace(detail) ? code : detail;
        }

        /// <summary>
        /// 解绑当前战斗事件，不保留跨会话读数。
        /// </summary>
        public void Dispose() => Bind(null);
    }
}
