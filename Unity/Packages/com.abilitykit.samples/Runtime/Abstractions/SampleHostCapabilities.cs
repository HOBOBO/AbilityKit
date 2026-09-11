#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Describes the runtime features a concrete sample host can provide.
    /// </summary>
    public sealed class SampleHostCapabilities
    {
        private SampleHostCapabilities(
            bool supportsInteractiveSelection,
            bool supportsInstantRun,
            bool supportsHostDrivenTicks,
            bool supportsRealtimeTicks,
            bool supportsFileOutput,
            bool supportsStructuredOutput,
            bool supportsResourceLoading)
        {
            SupportsInteractiveSelection = supportsInteractiveSelection;
            SupportsInstantRun = supportsInstantRun;
            SupportsHostDrivenTicks = supportsHostDrivenTicks;
            SupportsRealtimeTicks = supportsRealtimeTicks;
            SupportsFileOutput = supportsFileOutput;
            SupportsStructuredOutput = supportsStructuredOutput;
            SupportsResourceLoading = supportsResourceLoading;
        }

        /// <summary>Whether the host can let users choose samples at runtime.</summary>
        public bool SupportsInteractiveSelection { get; }

        /// <summary>Whether the host can execute a sample synchronously in one call.</summary>
        public bool SupportsInstantRun { get; }

        /// <summary>Whether the host can keep a sample handle and drive ticks itself.</summary>
        public bool SupportsHostDrivenTicks { get; }

        /// <summary>Whether the host can advance samples using realtime frame deltas.</summary>
        public bool SupportsRealtimeTicks { get; }

        /// <summary>Whether the host can write sample output to files.</summary>
        public bool SupportsFileOutput { get; }

        /// <summary>Whether the host can consume structured sample output entries.</summary>
        public bool SupportsStructuredOutput { get; }

        /// <summary>Whether the host can provide resource loading services.</summary>
        public bool SupportsResourceLoading { get; }

        /// <summary>
        /// Gets default capabilities for a known host kind.
        /// </summary>
        public static SampleHostCapabilities ForHost(SampleHostKind hostKind)
        {
            return hostKind switch
            {
                SampleHostKind.Console => new SampleHostCapabilities(
                    supportsInteractiveSelection: true,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: false,
                    supportsRealtimeTicks: false,
                    supportsFileOutput: true,
                    supportsStructuredOutput: false,
                    supportsResourceLoading: false),
                SampleHostKind.File => new SampleHostCapabilities(
                    supportsInteractiveSelection: false,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: false,
                    supportsRealtimeTicks: false,
                    supportsFileOutput: true,
                    supportsStructuredOutput: false,
                    supportsResourceLoading: false),
                SampleHostKind.Web => new SampleHostCapabilities(
                    supportsInteractiveSelection: true,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: true,
                    supportsRealtimeTicks: false,
                    supportsFileOutput: false,
                    supportsStructuredOutput: true,
                    supportsResourceLoading: true),
                SampleHostKind.MonoGame => new SampleHostCapabilities(
                    supportsInteractiveSelection: true,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: true,
                    supportsRealtimeTicks: true,
                    supportsFileOutput: false,
                    supportsStructuredOutput: true,
                    supportsResourceLoading: true),
                SampleHostKind.Custom => new SampleHostCapabilities(
                    supportsInteractiveSelection: false,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: false,
                    supportsRealtimeTicks: false,
                    supportsFileOutput: false,
                    supportsStructuredOutput: true,
                    supportsResourceLoading: false),
                _ => new SampleHostCapabilities(
                    supportsInteractiveSelection: false,
                    supportsInstantRun: true,
                    supportsHostDrivenTicks: false,
                    supportsRealtimeTicks: false,
                    supportsFileOutput: false,
                    supportsStructuredOutput: true,
                    supportsResourceLoading: false)
            };
        }
    }
}
