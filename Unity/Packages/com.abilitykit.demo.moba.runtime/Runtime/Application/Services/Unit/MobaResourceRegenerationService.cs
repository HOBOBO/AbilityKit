using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 由正式世界 System 驱动的非生命资源回复；状态仅归资源容器，不保存额外计时或历史回复。
    /// </summary>
    [WorldService(typeof(MobaResourceRegenerationService))]
    public sealed class MobaResourceRegenerationService : IService
    {
        private readonly MobaActorRegistry _actors;
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaConfigDatabase _config;
        private readonly List<int> _actorIds = new List<int>(32);
        private readonly List<Rule> _rules = new List<Rule>();
        private bool _loaded;

        private readonly struct Rule
        {
            internal readonly int Id;
            internal readonly ResourceType Resource;
            internal readonly AttributeId Rate;
            internal Rule(int id, ResourceType resource, AttributeId rate)
            { Id = id; Resource = resource; Rate = rate; }
        }

        /// <summary>
        /// 依赖本世界注册表、正式配置和运行门控；配置在首次正常推进时固定加载。
        /// </summary>
        public MobaResourceRegenerationService(MobaActorRegistry actors, MobaLogicWorldRunGateService phase,
            MobaConfigDatabase config)
        { _actors = actors; _phase = phase; _config = config; }

        /// <summary>
        /// 每个世界帧调用一次。死亡/缺资源不回复，满值不写入，不产生治疗或伤害事件。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!_phase.InGame || deltaSeconds <= 0) return;
            if (!_loaded) LoadRules();
            var dt = MobaResourceFixedConvert.ToFixed(deltaSeconds);
            _actorIds.Clear();
            foreach (var entry in _actors.Entries) _actorIds.Add(entry.Key);
            _actorIds.Sort();
            foreach (var id in _actorIds)
            {
                if (!_actors.TryGet(id, out var actor) || !actor.hasAttributeGroup || actor.attributeGroup.Group == null ||
                    !actor.hasResourceContainer || actor.resourceContainer.Value?.Map == null ||
                    !actor.resourceContainer.Value.Map.TryGetValue(ResourceType.Hp, out var hp) || hp == null || hp.Current <= Fixed64.Zero)
                    continue;
                foreach (var rule in _rules)
                {
                    if (!MobaResourceMutation.TryGetState(actor, rule.Resource, out var state)) continue;
                    var value = actor.attributeGroup.Group.GetValue(rule.Rate);
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidOperationException("Resource regeneration rate must be finite.");
                    if (value <= 0) continue;
                    MobaResourceMutation.Regenerate(actor, rule.Resource, state,
                        MobaResourceFixedConvert.ToFixed(value) * dt);
                }
            }
        }

        private void LoadRules()
        {
            var resources = new HashSet<ResourceType>();
            var ids = new HashSet<int>();
            var loaded = new List<Rule>();
            foreach (var row in _config.GetTable<ResourceRegenerationRuleMO>().All())
            {
                var resource = (ResourceType)row.ResourceType;
                var type = (BattleAttributeType)row.FlatPerSecondAttribute;
                var rate = MobaAttributeIds.Get(type);
                if (row.Id <= 0 || !ids.Add(row.Id) || !Enum.IsDefined(typeof(ResourceType), resource) ||
                    resource == ResourceType.None || resource == ResourceType.Hp || !resources.Add(resource) || !rate.IsValid ||
                    type == BattleAttributeType.HP || type == BattleAttributeType.MAX_HP || type == BattleAttributeType.EXTRA_HP ||
                    type == BattleAttributeType.MANA || type == BattleAttributeType.MAX_MANA ||
                    !_config.GetTable<AttrTypeMO>().TryGet(row.FlatPerSecondAttribute, out var attribute) || attribute.ValueKind != 0)
                    throw new InvalidOperationException("Invalid resource regeneration rule.");
                loaded.Add(new Rule(row.Id, resource, rate));
            }
            loaded.Sort((a, b) => a.Id.CompareTo(b.Id));
            _rules.AddRange(loaded); _loaded = true;
        }

        /// <summary>
        /// 清理世界内只读索引与帧内缓冲，不变更资源或属性。
        /// </summary>
        public void Dispose() { _actorIds.Clear(); _rules.Clear(); _loaded = false; }
    }
}
