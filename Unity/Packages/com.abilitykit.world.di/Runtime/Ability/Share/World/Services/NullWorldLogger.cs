namespace AbilityKit.Ability.World.Services
{
    public sealed class NullWorldLogger : IWorldLogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
    }
}
