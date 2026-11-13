namespace AbilityKit.Combat.MotionSystem.Collision
{
    /// <summary>Resolves an immutable domain query selection without changing other movers' queries.</summary>
    public interface IMotionCollisionWorldSelector
    {
        IMotionCollisionWorld Select(int selectionKey);
    }
}
