using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public interface IMoveCollisionResolver
    {
        Vec3 ResolveDelta(int actorId, in Vec3 from, in Vec3 desiredDelta);
    }

    public sealed class NoCollisionResolver : IMoveCollisionResolver
    {
        public Vec3 ResolveDelta(int actorId, in Vec3 from, in Vec3 desiredDelta) => desiredDelta;
    }
}
