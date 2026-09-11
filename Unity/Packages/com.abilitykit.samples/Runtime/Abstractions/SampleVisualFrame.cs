#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral visual frame used by learning hosts to animate sample behavior from semantic data.
    /// </summary>
    public sealed class SampleVisualFrame
    {
        /// <summary>Stable frame index in the visual playback sequence.</summary>
        public int Index { get; set; }

        /// <summary>Short title shown for this frame.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Semantic guide step or diagram node this frame focuses on.</summary>
        public string VisualStep { get; set; } = string.Empty;

        /// <summary>Plain language explanation of what changed in this frame.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Runtime output label that this visual frame should sync to.</summary>
        public string OutputHint { get; set; } = string.Empty;

        /// <summary>State changes or facts that hosts can render as animated annotations.</summary>
        public string[] StateChanges { get; set; } = Array.Empty<string>();

        /// <summary>Objects, APIs, phases, or entities that should be highlighted.</summary>
        public string[] Highlights { get; set; } = Array.Empty<string>();
    }
}
