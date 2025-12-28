namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO
{
    public interface IBattleAttributeTemplateCO : IMobaConfigObject<int>
    {
        int MaxHp { get; }
        int Attack { get; }
        int Defense { get; }
        int MoveSpeed { get; }
    }
}
