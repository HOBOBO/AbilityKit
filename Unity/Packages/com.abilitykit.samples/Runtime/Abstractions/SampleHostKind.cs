#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Describes the host that is running a sample.
    /// </summary>
    public enum SampleHostKind
    {
        /// <summary>Pure logic host.</summary>
        Logic,
        /// <summary>Console host.</summary>
        Console,
        /// <summary>File-output host.</summary>
        File,
        /// <summary>Web or WebAssembly host.</summary>
        Web,
        /// <summary>MonoGame host.</summary>
        MonoGame,
        /// <summary>Custom host.</summary>
        Custom,
    }
}
