namespace AbilityKit.Ability.World.Services
{
    public interface IWorldLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
