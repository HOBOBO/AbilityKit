using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// Legacy JSON config group deserializer kept for older demo data.
    /// New config domains should use explicit config groups and deserializers.
    /// </summary>
    public sealed class LegacyJsonConfigGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly LegacyJsonConfigGroupDeserializer Instance = new LegacyJsonConfigGroupDeserializer();

        private LegacyJsonConfigGroupDeserializer() { }

        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type>
        {
            typeof(CharacterDTO),
            typeof(SkillDTO),
            typeof(SkillButtonTemplateDTO),
            typeof(TagTemplateDTO),
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

            if (dtoType == typeof(SkillFlowDTO) ||
                dtoType == typeof(ContinuousTagTemplateDTO) ||
                dtoType == typeof(TagTemplateDTO))
            {
                return LubanConfigGroupDeserializer.Instance.DeserializeFromText(text, dtoType);
            }

            return BattleDemo.JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(text, dtoType);
        }

        public override bool CanHandle(Type dtoType)
        {
            return SupportedTypes.Contains(dtoType);
        }
    }
}
