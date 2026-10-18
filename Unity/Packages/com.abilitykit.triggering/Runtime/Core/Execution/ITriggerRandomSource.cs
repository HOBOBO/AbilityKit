namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// Random source used by executable plans. Deterministic simulations should inject
    /// a source whose state can be captured and restored by the caller.
    /// </summary>
    public interface ITriggerRandomSource
    {
        float NextFloat01();
    }
}
