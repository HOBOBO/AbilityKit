using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Grains.Battle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class StateSyncObserverOptionsTests
{
    [Fact]
    public void Defaults_MatchExistingQueuePolicyAndDrainInterval()
    {
        var options = new StateSyncObserverOptions();
        var settings = StateSyncObserverOptionsMapper.Map(options);
        var expected = SnapshotSendQueuePolicy.Default;

        Assert.Equal(expected.BytesPerSecond, settings.QueuePolicy.BytesPerSecond);
        Assert.Equal(expected.BurstBytes, settings.QueuePolicy.BurstBytes);
        Assert.Equal(expected.MaxQueueLength, settings.QueuePolicy.MaxQueueLength);
        Assert.Equal(expected.MaxQueueAge, settings.QueuePolicy.MaxQueueAge);
        Assert.Equal(TimeSpan.FromMilliseconds(10), settings.DrainInterval);
    }

    [Fact]
    public void ProductionBinding_MapsAllConfiguredValues()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{StateSyncObserverOptions.ConfigurationSection}:BytesPerSecond"] = "24000",
            [$"{StateSyncObserverOptions.ConfigurationSection}:BurstBytes"] = "48000",
            [$"{StateSyncObserverOptions.ConfigurationSection}:MaxQueueLength"] = "7",
            [$"{StateSyncObserverOptions.ConfigurationSection}:MaxQueueAgeMs"] = "750",
            [$"{StateSyncObserverOptions.ConfigurationSection}:DrainIntervalMs"] = "25"
        });

        var options = provider.GetRequiredService<IOptions<StateSyncObserverOptions>>().Value;
        var settings = StateSyncObserverOptionsMapper.Map(options);

        Assert.Equal(24000, settings.QueuePolicy.BytesPerSecond);
        Assert.Equal(48000, settings.QueuePolicy.BurstBytes);
        Assert.Equal(7, settings.QueuePolicy.MaxQueueLength);
        Assert.Equal(TimeSpan.FromMilliseconds(750), settings.QueuePolicy.MaxQueueAge);
        Assert.Equal(TimeSpan.FromMilliseconds(25), settings.DrainInterval);
    }

    [Theory]
    [InlineData("BytesPerSecond", "-1")]
    [InlineData("BurstBytes", "-1")]
    [InlineData("MaxQueueLength", "0")]
    [InlineData("MaxQueueAgeMs", "-1")]
    [InlineData("DrainIntervalMs", "0")]
    public void ProductionBinding_InvalidBoundary_ThrowsOptionsValidationException(string key, string value)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{StateSyncObserverOptions.ConfigurationSection}:{key}"] = value
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<StateSyncObserverOptions>>().Value);

        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void Map_CreatesStableRuntimeSnapshot()
    {
        var options = new StateSyncObserverOptions
        {
            BytesPerSecond = 100,
            BurstBytes = 200,
            MaxQueueLength = 3,
            MaxQueueAgeMs = 400,
            DrainIntervalMs = 20
        };
        var settings = StateSyncObserverOptionsMapper.Map(options);

        options.BytesPerSecond = 999;
        options.BurstBytes = 999;
        options.MaxQueueLength = 99;
        options.MaxQueueAgeMs = 999;
        options.DrainIntervalMs = 99;

        Assert.Equal(100, settings.QueuePolicy.BytesPerSecond);
        Assert.Equal(200, settings.QueuePolicy.BurstBytes);
        Assert.Equal(3, settings.QueuePolicy.MaxQueueLength);
        Assert.Equal(TimeSpan.FromMilliseconds(400), settings.QueuePolicy.MaxQueueAge);
        Assert.Equal(TimeSpan.FromMilliseconds(20), settings.DrainInterval);
    }

    [Fact]
    public void MappedPolicy_MaxQueueLengthControlsQueueAdmission()
    {
        var settings = StateSyncObserverOptionsMapper.Map(new StateSyncObserverOptions
        {
            BytesPerSecond = 0,
            BurstBytes = 0,
            MaxQueueLength = 1,
            MaxQueueAgeMs = 500,
            DrainIntervalMs = 10
        });
        var queue = new SnapshotSendQueue<int>(settings.QueuePolicy, nowTicks: 0);
        var first = CreateCriticalItem(value: 1, frame: 1);
        var second = CreateCriticalItem(value: 2, frame: 2);

        var firstResult = queue.Enqueue(in first, nowTicks: 0);
        var secondResult = queue.Enqueue(in second, nowTicks: 0);

        Assert.Equal(SnapshotDeliveryStatus.Accepted, firstResult.Status);
        Assert.Equal(SnapshotDeliveryStatus.Backpressured, secondResult.Status);
        Assert.Equal(1, queue.Count);
    }

    [Theory]
    [InlineData("ideal")]
    [InlineData("lan")]
    [InlineData("mobile4g")]
    [InlineData("crossregion")]
    [InlineData("poorwifi")]
    public void ResolveQueuePolicy_DoesNotDoubleThrottleNonBandwidthProfiles(string environmentId)
    {
        var configured = StateSyncObserverOptionsMapper.Map(new StateSyncObserverOptions()).QueuePolicy;

        var resolved = StateSyncObserverOptionsMapper.ResolveQueuePolicy(configured, environmentId);

        Assert.Equal(0, resolved.BytesPerSecond);
        Assert.True(resolved.BurstBytes >= configured.BurstBytes);
        Assert.Equal(configured.MaxQueueLength, resolved.MaxQueueLength);
    }

    [Fact]
    public void ResolveQueuePolicy_PreservesLimitedBandwidthBudget()
    {
        var configured = StateSyncObserverOptionsMapper.Map(new StateSyncObserverOptions()).QueuePolicy;

        var resolved = StateSyncObserverOptionsMapper.ResolveQueuePolicy(configured, "limitedbw");

        Assert.Equal(configured.BytesPerSecond, resolved.BytesPerSecond);
        Assert.Equal(configured.BurstBytes, resolved.BurstBytes);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddStateSyncObserverOptions(configuration);
        return services.BuildServiceProvider();
    }

    private static SnapshotSendQueueItem<int> CreateCriticalItem(int value, int frame)
    {
        return new SnapshotSendQueueItem<int>(
            value,
            frame,
            byteCount: 1,
            SnapshotDeliveryPriority.Critical,
            replaceable: false,
            producedAtTicks: 0);
    }
}
