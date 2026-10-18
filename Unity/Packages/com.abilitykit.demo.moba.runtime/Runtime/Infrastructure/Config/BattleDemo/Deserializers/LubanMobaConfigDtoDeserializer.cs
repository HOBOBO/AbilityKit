using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    /// <summary>
    /// 已废弃的 Luban 文本反序列化器；运行时配置统一使用 JSON DTO 反序列化。
    /// </summary>
    public sealed class LubanMobaConfigDtoDeserializer : IMobaConfigDtoDeserializer
    {
        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>
        {
            typeof(CharacterDTO),
            typeof(SkillDTO),
            typeof(SkillButtonTemplateDTO),
            typeof(ContinuousTagTemplateDTO),
            typeof(SearchQueryTemplateDTO),
            typeof(PassiveSkillDTO),
            typeof(SkillFlowDTO),
            typeof(SkillLevelTableDTO),
            typeof(BattleAttributeTemplateDTO),
            typeof(AttrTypeDTO),
            typeof(ModelDTO),
            typeof(BuffDTO),
            typeof(ProjectileLauncherDTO),
            typeof(ProjectileDTO),
            typeof(AoeDTO),
            typeof(EmitterDTO),
            typeof(SummonDTO),
            typeof(SpawnSummonActionTemplateDTO),
            typeof(ComponentTemplateDTO),
            typeof(PresentationTemplateDTO),
            typeof(GameplayDTO),
            typeof(DungeonDTO),
        };

        public Array DeserializeDtoArray(string text, Type dtoType)
        {
            throw new NotSupportedException(
                "Luban bytes deserialization is no longer supported. " +
                "Please use JSON format (IMobaConfigDtoDeserializer) instead.");
        }

        public Array DeserializeBytes(byte[] bytes, Type targetType)
        {
            throw new NotSupportedException(
                $"[{nameof(LubanMobaConfigDtoDeserializer)}] Bytes deserialization not supported for: {targetType.FullName}");
        }

        public Array DeserializeText(string text, Type targetType)
        {
            throw new NotSupportedException(
                $"[{nameof(LubanMobaConfigDtoDeserializer)}] Text deserialization not supported for: {targetType.FullName}");
        }

        public bool CanHandle(Type targetType)
        {
            return SupportedTypes.Contains(targetType);
        }
    }
}
