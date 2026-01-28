namespace AbilityKit.Triggering.Runtime
{
    public interface ITriggerContextSource<TCtx>
    {
        TCtx GetContext();
    }
}
