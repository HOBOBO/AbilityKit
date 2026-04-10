using AbilityKit.Ability.World.Services;

namespace AbilityKit.Core.Math
{
    public sealed class CollisionService : ICollisionService, IService
    {
        private readonly ICollisionWorld _world = new NaiveCollisionWorld();

        public ICollisionWorld World => _world;

        public void Dispose()
        {
        }
    }
}
