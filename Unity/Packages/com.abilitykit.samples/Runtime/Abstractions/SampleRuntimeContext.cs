#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Runtime services supplied by a concrete sample host.
    /// </summary>
    public sealed class SampleRuntimeContext
    {
        public SampleRuntimeContext(
            ILogger output,
            ISampleEnvironment environment,
            SampleHostKind hostKind = SampleHostKind.Logic,
            IConfigProvider? config = null,
            IResourceProvider? resources = null,
            string? outputDirectory = null,
            SampleHostCapabilities? hostCapabilities = null,
            IReadOnlyDictionary<string, string>? inputs = null)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            HostKind = hostKind;
            HostCapabilities = hostCapabilities ?? SampleHostCapabilities.ForHost(hostKind);
            Config = config;
            Resources = resources;
            OutputDirectory = outputDirectory ?? string.Empty;
            Inputs = inputs ?? new Dictionary<string, string>();
        }

        public ILogger Output { get; }
        public ISampleEnvironment Environment { get; }
        public SampleHostKind HostKind { get; }
        public SampleHostCapabilities HostCapabilities { get; }
        public IConfigProvider? Config { get; }
        public IResourceProvider? Resources { get; }
        public string OutputDirectory { get; }
        public IReadOnlyDictionary<string, string> Inputs { get; }
    }
}
