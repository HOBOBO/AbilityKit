namespace AbilityKit.Triggering.Collections
{
    /// <summary>
    /// Immutable collection contract shared by execution nodes and project extensions.
    /// </summary>
    public interface IReadOnlyTriggerCollection
    {
        int Count { get; }
        TriggerCollectionValueKind ValueKind { get; }
        string ElementTypeId { get; }
        bool TryGetValue(int index, out TriggerCollectionValue value);
    }
}
