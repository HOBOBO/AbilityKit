#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Keeps a started sample and its environment alive for UI, web, or game-loop hosts.
    /// </summary>
    public sealed class SampleRunHandle : IDisposable
    {
        private bool _disposed;

        public SampleRunHandle(
            SampleCatalogEntry entry,
            ISample? sample,
            ISampleEnvironment environment,
            ILogger output,
            SampleExecutionResult? result = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Sample = sample;
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Result = result;
        }

        /// <summary>Catalog entry that was started.</summary>
        public SampleCatalogEntry Entry { get; }
        /// <summary>Running sample instance, if it was created successfully.</summary>
        public ISample? Sample { get; }
        /// <summary>Host-driven environment.</summary>
        public ISampleEnvironment Environment { get; }
        /// <summary>Output logger.</summary>
        public ILogger Output { get; }
        /// <summary>Immediate execution result, if startup completed or failed.</summary>
        public SampleExecutionResult? Result { get; }

        /// <summary>
        /// Advances the sample environment by one host-provided delta.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SampleRunHandle));

            Environment.Advance(deltaTime);
            Output.Flush();
        }

        /// <summary>
        /// Resets the underlying environment.
        /// </summary>
        public void Reset()
        {
            Environment.Reset();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (Sample is IDisposable sampleDisposable)
                sampleDisposable.Dispose();
            if (Environment is IDisposable envDisposable)
                envDisposable.Dispose();

            Output.Flush();
        }
    }
}
