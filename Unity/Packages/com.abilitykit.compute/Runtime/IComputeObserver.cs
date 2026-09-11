namespace AbilityKit.Compute
{
    public interface IComputeObserver
    {
        void OnAttempt(string operationId, int itemCount, in ComputeAttempt attempt);
    }
}
