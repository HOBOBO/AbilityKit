using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.HotReload;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class AoeMO
    {
        public int Id { get; }
        public string Name { get; }

        public int ModelId { get; }
        public int VfxId { get; }
        public int AttachMode { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public float Radius { get; }
        public int DelayMs { get; }
        public int CollisionLayerMask { get; }
        public int MaxTargets { get; }

        public int[] OnDelayTriggerIds { get; }

        public AoeMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.AoeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            ModelId = dto.ModelId;
            VfxId = dto.VfxId;
            AttachMode = dto.AttachMode;
            OffsetX = dto.OffsetX;
            OffsetY = dto.OffsetY;
            OffsetZ = dto.OffsetZ;

            Radius = dto.Radius;
            DelayMs = dto.DelayMs;
            CollisionLayerMask = dto.CollisionLayerMask;
            MaxTargets = dto.MaxTargets;
            OnDelayTriggerIds = dto.OnDelayTriggerIds;
        }
    }

    public sealed class EmitterMO
    {
        public int Id { get; }
        public string Name { get; }

        public int EmitKind { get; }
        public int TemplateId { get; }

        public int DelayMs { get; }
        public int DurationMs { get; }
        public int IntervalMs { get; }
        public int TotalCount { get; }

        public int CountPerShot { get; }
        public float FanAngleDeg { get; }

        public int CenterMode { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public EmitterMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.EmitterDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            EmitKind = dto.EmitKind;
            TemplateId = dto.TemplateId;
            DelayMs = dto.DelayMs;
            DurationMs = dto.DurationMs;
            IntervalMs = dto.IntervalMs;
            TotalCount = dto.TotalCount;
            CountPerShot = dto.CountPerShot;
            FanAngleDeg = dto.FanAngleDeg;
            CenterMode = dto.CenterMode;
            OffsetX = dto.OffsetX;
            OffsetY = dto.OffsetY;
            OffsetZ = dto.OffsetZ;
        }
    }
}

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public interface IMobaConfigTextSink
    {
        bool TryGetText(string key, out string text);
    }

    public sealed class DictionaryMobaConfigTextSink : IMobaConfigTextSink
    {
        private readonly IReadOnlyDictionary<string, string> _texts;

        public DictionaryMobaConfigTextSink(IReadOnlyDictionary<string, string> texts)
        {
            _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        }

        public bool TryGetText(string key, out string text)
        {
            text = null;
            if (_texts == null) return false;
            return _texts.TryGetValue(key, out text);
        }
    }

    public sealed class MobaConfigDatabase
    {
        private const string ConfigKey = "moba.config";

        private readonly IMobaConfigTableRegistry _registry;
        private readonly IMobaConfigDtoDeserializer _deserializer;
        private readonly IMobaConfigDtoBytesDeserializer _bytesDeserializer;
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _dtoTables = new Dictionary<Type, object>();
        private long _version;

        public long Version => _version;

        public MobaConfigDatabase(
            IMobaConfigTableRegistry registry = null,
            IMobaConfigDtoDeserializer deserializer = null,
            IMobaConfigDtoBytesDeserializer bytesDeserializer = null)
        {
            _registry = registry ?? DefaultMobaConfigTableRegistry.Instance;
            _deserializer = deserializer ?? JsonNetMobaConfigDtoDeserializer.Instance;
            _bytesDeserializer = bytesDeserializer;
        }

        public void LoadFromTextSink(IMobaConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new DefaultMobaConfigLoader(_registry);
            loader.Load(this, new MobaConfigTextSinkAdapter(sink), resourcesDir);
        }

        public ConfigReloadResult ReloadFromTextSink(IMobaConfigTextSink sink, string resourcesDir = null)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            var loader = new DefaultMobaConfigLoader(_registry);
            return loader.Reload(this, new MobaConfigTextSinkAdapter(sink), resourcesDir);
        }

        public void LoadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new DefaultMobaConfigLoader(_registry);
            loader.LoadFromResources(this, resourcesDir);
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
                var fail = ConfigReloadResult.Fail(ConfigKey, _version, "Bytes deserializer not provided. Register IMobaConfigDtoBytesDeserializer into DI or pass it into MobaConfigDatabase ctor.");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextTables = new Dictionary<Type, object>();
            var nextDtoTables = new Dictionary<Type, object>();

            var tables = _registry.Tables;
            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                if (!TryGetBytes(bytesByKey, fullPath, t.FileWithoutExt, out var bytes) || bytes == null || bytes.Length == 0)
                {
                    var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Config bytes not found: {fullPath}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                try
                {
                    var arr = _bytesDeserializer.DeserializeDtoArray(bytes, t.DtoType);
                    var dtoTableObj = CreateDtoTableFromDtos(t.DtoType, arr);
                    nextDtoTables[t.DtoType] = dtoTableObj;
                    var tableObj = CreateTableFromDtos(t.DtoType, t.MoType, arr);
                    nextTables[t.MoType] = tableObj;
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Failed to parse config bytes: {fullPath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            _tables.Clear();
            foreach (var kv in nextTables)
            {
                _tables[kv.Key] = kv.Value;
            }

            _dtoTables.Clear();
            foreach (var kv in nextDtoTables)
            {
                _dtoTables[kv.Key] = kv.Value;
            }

            _version++;
            var ok = ConfigReloadResult.Success(ConfigKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(ok);
            return ok;
        }

        public ConfigReloadResult ReloadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var loader = new DefaultMobaConfigLoader(_registry);
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

        public ConfigReloadResult ReloadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir = null)
        {
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));

            var nextTables = new Dictionary<Type, object>();
            var nextDtoTables = new Dictionary<Type, object>();

            var tables = _registry.Tables;
            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                if (!TryGetJson(jsonByKey, fullPath, t.FileWithoutExt, out var json) || string.IsNullOrEmpty(json))
                {
                    var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Config json not found: {fullPath}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                try
                {
                    var arr = _deserializer.DeserializeDtoArray(json, t.DtoType);
                    var dtoTableObj = CreateDtoTableFromDtos(t.DtoType, arr);
                    nextDtoTables[t.DtoType] = dtoTableObj;
                    var tableObj = CreateTableFromDtos(t.DtoType, t.MoType, arr);
                    nextTables[t.MoType] = tableObj;
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Failed to parse config json: {fullPath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            _tables.Clear();
            foreach (var kv in nextTables)
            {
                _tables[kv.Key] = kv.Value;
            }

            _dtoTables.Clear();
            foreach (var kv in nextDtoTables)
            {
                _dtoTables[kv.Key] = kv.Value;
            }

            _version++;
            var ok = ConfigReloadResult.Success(ConfigKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(ok);
            return ok;
        }

        private static bool TryGetJson(IReadOnlyDictionary<string, string> jsonByKey, string fullPath, string fileWithoutExt, out string json)
        {
            json = null;
            if (jsonByKey == null) return false;
            if (fullPath != null && jsonByKey.TryGetValue(fullPath, out json)) return true;
            if (fileWithoutExt != null && jsonByKey.TryGetValue(fileWithoutExt, out json)) return true;
            return false;
        }

        private static bool TryGetBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string fullPath, string fileWithoutExt, out byte[] bytes)
        {
            bytes = null;
            if (bytesByKey == null) return false;
            if (fullPath != null && bytesByKey.TryGetValue(fullPath, out bytes)) return true;
            if (fileWithoutExt != null && bytesByKey.TryGetValue(fileWithoutExt, out bytes)) return true;
            return false;
        }


        private static object CreateTableFromDtos(Type dtoType, Type moType, Array dtoArray)
        {
            var tableType = typeof(ConfigTable<>).MakeGenericType(moType);
            var table = Activator.CreateInstance(tableType);
            var addFromDto = tableType.GetMethod("AddFromDto", BindingFlags.Instance | BindingFlags.Public);
            if (addFromDto == null) throw new InvalidOperationException($"AddFromDto not found. tableType={tableType.FullName}");

            if (dtoArray != null)
            {
                for (var i = 0; i < dtoArray.Length; i++)
                {
                    var dto = dtoArray.GetValue(i);
                    if (dto == null) continue;
                    addFromDto.Invoke(table, new[] { dto });
                }
            }

            return table;
        }

        private static object CreateDtoTableFromDtos(Type dtoType, Array dtoArray)
        {
            var tableType = typeof(ConfigDtoTable<>).MakeGenericType(dtoType);
            var table = Activator.CreateInstance(tableType);
            var addFromDto = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            if (addFromDto == null) throw new InvalidOperationException($"Add not found. tableType={tableType.FullName}");

            if (dtoArray != null)
            {
                for (var i = 0; i < dtoArray.Length; i++)
                {
                    var dto = dtoArray.GetValue(i);
                    if (dto == null) continue;
                    addFromDto.Invoke(table, new[] { dto });
                }
            }

            return table;
        }

        private sealed class ConfigDtoTable<TDto>
        {
            private readonly Dictionary<int, TDto> _byId = new Dictionary<int, TDto>();

            public void Add(object dto)
            {
                if (dto == null) return;
                var id = ReadId(dto);
                _byId[id] = (TDto)dto;
            }

            public TDto Get(int id)
            {
                return _byId.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Config not found: type={typeof(TDto).Name} id={id}");
            }

            public bool TryGet(int id, out TDto dto) => _byId.TryGetValue(id, out dto);
        }

        private sealed class ConfigTable<TMO>
        {
            private readonly Dictionary<int, TMO> _byId = new Dictionary<int, TMO>();

            public void AddFromDto(object dto)
            {
                if (dto == null) return;
                var id = ReadId(dto);
                var mo = (TMO)Activator.CreateInstance(typeof(TMO), dto);
                _byId[id] = mo;
            }

            public TMO Get(int id)
            {
                return _byId.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Config not found: type={typeof(TMO).Name} id={id}");
            }

            public bool TryGet(int id, out TMO mo) => _byId.TryGetValue(id, out mo);
        }

        private static int ReadId(object dto)
        {
            var t = dto.GetType();
            var f = t.GetField("Id");
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(dto);
            var p = t.GetProperty("Id");
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(dto);
            throw new InvalidOperationException($"DTO must have int Id field/property. type={t.FullName}");
        }

        private ConfigTable<TMO> GetTable<TMO>()
        {
            if (_tables.TryGetValue(typeof(TMO), out var o) && o is ConfigTable<TMO> t) return t;
            t = new ConfigTable<TMO>();
            _tables[typeof(TMO)] = t;
            return t;
        }

        private ConfigDtoTable<TDto> GetDtoTable<TDto>()
        {
            if (_dtoTables.TryGetValue(typeof(TDto), out var o) && o is ConfigDtoTable<TDto> t) return t;
            t = new ConfigDtoTable<TDto>();
            _dtoTables[typeof(TDto)] = t;
            return t;
        }

        public TDto GetDto<TDto>(int id)
        {
            return GetDtoTable<TDto>().Get(id);
        }

        public bool TryGetDto<TDto>(int id, out TDto dto) => GetDtoTable<TDto>().TryGet(id, out dto);

        public CharacterMO GetCharacter(int id)
        {
            return GetTable<CharacterMO>().Get(id);
        }

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

        public BattleAttributeTemplateMO GetAttributeTemplate(int id)
        {
            return GetTable<BattleAttributeTemplateMO>().Get(id);
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

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.AoeMO GetAoe(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.AoeMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.EmitterMO GetEmitter(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.EmitterMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SummonMO GetSummon(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SummonMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.ComponentTemplateMO GetComponentTemplate(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.ComponentTemplateMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SkillButtonTemplateMO GetSkillButtonTemplate(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SkillButtonTemplateMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.TagTemplateMO GetTagTemplate(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.TagTemplateMO>().Get(id);
        }

        public global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.OngoingEffectMO GetOngoingEffect(int id)
        {
            return GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.OngoingEffectMO>().Get(id);
        }

        public bool TryGetCharacter(int id, out CharacterMO mo) => GetTable<CharacterMO>().TryGet(id, out mo);
        public bool TryGetSkill(int id, out SkillMO mo) => GetTable<SkillMO>().TryGet(id, out mo);
        public bool TryGetPassiveSkill(int id, out PassiveSkillMO mo) => GetTable<PassiveSkillMO>().TryGet(id, out mo);
        public bool TryGetSkillFlow(int id, out SkillFlowMO mo) => GetTable<SkillFlowMO>().TryGet(id, out mo);
        public bool TryGetSkillLevelTable(int id, out SkillLevelTableMO mo) => GetTable<SkillLevelTableMO>().TryGet(id, out mo);
        public bool TryGetAttrType(int id, out AttrTypeMO mo) => GetTable<AttrTypeMO>().TryGet(id, out mo);
        public bool TryGetAttributeTemplate(int id, out BattleAttributeTemplateMO mo) => GetTable<BattleAttributeTemplateMO>().TryGet(id, out mo);
        public bool TryGetModel(int id, out ModelMO mo) => GetTable<ModelMO>().TryGet(id, out mo);
        public bool TryGetBuff(int id, out BuffMO mo) => GetTable<BuffMO>().TryGet(id, out mo);
        public bool TryGetSummon(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SummonMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SummonMO>().TryGet(id, out mo);
        public bool TryGetComponentTemplate(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.ComponentTemplateMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.ComponentTemplateMO>().TryGet(id, out mo);
        public bool TryGetSkillButtonTemplate(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SkillButtonTemplateMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.SkillButtonTemplateMO>().TryGet(id, out mo);
        public bool TryGetTagTemplate(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.TagTemplateMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.TagTemplateMO>().TryGet(id, out mo);
        public bool TryGetOngoingEffect(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.OngoingEffectMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.OngoingEffectMO>().TryGet(id, out mo);
        public bool TryGetProjectileLauncher(int id, out ProjectileLauncherMO mo) => GetTable<ProjectileLauncherMO>().TryGet(id, out mo);
        public bool TryGetProjectile(int id, out ProjectileMO mo) => GetTable<ProjectileMO>().TryGet(id, out mo);
        public bool TryGetAoe(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.AoeMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.AoeMO>().TryGet(id, out mo);
        public bool TryGetEmitter(int id, out global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.EmitterMO mo) => GetTable<global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.EmitterMO>().TryGet(id, out mo);
    }
}
