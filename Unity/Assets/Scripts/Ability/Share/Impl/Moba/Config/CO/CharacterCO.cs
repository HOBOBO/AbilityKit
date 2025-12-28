namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO
{
    public interface ICharacterCO : IMobaConfigObject<int>
    {
        string Name { get; }
        int ModelId { get; }
        int AttributeTemplateId { get; }
        int[] SkillIds { get; }
    }
}
