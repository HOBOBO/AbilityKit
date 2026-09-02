namespace UnityHFSM.Inspection
{
    /// <summary>
    /// Marker for runtime state machines that expose the public visitor-based inspection surface.
    /// The marker keeps registry integration independent of concrete generic type arguments.
    /// </summary>
    public interface IStateMachineInspectionSource : IVisitableState
    {
    }
}
