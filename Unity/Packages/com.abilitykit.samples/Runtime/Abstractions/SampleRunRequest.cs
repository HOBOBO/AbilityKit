#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral request used to run samples from CLI, UI, Web, Unity, MonoGame, or custom hosts.
    /// </summary>
    public sealed class SampleRunRequest
    {
        private SampleRunRequest(SampleRunSelectionKind selectionKind, int? index, string id, SampleRunOptions? options)
        {
            SelectionKind = selectionKind;
            Index = index;
            Id = id ?? string.Empty;
            Options = options ?? new SampleRunOptions();
        }

        /// <summary>How the host selected the sample or samples.</summary>
        public SampleRunSelectionKind SelectionKind { get; }

        /// <summary>Selected display index when <see cref="SelectionKind"/> is <see cref="SampleRunSelectionKind.Index"/>.</summary>
        public int? Index { get; }

        /// <summary>Selected stable id when <see cref="SelectionKind"/> is <see cref="SampleRunSelectionKind.Id"/>.</summary>
        public string Id { get; }

        /// <summary>Run options supplied by the host.</summary>
        public SampleRunOptions Options { get; }

        /// <summary>Creates a request that runs one sample by display index.</summary>
        public static SampleRunRequest ByIndex(int index, SampleRunOptions? options = null)
        {
            return new SampleRunRequest(SampleRunSelectionKind.Index, index, string.Empty, options);
        }

        /// <summary>Creates a request that runs one sample by stable id.</summary>
        public static SampleRunRequest ById(string id, SampleRunOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Sample id cannot be empty.", nameof(id));

            return new SampleRunRequest(SampleRunSelectionKind.Id, null, id, options);
        }

        /// <summary>Creates a request that runs every sample in the current catalog.</summary>
        public static SampleRunRequest All(SampleRunOptions? options = null)
        {
            return new SampleRunRequest(SampleRunSelectionKind.All, null, string.Empty, options);
        }
    }
}
