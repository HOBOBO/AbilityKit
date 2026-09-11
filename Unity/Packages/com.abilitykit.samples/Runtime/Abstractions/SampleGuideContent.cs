#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral learning content that helps a sample explain its purpose with text and visual hints.
    /// </summary>
    public sealed class SampleGuideContent
    {
        /// <summary>Short problem statement shown before the sample output.</summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>What the learner should observe when running this sample.</summary>
        public string Observe { get; set; } = string.Empty;

        /// <summary>How the sample connects to real project usage.</summary>
        public string Takeaway { get; set; } = string.Empty;

        /// <summary>Visual layout hint for hosts that can draw diagrams.</summary>
        public string VisualKind { get; set; } = string.Empty;

        /// <summary>Ordered labels used by visual hosts to draw a simple flow, stack, or timeline.</summary>
        public string[] VisualSteps { get; set; } = Array.Empty<string>();
    }
}
