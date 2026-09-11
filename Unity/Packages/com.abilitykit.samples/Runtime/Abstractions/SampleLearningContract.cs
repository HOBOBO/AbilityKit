#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral learning contract used to explain what a sample teaches and how it should be consumed.
    /// </summary>
    public sealed class SampleLearningContract
    {
        /// <summary>One-sentence learning goal for the sample.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Framework capabilities demonstrated by this sample.</summary>
        public string[] Capabilities { get; set; } = Array.Empty<string>();

        /// <summary>Key public APIs or types worth highlighting.</summary>
        public string[] ApiHighlights { get; set; } = Array.Empty<string>();

        /// <summary>Important concepts the learner should remember.</summary>
        public string[] Concepts { get; set; } = Array.Empty<string>();

        /// <summary>Input or interaction hints that a host can render as controls.</summary>
        public string[] InputHints { get; set; } = Array.Empty<string>();

        /// <summary>Observable output or verification points the sample should surface.</summary>
        public string[] OutputHints { get; set; } = Array.Empty<string>();

        /// <summary>Optional execution hint such as instant, frame-based, or continuous.</summary>
        public string ExecutionHint { get; set; } = string.Empty;
    }
}
