using System.Collections.Generic;

namespace AbilityKit.Ability.Battle.SkillLibrary
{
    public static class SkillLibraryExample
    {
        private enum ExampleSkillId
        {
            Fireball = 1,
            Heal = 2,
            Dash = 3
        }

        private enum ExampleSkillSchool
        {
            None = 0,
            Fire = 1,
            Holy = 2,
            Movement = 3
        }

        private sealed class ExampleSkillData
        {
            public ExampleSkillSchool School;
            public int CooldownMs;
            public int[] Tags;
        }

        public static void Run()
        {
            var lib = new SkillLibrary<ExampleSkillId, ExampleSkillData>();

            var bySchool = lib.CreateDerivedKeyedIndex<ExampleSkillSchool>(d => d.School);
            var byTag = lib.CreateDerivedMultiKeyIndex<int>(d => d.Tags ?? System.Array.Empty<int>());

            lib.Add(ExampleSkillId.Fireball, new ExampleSkillData { School = ExampleSkillSchool.Fire, CooldownMs = 8000, Tags = new[] { 1, 10 } });
            lib.Add(ExampleSkillId.Heal, new ExampleSkillData { School = ExampleSkillSchool.Holy, CooldownMs = 12000, Tags = new[] { 2, 20 } });
            lib.Add(ExampleSkillId.Dash, new ExampleSkillData { School = ExampleSkillSchool.Movement, CooldownMs = 4000, Tags = new[] { 3, 30 } });

            IReadOnlyCollection<ExampleSkillId> fireSkills = bySchool.Get(ExampleSkillSchool.Fire);
            IReadOnlyCollection<ExampleSkillId> tag10Skills = byTag.Get(10);

            _ = fireSkills;
            _ = tag10Skills;

            lib.Update(ExampleSkillId.Dash,
                new ExampleSkillData { School = ExampleSkillSchool.Movement, CooldownMs = 3500, Tags = new[] { 3, 31 } },
                new SkillUpdate(1, null));
        }
    }
}
