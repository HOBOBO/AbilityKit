using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Orleans.Grains.Battle;

public sealed class StateSyncObserverOptions
{
    public const string ConfigurationSection = "AbilityKit:StateSyncObserver";

    public int BytesPerSecond { get; set; } = 128 * 1024 / 8;
    public int BurstBytes { get; set; } = 32 * 1024;
    public int MaxQueueLength { get; set; } = 32;
    public int MaxQueueAgeMs { get; set; } = 500;
    public int DrainIntervalMs { get; set; } = 10;
}

internal readonly record struct StateSyncObserverRuntimeSettings(
    SnapshotSendQueuePolicy QueuePolicy,
    TimeSpan DrainInterval);

public static class StateSyncObserverOptionsMapper
{
    public static bool Validate(StateSyncObserverOptions options)
    {
        return GetValidationFailures(options).Count == 0;
    }

    public static IReadOnlyList<string> GetValidationFailures(StateSyncObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (options.BytesPerSecond < 0)
            failures.Add("BytesPerSecond must be greater than or equal to zero.");
        if (options.BurstBytes < 0)
            failures.Add("BurstBytes must be greater than or equal to zero.");
        if (options.MaxQueueLength <= 0)
            failures.Add("MaxQueueLength must be greater than zero.");
        if (options.MaxQueueAgeMs < 0)
            failures.Add("MaxQueueAgeMs must be greater than or equal to zero.");
        if (options.DrainIntervalMs <= 0)
            failures.Add("DrainIntervalMs must be greater than zero.");

        return failures;
    }

    internal static StateSyncObserverRuntimeSettings Map(StateSyncObserverOptions options)
    {
        var failures = GetValidationFailures(options);
        if (failures.Count != 0)
        {
            throw new ArgumentException(
                $"Invalid {nameof(StateSyncObserverOptions)}: {string.Join(" ", failures)}",
                nameof(options));
        }

        return new StateSyncObserverRuntimeSettings(
            new SnapshotSendQueuePolicy(
                options.BytesPerSecond,
                options.BurstBytes,
                options.MaxQueueLength,
                TimeSpan.FromMilliseconds(options.MaxQueueAgeMs)),
            TimeSpan.FromMilliseconds(options.DrainIntervalMs));
    }

    internal static SnapshotSendQueuePolicy ResolveQueuePolicy(
        SnapshotSendQueuePolicy configured,
        string? networkEnvironmentId)
    {
        if (string.IsNullOrWhiteSpace(networkEnvironmentId)
            || string.Equals(networkEnvironmentId, "limitedbw", StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        // Bandwidth conditioning is applied by the selected transport profile.
        // Do not apply the legacy 128 Kbps observer default a second time on
        // ideal/latency-only profiles, otherwise pure-state deltas are dropped
        // before they can reach the client's multi-frame playback buffer.
        return new SnapshotSendQueuePolicy(
            bytesPerSecond: 0,
            burstBytes: Math.Max(configured.BurstBytes, 256 * 1024),
            maxQueueLength: configured.MaxQueueLength,
            maxQueueAge: configured.MaxQueueAge);
    }
}
