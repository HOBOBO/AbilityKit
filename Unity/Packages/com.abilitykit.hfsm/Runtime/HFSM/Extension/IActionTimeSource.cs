namespace UnityHFSM.Extension
{
    public interface IActionTimeSource
    {
        float DeltaTime { get; }
        float UnscaledDeltaTime { get; }
    }
}
