#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// A catalog item that can be shown by any sample host.
    /// </summary>
    public sealed class SampleCatalogEntry
    {
        /// <summary>
        /// Creates a catalog entry.
        /// </summary>
        public SampleCatalogEntry(
            int index,
            string id,
            string title,
            string description,
            SampleCategory category,
            Type sampleType,
            Func<ISample> factory,
            int priority = 100,
            string[]? tags = null,
            string? status = null,
            string? level = null,
            string[]? modules = null,
            string[]? next = null,
            SampleGuideContent? guide = null,
            SampleCodeWalkthroughStep[]? codeWalkthrough = null,
            SampleLearningContract? learningContract = null,
            SampleVisualFrame[]? visualFrames = null,
            SampleInputField[]? inputFields = null,
            SampleLearningCheckpoint[]? learningCheckpoints = null,
            string? visualTemplate = null,
            SampleVisualModel? visualModel = null,
            bool isManifestEntry = false)
        {
            Index = index;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Category = category;
            SampleType = sampleType ?? throw new ArgumentNullException(nameof(sampleType));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Priority = priority;
            Tags = tags ?? Array.Empty<string>();
            Status = string.IsNullOrWhiteSpace(status) ? "Draft" : status;
            Level = level ?? string.Empty;
            Modules = modules ?? Array.Empty<string>();
            Next = next ?? Array.Empty<string>();
            Guide = guide ?? new SampleGuideContent();
            CodeWalkthrough = codeWalkthrough ?? Array.Empty<SampleCodeWalkthroughStep>();
            LearningContract = learningContract ?? new SampleLearningContract();
            VisualFrames = visualFrames ?? Array.Empty<SampleVisualFrame>();
            InputFields = inputFields ?? Array.Empty<SampleInputField>();
            LearningCheckpoints = learningCheckpoints ?? Array.Empty<SampleLearningCheckpoint>();
            VisualTemplate = string.IsNullOrWhiteSpace(visualTemplate) ? "timeline" : visualTemplate;
            VisualModel = visualModel ?? new SampleVisualModel();
            IsManifestEntry = isManifestEntry;
        }

        private readonly Func<ISample> _factory;

        /// <summary>Display index.</summary>
        public int Index { get; }
        /// <summary>Stable id for UI selections and persisted preferences.</summary>
        public string Id { get; }
        /// <summary>Display title.</summary>
        public string Title { get; }
        /// <summary>Short description.</summary>
        public string Description { get; }
        /// <summary>Sample category.</summary>
        public SampleCategory Category { get; }
        /// <summary>Concrete sample type.</summary>
        public Type SampleType { get; }
        /// <summary>Sort priority from the sample attribute.</summary>
        public int Priority { get; }
        /// <summary>Optional tags for filtering.</summary>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Lifecycle status from the manifest.</summary>
        public string Status { get; }
        /// <summary>Learning level from the manifest.</summary>
        public string Level { get; }
        /// <summary>Package modules demonstrated by this sample.</summary>
        public IReadOnlyList<string> Modules { get; }
        /// <summary>Suggested next sample ids.</summary>
        public IReadOnlyList<string> Next { get; }
        /// <summary>Optional guide content for text and visual hosts.</summary>
        public SampleGuideContent Guide { get; }
        /// <summary>Optional source walkthrough steps for learning hosts.</summary>
        public IReadOnlyList<SampleCodeWalkthroughStep> CodeWalkthrough { get; }
        /// <summary>Optional formal learning contract for hosts and documentation.</summary>
        public SampleLearningContract LearningContract { get; }
        /// <summary>Optional visual playback frames for animated learning hosts.</summary>
        public IReadOnlyList<SampleVisualFrame> VisualFrames { get; }
        /// <summary>Optional input fields that hosts can render before running the sample.</summary>
        public IReadOnlyList<SampleInputField> InputFields { get; }
        /// <summary>Optional checkpoints that help beginners verify understanding.</summary>
        public IReadOnlyList<SampleLearningCheckpoint> LearningCheckpoints { get; }
        /// <summary>Visual renderer template key used by hosts to choose reusable drawing logic.</summary>
        public string VisualTemplate { get; }
        /// <summary>Semantic visual model consumed by reusable renderer templates.</summary>
        public SampleVisualModel VisualModel { get; }
        /// <summary>Whether this entry came from sample-manifest.json.</summary>
        public bool IsManifestEntry { get; }
 
        /// <summary>
        /// Creates a fresh sample instance.
        /// </summary>
        public ISample CreateSample()
        {
            return _factory();
        }
    }
}
