using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo
{
    public sealed class JsonNetMobaConfigDtoDeserializer : IMobaConfigDtoDeserializer
    {
        public static readonly JsonNetMobaConfigDtoDeserializer Instance = new JsonNetMobaConfigDtoDeserializer();

        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>
        {
            typeof(MO.CharacterDTO),
            typeof(MO.SkillDTO),
            typeof(MO.SkillButtonTemplateDTO),
            typeof(MO.TagTemplateDTO),
            typeof(MO.SearchQueryTemplateDTO),
            typeof(MO.PassiveSkillDTO),
            typeof(MO.SkillFlowDTO),
            typeof(MO.SkillLevelTableDTO),
            typeof(MO.BattleAttributeTemplateDTO),
            typeof(AttrTypeDTO),
            typeof(MO.ModelDTO),
            typeof(MO.BuffDTO),
            typeof(MO.ProjectileLauncherDTO),
            typeof(MO.ProjectileDTO),
            typeof(MO.AoeDTO),
            typeof(MO.EmitterDTO),
            typeof(MO.SummonDTO),
            typeof(MO.SpawnSummonActionTemplateDTO),
            typeof(MO.ComponentTemplateDTO),
            typeof(MO.OngoingEffectDTO),
            typeof(MO.PresentationTemplateDTO),
        };

        private JsonNetMobaConfigDtoDeserializer() { }

        /// <summary>
        /// MOBA 专用：从 JSON 文本反序列化 DTO 数组
        /// </summary>
        public Array DeserializeDtoArray(string text, Type dtoType)
        {
            if (dtoType == null) throw new ArgumentNullException(nameof(dtoType));
            if (string.IsNullOrEmpty(text)) return Array.CreateInstance(dtoType, 0);

            var token = JToken.Parse(text);
            if (token is not JArray array) return Array.CreateInstance(dtoType, 0);

            var list = new List<object>();
            foreach (var item in array)
            {
                var obj = item.ToObject(dtoType);
                if (obj != null)
                {
                    list.Add(obj);
                }
            }

            var result = Array.CreateInstance(dtoType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                result.SetValue(list[i], i);
            }
            return result;
        }

        /// <summary>
        /// 通用 IConfigDeserializer 实现
        /// </summary>
        public Array DeserializeBytes(byte[] bytes, Type targetType)
        {
            throw new NotSupportedException(
                $"[{nameof(JsonNetMobaConfigDtoDeserializer)}] Bytes deserialization not supported for: {targetType.FullName}");
        }

        public Array DeserializeText(string text, Type targetType)
        {
            if (string.IsNullOrEmpty(text)) return Array.CreateInstance(targetType, 0);
            return DeserializeDtoArray(text, targetType);
        }

        public bool CanHandle(Type targetType)
        {
            return SupportedTypes.Contains(targetType);
        }
    }
}
