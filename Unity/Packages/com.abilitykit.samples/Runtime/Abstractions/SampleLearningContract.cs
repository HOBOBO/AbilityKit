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

        /// <summary>Who this sample is written for.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>Sample ids that should be read before this one.</summary>
        public string[] Prerequisites { get; set; } = Array.Empty<string>();

        /// <summary>What the learner is expected to be able to do afterwards.</summary>
        public string[] Outcomes { get; set; } = Array.Empty<string>();

        /// <summary>Common mistakes or shortcuts to avoid.</summary>
        public string[] Pitfalls { get; set; } = Array.Empty<string>();
    }
}
