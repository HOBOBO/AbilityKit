namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO
{
    public interface ISkillCO : IMobaConfigObject<int>, ITaggedConfigObject
    {
        string Name { get; }
        int CooldownMs { get; }
        int Range { get; }
        int IconId { get; }
        int Category { get; }
    }
}
