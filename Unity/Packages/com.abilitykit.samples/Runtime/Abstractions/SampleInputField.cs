#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Host-neutral input field declaration used by sample hosts to render controls and validate values.
    /// </summary>
    public sealed class SampleInputField
    {
        /// <summary>Stable input key used by hosts and run options.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Display label shown by UI hosts.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Input value type, such as number, text, boolean, or select.</summary>
        public string Type { get; set; } = "text";

        /// <summary>Default value represented as text for host-neutral serialization.</summary>
        public string DefaultValue { get; set; } = string.Empty;

        /// <summary>Optional minimum value for numeric inputs.</summary>
        public string Min { get; set; } = string.Empty;

        /// <summary>Optional maximum value for numeric inputs.</summary>
        public string Max { get; set; } = string.Empty;

        /// <summary>Optional step value for numeric inputs.</summary>
        public string Step { get; set; } = string.Empty;

        /// <summary>Allowed values for select-like inputs.</summary>
        public string[] Options { get; set; } = Array.Empty<string>();

        /// <summary>Short explanation shown near the input control.</summary>
        public string HelpText { get; set; } = string.Empty;

        /// <summary>Whether the value is required before execution.</summary>
        public bool Required { get; set; }
    }
}
