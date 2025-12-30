using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Move
{
    public interface IMoveTask
    {
        int Priority { get; }
        MobaMoveGroup Group { get; }
        MobaMoveKind Kind { get; }
        MobaMoveStacking Stacking { get; }

        bool IsFinished { get; }

        Vec3 Tick(float dt);

        void Cancel();

        bool TryMergeFrom(IMoveTask other);
    }
}
