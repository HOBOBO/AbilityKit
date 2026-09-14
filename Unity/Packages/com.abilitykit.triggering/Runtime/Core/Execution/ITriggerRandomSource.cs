namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// Random source used by executable plans. Deterministic simulations should inject
    /// a source whose state participates in their checkpoint/rollback lifecycle.
    /// </summary>
    public interface ITriggerRandomSource
    {
        float NextFloat01();
    }
}
