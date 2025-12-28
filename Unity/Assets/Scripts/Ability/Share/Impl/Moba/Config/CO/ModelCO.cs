namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO
{
    public interface IModelCO : IMobaConfigObject<int>
    {
        string PrefabPath { get; }
        float Scale { get; }
    }
}
