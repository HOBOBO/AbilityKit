#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral semantic visual model consumed by reusable render templates.
    /// </summary>
    public sealed class SampleVisualModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SampleVisualNode[] Nodes { get; set; } = Array.Empty<SampleVisualNode>();
        public SampleVisualEdge[] Edges { get; set; } = Array.Empty<SampleVisualEdge>();
        public SampleVisualMetric[] Metrics { get; set; } = Array.Empty<SampleVisualMetric>();
    }

    /// <summary>
    /// Semantic node rendered by a visual template.
    /// </summary>
    public sealed class SampleVisualNode
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Semantic relationship between visual nodes.
    /// </summary>
    public sealed class SampleVisualEdge
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Numeric or textual state displayed by a visual template.
    /// </summary>
    public sealed class SampleVisualMetric
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }
}
