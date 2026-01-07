namespace AbilityKit.Ability.Share.Math
{
    public interface ICollisionService
    {
        ICollisionWorld World { get; }
    }

    public sealed class CollisionService : ICollisionService
    {
        private readonly ICollisionWorld _world = new NaiveCollisionWorld();

        public ICollisionWorld World => _world;
    }
}
