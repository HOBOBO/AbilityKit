using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// Luban 二进制格式配置组反序列化器（已弃用）
    /// </summary>
    public sealed class LubanBinaryConfigGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly LubanBinaryConfigGroupDeserializer Instance = new LubanBinaryConfigGroupDeserializer();

        private LubanBinaryConfigGroupDeserializer() { }

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType)
        {
            throw new NotSupportedException(
                "Luban binary format is no longer supported. Please use JSON format instead.");
        }

        public override Array DeserializeFromText(string text, Type dtoType)
        {
            throw new NotSupportedException(
                "Luban binary format is no longer supported. Please use JSON format instead.");
        }

        public override bool CanHandle(Type dtoType)
        {
            return false;
        }
    }

    /// <summary>
    /// 传统 JSON 格式配置组反序列化器
    /// </summary>
    public sealed class LegacyJsonConfigGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly LegacyJsonConfigGroupDeserializer Instance = new LegacyJsonConfigGroupDeserializer();

        private LegacyJsonConfigGroupDeserializer() { }

        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>
        {
            typeof(BattleDemo.MO.CharacterDTO),
            typeof(BattleDemo.MO.SkillDTO),
            typeof(BattleDemo.MO.SkillButtonTemplateDTO),
            typeof(BattleDemo.MO.TagTemplateDTO),
            typeof(BattleDemo.MO.SearchQueryTemplateDTO),
            typeof(BattleDemo.MO.PassiveSkillDTO),
            typeof(BattleDemo.MO.SkillFlowDTO),
            typeof(BattleDemo.MO.SkillLevelTableDTO),
            typeof(BattleDemo.MO.BattleAttributeTemplateDTO),
            typeof(AttrTypeDTO),
            typeof(BattleDemo.MO.ModelDTO),
            typeof(BattleDemo.MO.BuffDTO),
            typeof(BattleDemo.MO.ProjectileLauncherDTO),
            typeof(BattleDemo.MO.ProjectileDTO),
            typeof(BattleDemo.MO.AoeDTO),
            typeof(BattleDemo.MO.EmitterDTO),
            typeof(BattleDemo.MO.SummonDTO),
            typeof(BattleDemo.MO.SpawnSummonActionTemplateDTO),
            typeof(BattleDemo.MO.ComponentTemplateDTO),
            typeof(BattleDemo.MO.OngoingEffectDTO),
            typeof(BattleDemo.MO.PresentationTemplateDTO),
        };

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType)
        {
            throw CreateNotSupportedException(dtoType, nameof(LegacyJsonConfigGroupDeserializer));
        }

        public override Array DeserializeFromText(string text, Type dtoType)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException(nameof(text));
            if (dtoType == null)
                throw new ArgumentNullException(nameof(dtoType));
            if (!CanHandle(dtoType))
                throw CreateNotSupportedException(dtoType, nameof(LegacyJsonConfigGroupDeserializer));

            return BattleDemo.JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(text, dtoType);
        }

        public override bool CanHandle(Type dtoType)
        {
            return SupportedTypes.Contains(dtoType);
        }
    }
}
