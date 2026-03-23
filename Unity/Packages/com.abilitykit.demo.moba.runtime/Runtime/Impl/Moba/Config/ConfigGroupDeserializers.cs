using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    /// <summary>
    /// Luban 二进制格式配置组反序列化器
    /// </summary>
    public sealed class LubanBinaryConfigGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly LubanBinaryConfigGroupDeserializer Instance = new LubanBinaryConfigGroupDeserializer();

        private LubanBinaryConfigGroupDeserializer() { }

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentNullException(nameof(bytes));

            if (dtoType == null)
                throw new ArgumentNullException(nameof(dtoType));

            if (dtoType == typeof(global::cfg.DRBuff))
            {
                var buf = global::Luban.ByteBuf.Wrap(bytes);
                var table = new global::cfg.Buffs(buf);
                return table.DataList.ToArray();
            }

            throw CreateNotSupportedException(dtoType, nameof(LubanBinaryConfigGroupDeserializer));
        }

        public override Array DeserializeFromText(string text, Type dtoType)
        {
            throw CreateNotSupportedException(dtoType, nameof(LubanBinaryConfigGroupDeserializer));
        }

        public override bool CanHandle(Type dtoType)
        {
            return dtoType == typeof(global::cfg.DRBuff);
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
            typeof(CharacterDTO),
            typeof(SkillDTO),
            typeof(SkillButtonTemplateDTO),
            typeof(TagTemplateDTO),
            typeof(SearchQueryTemplateDTO),
            typeof(PassiveSkillDTO),
            typeof(SkillFlowDTO),
            typeof(BattleAttributeTemplateDTO),
            typeof(ModelDTO),
            typeof(BuffDTO),
            typeof(ProjectileLauncherDTO),
            typeof(ProjectileDTO),
            typeof(AoeDTO),
            typeof(EmitterDTO),
            typeof(SummonDTO),
            typeof(SpawnSummonActionTemplateDTO),
            typeof(ComponentTemplateDTO),
            typeof(OngoingEffectDTO),
            typeof(PresentationTemplateDTO),
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

            var arr = JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(text, dtoType);
            return arr;
        }

        public override bool CanHandle(Type dtoType)
        {
            return SupportedTypes.Contains(dtoType);
        }
    }
}
