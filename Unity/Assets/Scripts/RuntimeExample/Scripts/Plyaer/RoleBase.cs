using System.Collections.Generic;

namespace NBC.ActionEditorExample
{
    public abstract class RoleBase : MonoManager
    {
        private readonly Dictionary<int, ISkillPlay> _skills = new Dictionary<int, ISkillPlay>();

        public void AddSkill(ISkillPlay skillPlay)
        {
            skillPlay.Player = this;
            _skills[skillPlay.SkillConfig.Id] = skillPlay;
        }

        public void UseSkill(int id)
        {
            if (_skills.TryGetValue(id, out var skillPlay))
            {
                skillPlay.Start();
            }
        }
    }
}