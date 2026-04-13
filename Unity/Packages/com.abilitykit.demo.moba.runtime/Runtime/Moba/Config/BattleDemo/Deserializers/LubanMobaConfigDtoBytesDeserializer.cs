using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    /// <summary>
    /// Luban 瀛楄妭鐮佸弽搴忓垪鍖栧櫒锛堝凡寮冪敤锛屾敼鐢?JSON 鏍煎紡锛?
    /// </summary>
    public sealed class LubanMobaConfigDtoBytesDeserializer : IMobaConfigDtoBytesDeserializer
    {
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

        public Array DeserializeDtoArray(byte[] bytes, Type dtoType)
        {
            throw new NotSupportedException(
                "Luban bytes deserialization is no longer supported. " +
                "Please use JSON format (IMobaConfigDtoDeserializer) instead.");
        }

        public Array DeserializeBytes(byte[] bytes, Type targetType)
        {
            throw new NotSupportedException(
                $"[{nameof(LubanMobaConfigDtoBytesDeserializer)}] Bytes deserialization not supported for: {targetType.FullName}");
        }

        public Array DeserializeText(string text, Type targetType)
        {
            throw new NotSupportedException(
                $"[{nameof(LubanMobaConfigDtoBytesDeserializer)}] Text deserialization not supported for: {targetType.FullName}");
        }

        public bool CanHandle(Type targetType)
        {
            return SupportedTypes.Contains(targetType);
        }
    }
}
