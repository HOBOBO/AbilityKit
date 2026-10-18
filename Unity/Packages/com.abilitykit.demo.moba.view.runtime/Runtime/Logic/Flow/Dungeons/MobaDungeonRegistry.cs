using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 副本扩展注册中心。把副本用到的所有扩展点收在一处：玩法处理器、场景装配、胜负规则。
    /// 新增扩展点只需在这里注册，其余系统（Flow、校验、结算）自动发现。
    /// </summary>
    public sealed class MobaDungeonRegistry
    {
        private readonly Dictionary<string, IDungeonGameplayHandler> _gameplayHandlers =
            new Dictionary<string, IDungeonGameplayHandler>(StringComparer.Ordinal);
        private readonly Dictionary<string, IBattleSceneProvider> _sceneProviders =
            new Dictionary<string, IBattleSceneProvider>(StringComparer.Ordinal);
        private readonly Dictionary<string, IBattleOutcomeRule> _outcomeRules =
            new Dictionary<string, IBattleOutcomeRule>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownPortGroups = new HashSet<string>(StringComparer.Ordinal);

        public static MobaDungeonRegistry CreateStandard() => new MobaDungeonRegistry()
            .RegisterGameplayHandler(new TeamDeathmatchGameplayHandler())
            .RegisterGameplayHandler(new TriggersOnlyGameplayHandler())
            .RegisterSceneProvider(new NullBattleSceneProvider())
            .RegisterOutcomeRule(new TeamDeathmatchOutcomeRule())
            .RegisterOutcomeRule(new TriggersAuthoritativeOutcomeRule())
            .DeclarePortGroup("deathmatch");

        public MobaDungeonRegistry RegisterGameplayHandler(IDungeonGameplayHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (string.IsNullOrWhiteSpace(handler.Id))
                throw new ArgumentException("Gameplay handler id is required.", nameof(handler));
            if (_gameplayHandlers.ContainsKey(handler.Id))
                throw new InvalidOperationException("Duplicate dungeon gameplay handler: " + handler.Id);
            _gameplayHandlers.Add(handler.Id, handler);
            return this;
        }

        public MobaDungeonRegistry RegisterSceneProvider(IBattleSceneProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(provider.Id))
                throw new ArgumentException("Scene provider id is required.", nameof(provider));
            if (_sceneProviders.ContainsKey(provider.Id))
                throw new InvalidOperationException("Duplicate dungeon scene provider: " + provider.Id);
            _sceneProviders.Add(provider.Id, provider);
            return this;
        }

        public MobaDungeonRegistry RegisterOutcomeRule(IBattleOutcomeRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Outcome rule id is required.", nameof(rule));
            if (_outcomeRules.ContainsKey(rule.Id))
                throw new InvalidOperationException("Duplicate dungeon outcome rule: " + rule.Id);
            _outcomeRules.Add(rule.Id, rule);
            return this;
        }

        /// <summary>声明一个已知的 Feature 端口组，供副本配置引用。</summary>
        public MobaDungeonRegistry DeclarePortGroup(string portGroup)
        {
            if (string.IsNullOrWhiteSpace(portGroup))
                throw new ArgumentException("Port group id is required.", nameof(portGroup));
            _knownPortGroups.Add(portGroup);
            return this;
        }

        public bool ContainsGameplayHandler(string id) =>
            !string.IsNullOrWhiteSpace(id) && _gameplayHandlers.ContainsKey(id);
        public bool ContainsSceneProvider(string id) =>
            !string.IsNullOrWhiteSpace(id) && _sceneProviders.ContainsKey(id);
        public bool ContainsOutcomeRule(string id) =>
            !string.IsNullOrWhiteSpace(id) && _outcomeRules.ContainsKey(id);
        public bool ContainsPortGroup(string portGroup) =>
            !string.IsNullOrWhiteSpace(portGroup) && _knownPortGroups.Contains(portGroup);

        public IDungeonGameplayHandler GetGameplayHandler(string id) =>
            _gameplayHandlers.TryGetValue(id ?? string.Empty, out var handler)
                ? handler
                : throw new InvalidOperationException("Dungeon gameplay handler is not registered: " + id);

        public IBattleSceneProvider GetSceneProvider(string id) =>
            _sceneProviders.TryGetValue(id ?? string.Empty, out var provider)
                ? provider
                : throw new InvalidOperationException("Dungeon scene provider is not registered: " + id);

        public IBattleOutcomeRule GetOutcomeRule(string id) =>
            _outcomeRules.TryGetValue(id ?? string.Empty, out var rule)
                ? rule
                : throw new InvalidOperationException("Dungeon outcome rule is not registered: " + id);

        public IReadOnlyCollection<string> GameplayHandlerIds => _gameplayHandlers.Keys;
        public IReadOnlyCollection<string> SceneProviderIds => _sceneProviders.Keys;
        public IReadOnlyCollection<string> OutcomeRuleIds => _outcomeRules.Keys;
        public IReadOnlyCollection<string> PortGroups => _knownPortGroups;
    }
}
