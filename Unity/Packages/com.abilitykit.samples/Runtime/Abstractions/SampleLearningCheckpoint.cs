#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral checkpoint that helps beginners verify what they should understand after a learning step.
    /// </summary>
    public sealed class SampleLearningCheckpoint
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string RelatedVisualStep { get; set; } = string.Empty;
        public string RelatedOutputHint { get; set; } = string.Empty;
        public string[] RelatedApis { get; set; } = Array.Empty<string>();
        public string Question { get; set; } = string.Empty;
        public string ExpectedAnswer { get; set; } = string.Empty;
    }
}
