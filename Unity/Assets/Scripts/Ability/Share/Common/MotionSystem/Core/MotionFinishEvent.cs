namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
{
    public enum MotionFinishEvent
    {
        None = 0,
        Arrive = 1,
        Expired = 2,
    }

    public interface IMotionFinishEventSource
    {
        MotionFinishEvent FinishEvent { get; }
    }
}
