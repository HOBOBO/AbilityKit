using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.HotReload;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using ConfigReloadResult = AbilityKit.Ability.HotReload.ConfigReloadResult;
using ConfigReloadBus = AbilityKit.Ability.HotReload.ConfigReloadBus;
using CharacterMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.CharacterMO;
using BattleAttributeTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.BattleAttributeTemplateMO;
using SkillMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillMO;
using PassiveSkillMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.PassiveSkillMO;
using SkillFlowMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillFlowMO;
using SkillLevelTableMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillLevelTableMO;
using AttrTypeMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.AttrTypeMO;
using ModelMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ModelMO;
using BuffMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.BuffMO;
using ContinuousProcessMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ContinuousProcessMO;
using ProjectileLauncherMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ProjectileLauncherMO;
using ProjectileMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ProjectileMO;
using AoeMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.AoeMO;
using EmitterMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.EmitterMO;
using SummonMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SummonMO;
using SummonAttrInheritMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SummonAttrInheritMO;
using ComponentTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ComponentTemplateMO;
using SkillButtonTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillButtonTemplateMO;
using TagTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.TagTemplateMO;
using ContinuousTagTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ContinuousTagTemplateMO;
using SearchQueryTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SearchQueryTemplateMO;
using BattleMapMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.BattleMapMO;
using GameplayMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.GameplayMO;
using MotionGroupMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.MotionGroupMO;
 
 namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA runtime configuration facade.
    /// Provides project-specific table access while delegating storage and reload mechanics to ConfigDatabase.
    /// </summary>
    public sealed class MobaConfigDatabase
    {
        private const string ReloadConfigKey = "moba.config";

        private readonly ConfigDatabase _innerDb;
        private readonly IMobaConfigTableRegistry _registry;
        private readonly IMobaConfigDtoDeserializer _deserializer;
        private readonly IMobaConfigDtoBytesDeserializer _bytesDeserializer;
        private readonly ITextAssetLoader _textAssetLoader;

        public long Version => _innerDb.Version;

        public MobaConfigDatabase(
            IMobaConfigTableRegistry registry = null,
            IMobaConfigDtoDeserializer deserializer = null,
            IMobaConfigDtoBytesDeserializer bytesDeserializer = null,
            ITextAssetLoader textAssetLoader = null)
        {
            _registry = registry ?? BattleDemo.MobaConfigRegistry.Instance;
            _deserializer = deserializer ?? BattleDemo.JsonNetMobaConfigDtoDeserializer.Instance;
            _bytesDeserializer = bytesDeserializer;
            _textAssetLoader = textAssetLoader ?? NullTextAssetLoader.Instance;

            // Create internal ConfigDatabase with MOBA-specific deserializer adapter
            var adapter = new MobaDeserializerAdapter(_deserializer, _bytesDeserializer);
            _innerDb = new ConfigDatabase(_registry, adapter);
        }

        private ConfigReloadResult PublishSuccess()
        {
            var success = ConfigReloadResult.Success(ReloadConfigKey, _innerDb.Version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        private ConfigReloadResult PublishFailure(string error)
        {
            var fail = ConfigReloadResult.Fail(ReloadConfigKey, _innerDb.Version, error);
            ConfigReloadBus.Publish(fail);
            return fail;
        }

        private ConfigReloadResult PublishReloadResult(AbilityKit.Ability.Config.ConfigReloadResult result)
        {
            return result.Succeeded ? PublishSuccess() : PublishFailure(result.Error);
        }
 
        public void LoadFromTextSink(IConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry, _textAssetLoader);
            loader.Load(this, new ConfigTextSinkAdapter(sink), resourcesDir);
        }

        public ConfigReloadResult ReloadFromTextSink(IConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry, _textAssetLoader);
            return loader.Reload(this, new ConfigTextSinkAdapter(sink), resourcesDir);
        }

        public void LoadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry, _textAssetLoader);
            loader.LoadFromResources(this, resourcesDir);
        }

        public void LoadFromResources(string resourcesDir, bool strict)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry, _textAssetLoader);
            var result = loader.ReloadFromResources(this, resourcesDir, strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public void LoadFromSource(IConfigSource source, string basePath = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = ReloadFromSource(source, basePath, strict: true);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromSource(IConfigSource source, string basePath = null, bool strict = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return PublishReloadResult(_innerDb.Reload(source, basePath, strict));
        }

        public void LoadFromDtoProvider(IMobaConfigDtoProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var result = ReloadFromDtoProvider(provider, strict: true);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromDtoProvider(IMobaConfigDtoProvider provider, bool strict = true)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var dtoArraysByType = new Dictionary<Type, Array>(TypeNameComparer.Instance);
            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                if (!provider.TryGetDtos(definition.DtoType, out var dtos) || dtos == null)
                {
                    if (strict)
                    {
                        return PublishFailure($"DTO provider did not return config: {definition.DtoType.FullName}");
                    }

                    dtos = Array.CreateInstance(definition.DtoType, 0);
                }

                dtoArraysByType[definition.DtoType] = dtos;
            }

            return ReloadFromDtoArrays(dtoArraysByType, strict);
        }

        public void LoadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType)
        {
            if (dtoArraysByType == null) throw new ArgumentNullException(nameof(dtoArraysByType));

            var result = ReloadFromDtoArrays(dtoArraysByType, strict: true);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType, bool strict = true)
        {
            if (dtoArraysByType == null) throw new ArgumentNullException(nameof(dtoArraysByType));

            return PublishReloadResult(_innerDb.ReloadFromDtoArrays(dtoArraysByType, strict));
        }

        public void LoadFromBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string resourcesDir = null)
        {
            if (bytesByKey == null) throw new ArgumentNullException(nameof(bytesByKey));

            var result = ReloadFromBytes(bytesByKey, resourcesDir);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string resourcesDir = null)
        {
            if (bytesByKey == null) throw new ArgumentNullException(nameof(bytesByKey));
            if (_bytesDeserializer == null)
            {
                return PublishFailure("Bytes deserializer not provided. Register IMobaConfigDtoBytesDeserializer into DI or pass it into MobaConfigDatabase ctor.");
            }

            return PublishReloadResult(_innerDb.ReloadFromBytes(bytesByKey, resourcesDir));
        }

        public void LoadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> jsonByKey,
            string bytesResourcesDir,
            string jsonResourcesDir)
        {
            var result = ReloadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir, strict: true);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public void LoadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> jsonByKey,
            string bytesResourcesDir,
            string jsonResourcesDir,
            bool strict)
        {
            var result = ReloadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir, strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> jsonByKey,
            string bytesResourcesDir,
            string jsonResourcesDir)
        {
            return ReloadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir, strict: true);
        }

        public ConfigReloadResult ReloadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> jsonByKey,
            string bytesResourcesDir,
            string jsonResourcesDir,
            bool strict)
        {
            if (bytesByKey == null) throw new ArgumentNullException(nameof(bytesByKey));
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));
            if (_bytesDeserializer == null)
            {
                return PublishFailure("Bytes deserializer not provided.");
            }

            return PublishReloadResult(_innerDb.ReloadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir, strict));
        }

        /// <summary>
        /// Loads configuration from ordered config groups.
        /// </summary>
        /// <param name="groups">Config groups in load order.</param>
        public void LoadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            var result = ReloadFromGroups(groups);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload from groups failed");
            }
        }

        /// <summary>
        /// Reloads configuration from ordered config groups.
        /// </summary>
        /// <param name="groups">Config groups in load order.</param>
        public ConfigReloadResult ReloadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return PublishFailure("No config groups provided");
            }

            return PublishReloadResult(_innerDb.ReloadFromGroups(groups));
        }

        public ConfigReloadResult ReloadFromResources(string resourcesDir)
        {
            return ReloadFromResources(resourcesDir, strict: true);
        }

        public ConfigReloadResult ReloadFromResources(string resourcesDir, bool strict)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry, _textAssetLoader);
            return loader.ReloadFromResources(this, resourcesDir, strict);
        }

        public void LoadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir = null)
        {
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));

            var result = ReloadFromJsonTexts(jsonByKey, resourcesDir);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public void LoadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir, bool strict)
        {
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));

            var result = ReloadFromJsonTexts(jsonByKey, resourcesDir, strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
        }

        public ConfigReloadResult ReloadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir = null)
        {
            return ReloadFromJsonTexts(jsonByKey, resourcesDir, strict: true);
        }

        public ConfigReloadResult ReloadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir, bool strict)
        {
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));

            return PublishReloadResult(_innerDb.ReloadFromTexts(jsonByKey, resourcesDir));
        }

        // ==================== MOBA-specific convenience methods ====================
 
        /// <summary>
        /// Gets a typed config table.
        /// </summary>
        public IConfigTable<TMO> GetTable<TMO>() where TMO : class
        {
            return _innerDb.GetTable<TMO>();
        }

        /// <summary>
        /// Gets a DTO by integer id.
        /// </summary>
        public TDto GetDto<TDto>(int id) where TDto : class
        {
            return _innerDb.GetDto<TDto>(id);
        }

        /// <summary>
        /// Tries to get a DTO by integer id.
        /// </summary>
        public bool TryGetDto<TDto>(int id, out TDto dto) where TDto : class
        {
            return _innerDb.TryGetDto(id, out dto);
        }

        /// <summary>
        /// Gets character config.
        /// </summary>
        public CharacterMO GetCharacter(int id)
        {
            return GetTable<CharacterMO>().Get(id);
        }

        /// <summary>
        /// Gets attribute template config.
        /// </summary>
        public BattleAttributeTemplateMO GetAttributeTemplate(int id)
        {
            return GetTable<BattleAttributeTemplateMO>().Get(id);
        }

        /// <summary>
        /// Tries to get attribute template config.
        /// </summary>
        public bool TryGetAttributeTemplate(int id, out BattleAttributeTemplateMO mo) => GetTable<BattleAttributeTemplateMO>().TryGet(id, out mo);

        public SkillMO GetSkill(int id)
        {
            return GetTable<SkillMO>().Get(id);
        }

        public PassiveSkillMO GetPassiveSkill(int id)
        {
            return GetTable<PassiveSkillMO>().Get(id);
        }

        public SkillFlowMO GetSkillFlow(int id)
        {
            return GetTable<SkillFlowMO>().Get(id);
        }

        public SkillLevelTableMO GetSkillLevelTable(int id)
        {
            return GetTable<SkillLevelTableMO>().Get(id);
        }

        public AttrTypeMO GetAttrType(int id)
        {
            return GetTable<AttrTypeMO>().Get(id);
        }

        public ModelMO GetModel(int id)
        {
            return GetTable<ModelMO>().Get(id);
        }

        public BuffMO GetBuff(int id)
        {
            return GetTable<BuffMO>().Get(id);
        }

        public ContinuousProcessMO GetContinuousProcess(int id)
        {
            return GetTable<ContinuousProcessMO>().Get(id);
        }

        public ProjectileLauncherMO GetProjectileLauncher(int id)
        {
            return GetTable<ProjectileLauncherMO>().Get(id);
        }

        public ProjectileMO GetProjectile(int id)
        {
            return GetTable<ProjectileMO>().Get(id);
        }

        public AoeMO GetAoe(int id)
        {
            return GetTable<AoeMO>().Get(id);
        }

        public EmitterMO GetEmitter(int id)
        {
            return GetTable<EmitterMO>().Get(id);
        }

        public SummonMO GetSummon(int id)
        {
            return GetTable<SummonMO>().Get(id);
        }

        public ComponentTemplateMO GetComponentTemplate(int id)
        {
            return GetTable<ComponentTemplateMO>().Get(id);
        }

        public SkillButtonTemplateMO GetSkillButtonTemplate(int id)
        {
            return GetTable<SkillButtonTemplateMO>().Get(id);
        }

        public TagTemplateMO GetTagTemplate(int id)
        {
            return GetTable<TagTemplateMO>().Get(id);
        }

        public ContinuousTagTemplateMO GetContinuousTagTemplate(int id)
        {
            return GetTable<ContinuousTagTemplateMO>().Get(id);
        }

        public bool TryGetCharacter(int id, out CharacterMO mo) => GetTable<CharacterMO>().TryGet(id, out mo);
        public IEnumerable<CharacterMO> GetAllCharacters() => GetTable<CharacterMO>().All();
        public bool TryGetSkill(int id, out SkillMO mo) => GetTable<SkillMO>().TryGet(id, out mo);
        public IEnumerable<SkillMO> GetAllSkills() => GetTable<SkillMO>().All();
        public bool TryGetPassiveSkill(int id, out PassiveSkillMO mo) => GetTable<PassiveSkillMO>().TryGet(id, out mo);
        public bool TryGetSkillFlow(int id, out SkillFlowMO mo) => GetTable<SkillFlowMO>().TryGet(id, out mo);
        public bool TryGetSkillLevelTable(int id, out SkillLevelTableMO mo) => GetTable<SkillLevelTableMO>().TryGet(id, out mo);
        public bool TryGetAttrType(int id, out AttrTypeMO mo) => GetTable<AttrTypeMO>().TryGet(id, out mo);
        public bool TryGetModel(int id, out ModelMO mo) => GetTable<ModelMO>().TryGet(id, out mo);
        public bool TryGetBuff(int id, out BuffMO mo) => GetTable<BuffMO>().TryGet(id, out mo);
        public bool TryGetContinuousProcess(int id, out ContinuousProcessMO mo) => GetTable<ContinuousProcessMO>().TryGet(id, out mo);
        public bool TryGetSummon(int id, out SummonMO mo) => GetTable<SummonMO>().TryGet(id, out mo);
        public bool TryGetSummonAttrInherit(int id, out SummonAttrInheritMO mo) => GetTable<SummonAttrInheritMO>().TryGet(id, out mo);
        public bool TryGetComponentTemplate(int id, out ComponentTemplateMO mo) => GetTable<ComponentTemplateMO>().TryGet(id, out mo);
        public bool TryGetSkillButtonTemplate(int id, out SkillButtonTemplateMO mo) => GetTable<SkillButtonTemplateMO>().TryGet(id, out mo);
        public bool TryGetTagTemplate(int id, out TagTemplateMO mo) => GetTable<TagTemplateMO>().TryGet(id, out mo);
        public bool TryGetContinuousTagTemplate(int id, out ContinuousTagTemplateMO mo) => GetTable<ContinuousTagTemplateMO>().TryGet(id, out mo);
        public bool TryGetTagTemplateByName(string name, out TagTemplateMO mo)
        {
            mo = null;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var entry in GetTable<TagTemplateMO>().All())
            {
                if (entry != null && name == entry.Name)
                {
                    mo = entry;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetContinuousTagTemplateByName(string name, out ContinuousTagTemplateMO mo)
        {
            mo = null;
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var entry in GetTable<ContinuousTagTemplateMO>().All())
            {
                if (entry != null && name == entry.Name)
                {
                    mo = entry;
                    return true;
                }
            }
            return false;
        }

        public BattleMapMO GetBattleMap(int id) => GetTable<BattleMapMO>().Get(id);
        public bool TryGetBattleMap(int id, out BattleMapMO mo) => GetTable<BattleMapMO>().TryGet(id, out mo);
        public IEnumerable<BattleMapMO> GetAllBattleMaps() => GetTable<BattleMapMO>().All();
        public bool TryGetProjectileLauncher(int id, out ProjectileLauncherMO mo) => GetTable<ProjectileLauncherMO>().TryGet(id, out mo);
        public bool TryGetProjectile(int id, out ProjectileMO mo) => GetTable<ProjectileMO>().TryGet(id, out mo);
        public bool TryGetAoe(int id, out AoeMO mo) => GetTable<AoeMO>().TryGet(id, out mo);
        public bool TryGetEmitter(int id, out EmitterMO mo) => GetTable<EmitterMO>().TryGet(id, out mo);
        public bool TryGetSearchQueryTemplate(int id, out SearchQueryTemplateMO mo) => GetTable<SearchQueryTemplateMO>().TryGet(id, out mo);
        public bool TryGetGameplay(int id, out GameplayMO mo) => GetTable<GameplayMO>().TryGet(id, out mo);
        public MotionGroupMO GetMotionGroup(int id) => GetTable<MotionGroupMO>().Get(id);
        public bool TryGetMotionGroup(int id, out MotionGroupMO mo) => GetTable<MotionGroupMO>().TryGet(id, out mo);
        public IEnumerable<MotionGroupMO> GetAllMotionGroups() => GetTable<MotionGroupMO>().All();
 
        // ==================== Internal adapter for MOBA deserializer ====================

        /// <summary>
        /// Adapts MOBA DTO deserializers to the generic config database deserializer contract.
        /// </summary>
        private sealed class MobaDeserializerAdapter : IConfigDeserializer
        {
            private readonly IMobaConfigDtoDeserializer _jsonDeserializer;
            private readonly IMobaConfigDtoBytesDeserializer _bytesDeserializer;

            public MobaDeserializerAdapter(IMobaConfigDtoDeserializer jsonDeserializer, IMobaConfigDtoBytesDeserializer bytesDeserializer)
            {
                _jsonDeserializer = jsonDeserializer;
                _bytesDeserializer = bytesDeserializer;
            }

            public Array DeserializeBytes(byte[] bytes, Type targetType)
            {
                if (_bytesDeserializer == null)
                {
                    throw new NotSupportedException("Bytes deserializer not configured. Please register IMobaConfigDtoBytesDeserializer.");
                }
                return _bytesDeserializer.DeserializeDtoArray(bytes, targetType);
            }

            public Array DeserializeText(string text, Type targetType)
            {
                return _jsonDeserializer.DeserializeDtoArray(text, targetType);
            }

            public bool CanHandle(Type targetType)
            {
                return _jsonDeserializer.CanHandle(targetType);
            }
        }
    }

    /// <summary>
    /// Strongly typed MOBA config table wrapper.
    /// </summary>
    public sealed class ConfigTable<TMO> where TMO : class
    {
        private readonly IntKeyConfigTable<TMO> _inner = new IntKeyConfigTable<TMO>();

        public int Count => _inner.Count;

        public void Add(int id, TMO entry)
        {
            _inner.Add(id, entry);
        }

        public void AddFromDto(object dto)
        {
            _inner.AddFromDto(dto, d => (TMO)Activator.CreateInstance(typeof(TMO), d));
        }

        public TMO Get(int id)
        {
            return _inner.Get(id);
        }

        public bool TryGet(int id, out TMO mo) => _inner.TryGet(id, out mo);

        public IEnumerable<TMO> All() => _inner.All();
    }
}
