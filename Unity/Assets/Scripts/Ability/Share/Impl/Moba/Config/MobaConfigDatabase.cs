using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class MobaConfigDatabase
    {
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();

        public void LoadFromResources(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            var jsonByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            var tables = MobaRuntimeConfigTableRegistry.Tables;
            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var path = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null) throw new InvalidOperationException($"Config json not found in Resources: {path}");
                var json = asset.text;
                if (string.IsNullOrEmpty(json)) throw new InvalidOperationException($"Config json is empty: {path}");
                jsonByKey[path] = json;
                jsonByKey[t.FileWithoutExt] = json;
            }

            LoadFromJsonTexts(jsonByKey, resourcesDir);
        }

        public void LoadFromJsonTexts(IReadOnlyDictionary<string, string> jsonByKey, string resourcesDir = null)
        {
            if (jsonByKey == null) throw new ArgumentNullException(nameof(jsonByKey));

            _tables.Clear();

            var tables = MobaRuntimeConfigTableRegistry.Tables;
            for (var i = 0; i < tables.Length; i++)
            {
                var t = tables[i];
                var fullPath = string.IsNullOrEmpty(resourcesDir) ? t.FileWithoutExt : $"{resourcesDir}/{t.FileWithoutExt}";

                if (!TryGetJson(jsonByKey, fullPath, t.FileWithoutExt, out var json) || string.IsNullOrEmpty(json))
                {
                    throw new InvalidOperationException($"Config json not found: {fullPath}");
                }

                try
                {
                    var dtoArrayType = t.DtoType.MakeArrayType();
                    var arr = (Array)JsonConvert.DeserializeObject(json, dtoArrayType);
                    var tableObj = CreateTableFromDtos(t.DtoType, t.MoType, arr);
                    _tables[t.MoType] = tableObj;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse config json: {fullPath}", ex);
                }
            }
        }

        private static bool TryGetJson(IReadOnlyDictionary<string, string> jsonByKey, string fullPath, string fileWithoutExt, out string json)
        {
            json = null;
            if (jsonByKey == null) return false;
            if (fullPath != null && jsonByKey.TryGetValue(fullPath, out json)) return true;
            if (fileWithoutExt != null && jsonByKey.TryGetValue(fileWithoutExt, out json)) return true;
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

        public bool TryGetCharacter(int id, out CharacterMO mo) => GetTable<CharacterMO>().TryGet(id, out mo);
        public bool TryGetSkill(int id, out SkillMO mo) => GetTable<SkillMO>().TryGet(id, out mo);
        public bool TryGetPassiveSkill(int id, out PassiveSkillMO mo) => GetTable<PassiveSkillMO>().TryGet(id, out mo);
        public bool TryGetSkillFlow(int id, out SkillFlowMO mo) => GetTable<SkillFlowMO>().TryGet(id, out mo);
        public bool TryGetSkillLevelTable(int id, out SkillLevelTableMO mo) => GetTable<SkillLevelTableMO>().TryGet(id, out mo);
        public bool TryGetAttrType(int id, out AttrTypeMO mo) => GetTable<AttrTypeMO>().TryGet(id, out mo);
        public bool TryGetAttributeTemplate(int id, out BattleAttributeTemplateMO mo) => GetTable<BattleAttributeTemplateMO>().TryGet(id, out mo);
        public bool TryGetModel(int id, out ModelMO mo) => GetTable<ModelMO>().TryGet(id, out mo);
        public bool TryGetBuff(int id, out BuffMO mo) => GetTable<BuffMO>().TryGet(id, out mo);
    }
}
