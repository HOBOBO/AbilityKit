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
using ProjectileLauncherMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ProjectileLauncherMO;
using ProjectileMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ProjectileMO;
using AoeMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.AoeMO;
using EmitterMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.EmitterMO;
using SummonMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SummonMO;
using ComponentTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.ComponentTemplateMO;
using SkillButtonTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillButtonTemplateMO;
using TagTemplateMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.TagTemplateMO;
using OngoingEffectMO = AbilityKit.Demo.Moba.Config.BattleDemo.MO.OngoingEffectMO;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 閰嶇疆鏁版嵁搴撳疄鐜?
    /// 鎻愪緵渚挎嵎鐨?MOBA 鐗瑰畾閰嶇疆璁块棶鏂规硶
    /// 鍐呴儴浣跨敤閫氱敤鐨?ConfigDatabase 杩涜閰嶇疆绠＄悊
    /// </summary>
    public sealed class MobaConfigDatabase
    {
        private readonly ConfigDatabase _innerDb;
        private readonly IMobaConfigTableRegistry _registry;
        private readonly IMobaConfigDtoDeserializer _deserializer;
        private readonly IMobaConfigDtoBytesDeserializer _bytesDeserializer;

        public long Version => _innerDb.Version;

        public MobaConfigDatabase(
            IMobaConfigTableRegistry registry = null,
            IMobaConfigDtoDeserializer deserializer = null,
            IMobaConfigDtoBytesDeserializer bytesDeserializer = null)
        {
            _registry = registry ?? BattleDemo.MobaConfigRegistry.Instance;
            _deserializer = deserializer ?? BattleDemo.JsonNetMobaConfigDtoDeserializer.Instance;
            _bytesDeserializer = bytesDeserializer;

            // Create internal ConfigDatabase with MOBA-specific deserializer adapter
            var adapter = new MobaDeserializerAdapter(_deserializer, _bytesDeserializer);
            _innerDb = new ConfigDatabase(_registry, adapter);
        }

        public void LoadFromTextSink(IConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry);
            loader.Load(this, new ConfigTextSinkAdapter(sink), resourcesDir);
        }

        public ConfigReloadResult ReloadFromTextSink(IConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry);
            return loader.Reload(this, new ConfigTextSinkAdapter(sink), resourcesDir);
        }

        public void LoadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry);
            loader.LoadFromResources(this, resourcesDir);
        }

        public void LoadFromResources(string resourcesDir, bool strict)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry);
            var result = loader.ReloadFromResources(this, resourcesDir, strict);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload failed");
            }
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
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, "Bytes deserializer not provided. Register IMobaConfigDtoBytesDeserializer into DI or pass it into MobaConfigDatabase ctor.");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            // Use internal ConfigDatabase for bytes loading
            var result = _innerDb.ReloadFromBytes(bytesByKey, resourcesDir);
            if (!result.Succeeded)
            {
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, result.Error);
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

            var success = ConfigReloadResult.Success("moba.config", _innerDb.Version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
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
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, "Bytes deserializer not provided.");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            // Use internal ConfigDatabase for mixed loading
            var result = _innerDb.ReloadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir, strict);
            if (!result.Succeeded)
            {
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, result.Error);
                            ConfigReloadBus.Publish(fail);
                            return fail;
                        }

            var success = ConfigReloadResult.Success("moba.config", _innerDb.Version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        /// <summary>
        /// 浠庨厤缃粍鍔犺浇閰嶇疆
        /// </summary>
        /// <param name="groups">閰嶇疆缁勫垪琛紝鎸夐『搴忓鐞?/param>
        public void LoadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            var result = ReloadFromGroups(groups);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.Error ?? "Config reload from groups failed");
            }
        }

        /// <summary>
        /// 浠庨厤缃粍閲嶆柊鍔犺浇閰嶇疆
        /// </summary>
        /// <param name="groups">閰嶇疆缁勫垪琛紝鎸夐『搴忓鐞?/param>
        public ConfigReloadResult ReloadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, "No config groups provided");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            // Use internal ConfigDatabase for groups loading
            var result = _innerDb.ReloadFromGroups(groups);
            if (!result.Succeeded)
            {
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, result.Error);
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

            var success = ConfigReloadResult.Success("moba.config", _innerDb.Version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        public ConfigReloadResult ReloadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new BattleDemo.DefaultMobaConfigLoader(_registry);
            return loader.ReloadFromResources(this, resourcesDir);
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

            // Use internal ConfigDatabase for JSON texts loading
            var result = _innerDb.ReloadFromTexts(jsonByKey, resourcesDir);
            if (!result.Succeeded)
            {
                var fail = ConfigReloadResult.Fail("moba.config", _innerDb.Version, result.Error);
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

            var success = ConfigReloadResult.Success("moba.config", _innerDb.Version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        // ==================== MOBA-specific convenience methods ====================

        /// <summary>
        /// 鑾峰彇閰嶇疆琛?
        /// </summary>
        public IConfigTable<TMO> GetTable<TMO>() where TMO : class
        {
            return _innerDb.GetTable<TMO>();
        }

        /// <summary>
        /// 鑾峰彇 DTO
        /// </summary>
        public TDto GetDto<TDto>(int id) where TDto : class
        {
            return _innerDb.GetDto<TDto>(id);
        }

        /// <summary>
        /// 灏濊瘯鑾峰彇 DTO
        /// </summary>
        public bool TryGetDto<TDto>(int id, out TDto dto) where TDto : class
        {
            return _innerDb.TryGetDto(id, out dto);
        }

        /// <summary>
        /// 鑾峰彇瑙掕壊閰嶇疆
        /// </summary>
        public CharacterMO GetCharacter(int id)
        {
            return GetTable<CharacterMO>().Get(id);
        }

        /// <summary>
        /// 鑾峰彇灞炴€фā鏉块厤缃?
        /// </summary>
        public BattleAttributeTemplateMO GetAttributeTemplate(int id)
        {
            return GetTable<BattleAttributeTemplateMO>().Get(id);
        }

        /// <summary>
        /// 灏濊瘯鑾峰彇灞炴€фā鏉块厤缃?
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

        public OngoingEffectMO GetOngoingEffect(int id)
        {
            return GetTable<OngoingEffectMO>().Get(id);
        }

        public bool TryGetCharacter(int id, out CharacterMO mo) => GetTable<CharacterMO>().TryGet(id, out mo);
        public bool TryGetSkill(int id, out SkillMO mo) => GetTable<SkillMO>().TryGet(id, out mo);
        public bool TryGetPassiveSkill(int id, out PassiveSkillMO mo) => GetTable<PassiveSkillMO>().TryGet(id, out mo);
        public bool TryGetSkillFlow(int id, out SkillFlowMO mo) => GetTable<SkillFlowMO>().TryGet(id, out mo);
        public bool TryGetSkillLevelTable(int id, out SkillLevelTableMO mo) => GetTable<SkillLevelTableMO>().TryGet(id, out mo);
        public bool TryGetAttrType(int id, out AttrTypeMO mo) => GetTable<AttrTypeMO>().TryGet(id, out mo);
        public bool TryGetModel(int id, out ModelMO mo) => GetTable<ModelMO>().TryGet(id, out mo);
        public bool TryGetBuff(int id, out BuffMO mo) => GetTable<BuffMO>().TryGet(id, out mo);
        public bool TryGetSummon(int id, out SummonMO mo) => GetTable<SummonMO>().TryGet(id, out mo);
        public bool TryGetComponentTemplate(int id, out ComponentTemplateMO mo) => GetTable<ComponentTemplateMO>().TryGet(id, out mo);
        public bool TryGetSkillButtonTemplate(int id, out SkillButtonTemplateMO mo) => GetTable<SkillButtonTemplateMO>().TryGet(id, out mo);
        public bool TryGetTagTemplate(int id, out TagTemplateMO mo) => GetTable<TagTemplateMO>().TryGet(id, out mo);
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
        public bool TryGetOngoingEffect(int id, out OngoingEffectMO mo) => GetTable<OngoingEffectMO>().TryGet(id, out mo);
        public bool TryGetProjectileLauncher(int id, out ProjectileLauncherMO mo) => GetTable<ProjectileLauncherMO>().TryGet(id, out mo);
        public bool TryGetProjectile(int id, out ProjectileMO mo) => GetTable<ProjectileMO>().TryGet(id, out mo);
        public bool TryGetAoe(int id, out AoeMO mo) => GetTable<AoeMO>().TryGet(id, out mo);
        public bool TryGetEmitter(int id, out EmitterMO mo) => GetTable<EmitterMO>().TryGet(id, out mo);

        // ==================== Internal adapter for MOBA deserializer ====================

        /// <summary>
        /// 灏?IMobaConfigDtoDeserializer 閫傞厤涓?IConfigDeserializer
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
    /// MOBA 閰嶇疆琛紝鎻愪緵寮虹被鍨嬬殑閰嶇疆璁块棶
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
    }
}
