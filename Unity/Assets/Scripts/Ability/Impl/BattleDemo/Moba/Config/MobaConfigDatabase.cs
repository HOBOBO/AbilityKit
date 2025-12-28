using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class MobaConfigDatabase
    {
        private readonly Dictionary<int, CharacterMO> _characters = new Dictionary<int, CharacterMO>();
        private readonly Dictionary<int, SkillMO> _skills = new Dictionary<int, SkillMO>();
        private readonly Dictionary<int, BattleAttributeTemplateMO> _attributes = new Dictionary<int, BattleAttributeTemplateMO>();
        private readonly Dictionary<int, ModelMO> _models = new Dictionary<int, ModelMO>();

        public void Load(IMobaConfigSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Load(source.Load());
        }

        public void Load(MobaConfigSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            _characters.Clear();
            _skills.Clear();
            _attributes.Clear();
            _models.Clear();

            if (snapshot.Characters != null)
            {
                for (var i = 0; i < snapshot.Characters.Length; i++)
                {
                    var dto = snapshot.Characters[i];
                    if (dto == null) continue;
                    _characters[dto.Id] = new CharacterMO(dto);
                }
            }

            if (snapshot.Skills != null)
            {
                for (var i = 0; i < snapshot.Skills.Length; i++)
                {
                    var dto = snapshot.Skills[i];
                    if (dto == null) continue;
                    _skills[dto.Id] = new SkillMO(dto);
                }
            }

            if (snapshot.AttributeTemplates != null)
            {
                for (var i = 0; i < snapshot.AttributeTemplates.Length; i++)
                {
                    var dto = snapshot.AttributeTemplates[i];
                    if (dto == null) continue;
                    _attributes[dto.Id] = new BattleAttributeTemplateMO(dto);
                }
            }

            if (snapshot.Models != null)
            {
                for (var i = 0; i < snapshot.Models.Length; i++)
                {
                    var dto = snapshot.Models[i];
                    if (dto == null) continue;
                    _models[dto.Id] = new ModelMO(dto);
                }
            }
        }

        public CharacterMO GetCharacter(int id)
        {
            return _characters.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Character not found: {id}");
        }

        public SkillMO GetSkill(int id)
        {
            return _skills.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Skill not found: {id}");
        }

        public BattleAttributeTemplateMO GetAttributeTemplate(int id)
        {
            return _attributes.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"AttributeTemplate not found: {id}");
        }

        public ModelMO GetModel(int id)
        {
            return _models.TryGetValue(id, out var v) ? v : throw new KeyNotFoundException($"Model not found: {id}");
        }

        public bool TryGetCharacter(int id, out CharacterMO mo) => _characters.TryGetValue(id, out mo);
        public bool TryGetSkill(int id, out SkillMO mo) => _skills.TryGetValue(id, out mo);
        public bool TryGetAttributeTemplate(int id, out BattleAttributeTemplateMO mo) => _attributes.TryGetValue(id, out mo);
        public bool TryGetModel(int id, out ModelMO mo) => _models.TryGetValue(id, out mo);
    }
}
