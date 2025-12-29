namespace NBC.ActionEditorExample
{
    public interface ISkillPlay
    {
        //如果要数据和表现层分离
        // ISkill SkillData { get; }
        // void SetSkillData(ISkill skillData);
        
        SkillConfig SkillConfig { get; set; }

        RoleBase Player { get; set; }
        
        void Start();
        void Stop();
    }
}