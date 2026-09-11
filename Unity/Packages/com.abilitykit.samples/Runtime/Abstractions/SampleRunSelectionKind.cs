#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Describes how a host selects samples to run.
    /// </summary>
    public enum SampleRunSelectionKind
    {
        /// <summary>No sample selected.</summary>
        None,
        /// <summary>Run one sample by display index.</summary>
        Index,
        /// <summary>Run one sample by stable manifest id.</summary>
        Id,
        /// <summary>Run all samples in the current catalog.</summary>
        All
    }
}
