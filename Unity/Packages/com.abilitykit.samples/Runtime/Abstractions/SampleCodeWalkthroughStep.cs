#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral source walkthrough step that connects sample code with runtime output and guide visuals.
    /// </summary>
    public sealed class SampleCodeWalkthroughStep
    {
        /// <summary>Short title shown by learning hosts.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Workspace-relative source file path.</summary>
        public string SourceFile { get; set; } = string.Empty;

        /// <summary>One-based inclusive start line in the source file.</summary>
        public int StartLine { get; set; }

        /// <summary>One-based inclusive end line in the source file.</summary>
        public int EndLine { get; set; }

        /// <summary>What this source block does in plain language.</summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>Runtime output section or key/value label that this source block produces.</summary>
        public string OutputHint { get; set; } = string.Empty;

        /// <summary>Optional visual step label that this source block maps to.</summary>
        public string VisualStep { get; set; } = string.Empty;
    }
}
