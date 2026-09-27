using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Console.Platform;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    /// <summary>
    /// Console Moba 配置数据库（简化版，用于 Console 平台）
    /// 存储游戏运行时的所有配置数据
    /// </summary>
    public sealed class ConsoleMobaConfigDatabase
    {
        private readonly ITextAssetLoader _loader;
        private readonly Dictionary<int, CharacterConfig> _charactersById = new();
        private readonly Dictionary<int, SkillConfig> _skillsById = new();
        private readonly Dictionary<int, SkillLevelTableConfig> _skillLevelTablesById = new();
        private readonly Dictionary<int, AttributeTemplateConfig> _attributeTemplatesById = new();
        private readonly Dictionary<int, BuffConfig> _buffsById = new();
        private readonly Dictionary<int, ProjectileConfig> _projectilesById = new();
        private readonly Dictionary<int, EffectConfig> _effectsById = new();

        public const string DefaultResourcesDir = "luban/moba";

        public ConsoleMobaConfigDatabase(ITextAssetLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        /// <summary>
        /// 从默认目录加载所有配置
        /// </summary>
        public void LoadFromResources(string dir = DefaultResourcesDir)
        {
            LoadCharacters(dir);
            LoadSkills(dir);
            LoadSkillLevelTables(dir);
            LoadAttributeTemplates(dir);
            LoadBuffs(dir);
            LoadProjectiles(dir);
            LoadEffects(dir);
        }

        private void LoadCharacters(string dir)
        {
            var path = System.IO.Path.Combine(dir, "characters.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var characters = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CharacterConfig>>(json);
                    if (characters != null)
                    {
                        foreach (var c in characters)
                        {
                            _charactersById[c.Id] = c;
                        }
                        Log.System($"Loaded {characters.Count} character configs");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load characters: {ex.Message}");
                }
            }
        }

        private void LoadSkills(string dir)
        {
            var path = System.IO.Path.Combine(dir, "skills.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var skills = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SkillConfig>>(json);
                    if (skills != null)
                    {
                        foreach (var s in skills)
                        {
                            _skillsById[s.Id] = s;
                        }
                        Log.System($"Loaded {skills.Count} skill configs");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load skills: {ex.Message}");
                }
            }
        }

        private void LoadSkillLevelTables(string dir)
        {
            var path = System.IO.Path.Combine(dir, "skill_level_tables.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var tables = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SkillLevelTableConfig>>(json);
                    if (tables != null)
                    {
                        foreach (var t in tables)
                        {
                            _skillLevelTablesById[t.Id] = t;
                        }
                        Log.System($"Loaded {tables.Count} skill level tables");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load skill_level_tables: {ex.Message}");
                }
            }
        }

        private void LoadAttributeTemplates(string dir)
        {
            var path = System.IO.Path.Combine(dir, "attribute_templates.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var templates = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AttributeTemplateConfig>>(json);
                    if (templates != null)
                    {
                        foreach (var t in templates)
                        {
                            _attributeTemplatesById[t.Id] = t;
                        }
                        Log.System($"Loaded {templates.Count} attribute templates");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load attribute_templates: {ex.Message}");
                }
            }
        }

        private void LoadBuffs(string dir)
        {
            var path = System.IO.Path.Combine(dir, "buffs.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var buffs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BuffConfig>>(json);
                    if (buffs != null)
                    {
                        foreach (var b in buffs)
                        {
                            _buffsById[b.Id] = b;
                        }
                        Log.System($"Loaded {buffs.Count} buff configs");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load buffs: {ex.Message}");
                }
            }
        }

        private void LoadProjectiles(string dir)
        {
            var path = System.IO.Path.Combine(dir, "projectiles.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var projectiles = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ProjectileConfig>>(json);
                    if (projectiles != null)
                    {
                        foreach (var p in projectiles)
                        {
                            _projectilesById[p.Id] = p;
                        }
                        Log.System($"Loaded {projectiles.Count} projectile configs");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load projectiles: {ex.Message}");
                }
            }
        }

        private void LoadEffects(string dir)
        {
            if (dir.Replace('\\', '/') == DefaultResourcesDir &&
                _loader.TryLoadBytes("luban/moba_bytes/effects.bytes", out var bytes))
            {
                var table = new moba_luban.Effects(Luban.ByteBuf.Wrap(bytes));
                foreach (var row in table.DataList)
                {
                    _effectsById.Add(row.Id, new EffectConfig
                    {
                        Id = row.Id,
                        Name = row.Name,
                        EffectType = row.EffectType,
                        BaseDamage = row.BaseDamage,
                        DamageType = row.DamageType,
                        AttackRatio = row.AttackRatio,
                        TargetPolicy = row.TargetPolicy,
                        Radius = row.Radius,
                    });
                }
                Log.System($"Loaded {table.DataList.Count} effect configs from Luban binary");
                return;
            }

            var loaded = false;
            var path = System.IO.Path.Combine(dir, "effects.json");

            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    var effects = Newtonsoft.Json.JsonConvert.DeserializeObject<List<EffectConfig>>(json);
                    if (effects != null && effects.Count > 0)
                    {
                        foreach (var e in effects)
                        {
                            _effectsById[e.Id] = e;
                        }
                        Log.System($"Loaded {effects.Count} effect configs from effects.json");
                        loaded = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load effects.json: {ex.Message}");
                }
            }

            // 如果没有从 effects.json 加载成功，尝试从 effect_plans.json 加载（兼容旧配置）
            if (!loaded)
            {
                LoadEffectsFromPlans(dir);
            }
        }

        /// <summary>
        /// 从 effect_plans.json 兼容加载效果配置
        /// effect_plans.json 的格式与 EffectConfig 不同，需要转换
        /// </summary>
        private void LoadEffectsFromPlans(string dir)
        {
            var path = System.IO.Path.Combine(dir, "effect_plans.json");
            if (_loader.TryLoadText(path, out var json) && !string.IsNullOrEmpty(json))
            {
                try
                {
                    // effect_plans.json 的根结构是 { "Triggers": [...] }
                    var wrapper = Newtonsoft.Json.JsonConvert.DeserializeObject<EffectPlansWrapper>(json);
                    if (wrapper?.Triggers != null)
                    {
                        foreach (var trigger in wrapper.Triggers)
                        {
                            var effect = new EffectConfig
                            {
                                Id = trigger.TriggerId,
                                Name = $"Effect_{trigger.TriggerId}",
                                EffectType = 1, // 伤害类型
                                DamageType = 1, // 物理伤害
                            };

                            // 从 Actions 中提取伤害值
                            if (trigger.Actions != null && trigger.Actions.Count > 0)
                            {
                                var firstAction = trigger.Actions[0];
                                if (firstAction.Args != null)
                                {
                                    if (firstAction.Args.TryGetValue("damage_value", out var dv) && dv.ConstValue > 0)
                                    {
                                        effect.BaseDamage = (float)dv.ConstValue;
                                    }
                                    if (firstAction.Args.TryGetValue("damage_type", out var dt) && dt.ConstValue > 0)
                                    {
                                        effect.DamageType = (int)dt.ConstValue;
                                    }
                                    if (firstAction.Args.TryGetValue("attack_ratio", out var ar) && ar.ConstValue > 0)
                                    {
                                        effect.AttackRatio = (float)ar.ConstValue;
                                    }
                                }
                            }

                            _effectsById[effect.Id] = effect;
                        }
                        Log.System($"Loaded {wrapper.Triggers.Count} effects from effect_plans.json (legacy format)");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Failed to load effects from effect_plans.json: {ex.Message}");
                }
            }
        }

        public bool TryGetCharacter(int id, out CharacterConfig config) => _charactersById.TryGetValue(id, out config);
        public bool TryGetSkill(int id, out SkillConfig config) => _skillsById.TryGetValue(id, out config);
        public bool TryGetSkillLevelTable(int id, out SkillLevelTableConfig config) => _skillLevelTablesById.TryGetValue(id, out config);
        public bool TryGetAttributeTemplate(int id, out AttributeTemplateConfig config) => _attributeTemplatesById.TryGetValue(id, out config);
        public bool TryGetBuff(int id, out BuffConfig config) => _buffsById.TryGetValue(id, out config);
        public bool TryGetProjectile(int id, out ProjectileConfig config) => _projectilesById.TryGetValue(id, out config);
        public bool TryGetEffect(int id, out EffectConfig config) => _effectsById.TryGetValue(id, out config);

        public int CharacterCount => _charactersById.Count;
        public int SkillCount => _skillsById.Count;
        public int SkillLevelTableCount => _skillLevelTablesById.Count;
        public int AttributeTemplateCount => _attributeTemplatesById.Count;
        public int BuffCount => _buffsById.Count;
        public int ProjectileCount => _projectilesById.Count;
        public int EffectCount => _effectsById.Count;

        public IEnumerable<CharacterConfig> GetAllCharacters() => _charactersById.Values;
        public IEnumerable<SkillConfig> GetAllSkills() => _skillsById.Values;
        public IEnumerable<AttributeTemplateConfig> GetAllAttributeTemplates() => _attributeTemplatesById.Values;
        public IEnumerable<BuffConfig> GetAllBuffs() => _buffsById.Values;
        public IEnumerable<ProjectileConfig> GetAllProjectiles() => _projectilesById.Values;

        /// <summary>
        /// 获取角色属性（通过 AttributeTemplateId）
        /// </summary>
        public AttributeTemplateConfig? GetCharacterAttributes(CharacterConfig character)
        {
            if (character == null) return null;
            return TryGetAttributeTemplate(character.AttributeTemplateId, out var attr) ? attr : null;
        }
    }

    /// <summary>
    /// 角色配置
    /// </summary>
    public sealed class CharacterConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int ModelId { get; set; }
        public int AttributeTemplateId { get; set; }
        public int[] SkillIds { get; set; } = Array.Empty<int>();
        public int[] PassiveSkillIds { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// 技能配置
    /// </summary>
    public sealed class SkillConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CooldownMs { get; set; }
        public float Range { get; set; }
        public int IconId { get; set; }
        public int Category { get; set; }
        public int[] Tags { get; set; } = Array.Empty<int>();
        public int SkillButtonTemplateId { get; set; }
        public int LevelTableId { get; set; }
        public int PreCastFlowId { get; set; }
        public int CastFlowId { get; set; }

        /// <summary>
        /// 冷却秒数
        /// </summary>
        public float CooldownSeconds => CooldownMs / 1000f;
    }

    /// <summary>
    /// 属性模板配置
    /// </summary>
    public sealed class AttributeTemplateConfig
    {
        public int Id { get; set; }
        public int[] ActiveSkills { get; set; } = Array.Empty<int>();
        public int[] PassiveSkills { get; set; } = Array.Empty<int>();
        public float Hp { get; set; }
        public float MaxHp { get; set; }
        public float ExtraHp { get; set; }
        public float PhysicsAttack { get; set; }
        public float MagicAttack { get; set; }
        public float ExtraPhysicsAttack { get; set; }
        public float ExtraMagicAttack { get; set; }
        public float PhysicsDefense { get; set; }
        public float MagicDefense { get; set; }
        public float Mana { get; set; }
        public float MaxMana { get; set; }
        public float CriticalR { get; set; }
        public float AttackSpeedR { get; set; }
        public float CooldownReduceR { get; set; }
        public float PhysicsPenetrationR { get; set; }
        public float MagicPenetrationR { get; set; }
        public float MoveSpeed { get; set; }
        public float PhysicsBloodsuckingR { get; set; }
        public float MagicBloodsuckingR { get; set; }
        public float AttackRange { get; set; }
        public float PerSecondBloodR { get; set; }
        public float PerSecondManaR { get; set; }
        public float ResilienceR { get; set; }
    }

    /// <summary>
    /// Buff 配置
    /// </summary>
    public sealed class BuffConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int DurationMs { get; set; }
        public int OngoingEffectId { get; set; }
        public int[] OnAddEffects { get; set; } = Array.Empty<int>();
        public int[] OnRemoveEffects { get; set; } = Array.Empty<int>();
        public int[] OnIntervalEffects { get; set; } = Array.Empty<int>();
        public int IntervalMs { get; set; }
        public int StackingPolicy { get; set; }
        public int RefreshPolicy { get; set; }
        public int MaxStacks { get; set; }
        public int[] TriggerIds { get; set; } = Array.Empty<int>();
        public int[] Tags { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// 弹道配置
    /// </summary>
    public sealed class ProjectileConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int VfxId { get; set; }
        public float Speed { get; set; }
        public int LifetimeMs { get; set; }
        public float MaxDistance { get; set; }
        public int HitPolicyKind { get; set; }
        public int HitsRemaining { get; set; }
        public int HitCooldownMs { get; set; }
        public int TickIntervalMs { get; set; }
        public int OnHitEffectId { get; set; }
        public int OnSpawnVfxId { get; set; }
        public int OnHitVfxId { get; set; }
        public int OnExpireVfxId { get; set; }
        public int ReturnAfterMs { get; set; }
        public float ReturnSpeed { get; set; }
        public float ReturnStopDistance { get; set; }
    }

    /// <summary>
    /// 技能等级表配置
    /// 包含技能每个等级的参数（冷却、消耗、伤害等）
    /// </summary>
    public sealed class SkillLevelTableConfig
    {
        public int Id { get; set; }
        public List<SkillLevelConfig> Levels { get; set; } = new();
    }

    /// <summary>
    /// 技能单个等级的配置
    /// </summary>
    public sealed class SkillLevelConfig
    {
        public int CooldownMs { get; set; }
        public int Cost { get; set; }
        public List<double> Params { get; set; } = new();

        /// <summary>
        /// 冷却秒数
        /// </summary>
        public float CooldownSeconds => CooldownMs / 1000f;

        /// <summary>
        /// 获取等级参数（用于伤害值等）
        /// </summary>
        public float GetParam(int index, float defaultValue = 0f)
        {
            if (index >= 0 && index < Params.Count)
            {
                return (float)Params[index];
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// 效果配置（逻辑层配置）
    /// 定义技能的伤害、类型等参数
    /// </summary>
    public sealed class EffectConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int EffectType { get; set; } // 1=伤害, 2=治疗, 3=护盾等
        public float BaseDamage { get; set; }
        public int DamageType { get; set; } // 1=物理, 2=魔法, 3=真实
        public float AttackRatio { get; set; } // 攻击力加成系数
        public int TargetPolicy { get; set; } // 0=无目标, 1=单体, 2=区域
        public float Radius { get; set; } // 区域半径（用于区域效果）
    }

    /// <summary>
    /// effect_plans.json 兼容解析类
    /// </summary>
    internal sealed class EffectPlansWrapper
    {
        public List<TriggerPlan> Triggers { get; set; } = new();
    }

    internal sealed class TriggerPlan
    {
        public int TriggerId { get; set; }
        public string EventName { get; set; } = "";
        public int EventId { get; set; }
        public bool AllowExternal { get; set; }
        public int Phase { get; set; }
        public int Priority { get; set; }
        public PredicateDef Predicate { get; set; } = new();
        public List<ActionDef> Actions { get; set; } = new();
    }

    internal sealed class PredicateDef
    {
        public string Kind { get; set; } = "";
        public object Nodes { get; set; }
    }

    internal sealed class ActionDef
    {
        public int ActionId { get; set; }
        public int Arity { get; set; }
        public Dictionary<string, ArgValue> Args { get; set; } = new();
    }

    internal sealed class ArgValue
    {
        public string Kind { get; set; } = "";
        public double ConstValue { get; set; }
    }
}
