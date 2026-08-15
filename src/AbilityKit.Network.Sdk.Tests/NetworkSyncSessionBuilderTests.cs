using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSyncSessionBuilderTests
{
    [Fact]
    public void Build_ResolvesStableNameAndReturnsNegotiatedDescriptor()
    {
        var registry = CreateRegistry(NetworkSyncProfiles.PredictRollback, (in int _) => "controller");
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 1, 3);
        options.RequiredProfileName = nameof(NetworkSyncModel.PredictRollback);

        var result = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(7);

        Assert.Equal("controller", result.Controller);
        Assert.Equal(nameof(NetworkSyncModel.PredictRollback), result.Descriptor.ProfileName);
        Assert.Equal(NetworkSyncProfiles.PredictRollback, result.Descriptor.Profile);
        Assert.Equal(1, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(3, result.Descriptor.MaximumSchemaVersion);
        Assert.True(result.Descriptor.ConfigurationReport.IsValid);
    }

    [Fact]
    public void Build_UsesCustomCatalogAndDirectProfile()
    {
        var profile = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FixedRateStateStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation);
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("Sample.RemoteInterpolation", profile);
        var registry = CreateRegistry(profile, (in int value) => value.ToString());
        var options = CreateOptions(profile, 1, 2);
        options.ProfileCatalog = catalog;
        options.RequiredProfileName = "Sample.RemoteInterpolation";

        var named = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(9);
        options.RequiredProfile = profile;
        options.RequiredProfileName = "Sample.DirectProfile";
        var direct = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(10);

        Assert.Equal("9", named.Controller);
        Assert.Equal("Sample.RemoteInterpolation", named.Descriptor.ProfileName);
        Assert.Equal("10", direct.Controller);
        Assert.Equal("Sample.DirectProfile", direct.Descriptor.ProfileName);
    }

    [Fact]
    public void Build_RejectsMissingCapabilityDeclaration()
    {
        var registry = CreateRegistry(NetworkSyncProfiles.PredictRollback, (in int _) => "controller");
        var options = new NetworkSyncSessionOptions
        {
            RequiredProfile = NetworkSyncProfiles.PredictRollback,
            RequiredMinimumSchemaVersion = 1,
            RequiredMaximumSchemaVersion = 1
        };

        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Equal(
            NetworkSyncSessionBuildFailureReason.MissingAvailableCapabilities,
            exception.Reason);
    }

    [Fact]
    public void Build_RejectsMissingCapabilitiesAndSchemaIntersection()
    {
        var registry = CreateRegistry(NetworkSyncProfiles.PredictRollback, (in int _) => "controller");
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 3, 4);
        options.RequiredProfile = NetworkSyncProfiles.PredictRollback;
        options.RequiredMinimumSchemaVersion = 1;
        options.RequiredMaximumSchemaVersion = 2;
        options.AvailableCapabilities = new NetworkSyncCapabilities(
            3,
            4,
            ClientPlaybackCapabilities.PredictRollback,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.SchemaVersionMismatch);
        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.MissingInputCapabilities);
    }

    [Fact]
    public void Build_RejectsMissingControllerBeforeRuntimeStarts()
    {
        var registry = CreateRegistry(NetworkSyncProfiles.Lockstep, (in int _) => "controller");
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 1, 1);
        options.RequiredProfile = NetworkSyncProfiles.PredictRollback;

        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Equal(
            NetworkSyncSessionBuildFailureReason.MissingControllerRegistration,
            exception.Reason);
        Assert.NotNull(exception.Descriptor);
        Assert.True(exception.Descriptor!.ConfigurationReport.IsValid);
    }

    [Fact]
    public void Build_DoesNotWrapControllerBuilderException()
    {
        var expected = new ApplicationException("controller failed");
        var registry = CreateRegistry<string, int>(
            NetworkSyncProfiles.PredictRollback,
            (in int _) => throw expected);
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 1, 1);
        options.RequiredProfile = NetworkSyncProfiles.PredictRollback;

        var actual = Assert.Throws<ApplicationException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Constructor_SnapshotsOptions()
    {
        var registry = CreateRegistry(NetworkSyncProfiles.PredictRollback, (in int _) => "controller");
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 1, 2);
        options.RequiredProfile = NetworkSyncProfiles.PredictRollback;
        options.RequiredProfileName = "Original";
        var builder = new NetworkSyncSessionBuilder<string, int>(registry, options);

        options.RequiredProfileName = "Changed";
        options.RequiredMinimumSchemaVersion = 9;
        options.RequiredMaximumSchemaVersion = 9;

        var result = builder.Build(0);

        Assert.Equal("Original", result.Descriptor.ProfileName);
        Assert.Equal(1, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(2, result.Descriptor.MaximumSchemaVersion);
    }

    [Fact]
    public void Constructor_SnapshotsMutableProfileCatalog()
    {
        var original = NetworkSyncProfiles.PredictRollback;
        var replacement = new NetworkSyncProfile(
            NetworkSyncModel.PredictRollback,
            ClientPlaybackPolicy.PredictRollback,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("Sample.Profile", original);
        var registry = CreateRegistry(original, (in int _) => "controller");
        var options = CreateOptions(original, 1, 1);
        options.ProfileCatalog = catalog;
        options.RequiredProfileName = "Sample.Profile";
        var builder = new NetworkSyncSessionBuilder<string, int>(registry, options);

        catalog.Register(
            "Sample.Profile",
            replacement,
            NetworkSyncProfileRegistrationMode.ReplaceExisting);

        var result = builder.Build(0);

        Assert.Equal(original, result.Descriptor.Profile);
    }

    [Fact]
    public void Build_RejectsNullControllerResult()
    {
        var registry = CreateRegistry<string?, int>(
            NetworkSyncProfiles.PredictRollback,
            (in int _) => null);
        var options = CreateOptions(NetworkSyncProfiles.PredictRollback, 1, 1);
        options.RequiredProfile = NetworkSyncProfiles.PredictRollback;

        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
        {
            new NetworkSyncSessionBuilder<string?, int>(registry, options).Build(0);
        });

        Assert.Equal(
            NetworkSyncSessionBuildFailureReason.ControllerFactoryReturnedNull,
            exception.Reason);
        Assert.NotNull(exception.Descriptor);
    }

    [Fact]
    public void Build_NegotiatesLocalAndRemoteSchemaIntersection()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 5);
        options.RequiredProfile = profile;
        options.RequiredMinimumSchemaVersion = 2;
        options.RequiredMaximumSchemaVersion = 6;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;
        options.RemoteCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 3, 4);

        var result = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);

        Assert.Equal(2, result.Descriptor.LocalNegotiation.MinimumSchemaVersion);
        Assert.Equal(5, result.Descriptor.LocalNegotiation.MaximumSchemaVersion);
        Assert.True(result.Descriptor.IsRemoteNegotiated);
        Assert.Equal(NetworkSyncRemoteCapabilityPolicy.Require, result.Descriptor.RemoteCapabilityPolicy);
        Assert.Equal(3, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(4, result.Descriptor.MaximumSchemaVersion);
        Assert.Equal(result.Descriptor.LocalCapabilities, result.Descriptor.AvailableCapabilities);
        Assert.True(result.Descriptor.RemoteCapabilities.HasValue);
    }

    [Fact]
    public void Build_RequireRemoteCapabilitiesRejectsMissingDeclarationBeforeControllerConstruction()
    {
        var controllerBuilt = false;
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) =>
        {
            controllerBuilt = true;
            return "controller";
        });
        var options = CreateOptions(profile, 1, 1);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;

        var exception = Assert.Throws<NetworkSyncSessionBuildException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Equal(NetworkSyncSessionBuildFailureReason.MissingRemoteCapabilities, exception.Reason);
        Assert.False(controllerBuilt);
        Assert.NotNull(exception.Descriptor);
        Assert.True(exception.Descriptor!.LocalNegotiation.IsCompatible);
        Assert.False(exception.Descriptor.IsRemoteNegotiated);
    }

    [Fact]
    public void Build_OptionalRemoteCapabilityUsesLocalResultWhenDeclarationIsMissing()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 2);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable;

        var result = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);

        Assert.False(result.Descriptor.IsRemoteNegotiated);
        Assert.Equal(
            NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable,
            result.Descriptor.RemoteCapabilityPolicy);
        Assert.False(result.Descriptor.RemoteCapabilities.HasValue);
        Assert.Equal(1, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(2, result.Descriptor.MaximumSchemaVersion);
    }

    [Fact]
    public void Build_RejectsRemoteCapabilityOrVersionMismatch()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 2);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;
        options.RemoteCapabilities = new NetworkSyncCapabilities(
            3,
            4,
            ClientPlaybackCapabilities.PredictRollback,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.SchemaVersionMismatch);
        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.MissingInputCapabilities);
    }

    [Fact]
    public void Build_RejectsRemoteReliableEventMismatchBeforeControllerConstruction()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var controllerConstructed = false;
        var registry = CreateRegistry(profile, (in int _) =>
        {
            controllerConstructed = true;
            return "controller";
        });
        var options = CreateOptions(profile, 1, 2);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;
        var remote = NetworkSyncCapabilities.FromProfile(in profile, 1, 2);
        options.RemoteCapabilities = new NetworkSyncCapabilities(
            remote.MinimumSchemaVersion,
            remote.MaximumSchemaVersion,
            remote.ClientPlayback,
            remote.Input,
            remote.Snapshot,
            remote.Interest,
            remote.Recovery,
            remote.ServerValidation,
            remote.ReliableEvent & ~ReliableEventCapabilities.ExternalAcknowledgement);

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0));

        Assert.False(controllerConstructed);
        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.MissingReliableEventCapabilities);
    }

    [Fact]
    public void Constructor_SnapshotsRemoteCapabilityOptions()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 3);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;
        options.RemoteCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 2, 2);
        var builder = new NetworkSyncSessionBuilder<string, int>(registry, options);

        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Ignore;
        options.RemoteCapabilities = null;

        var result = builder.Build(0);

        Assert.True(result.Descriptor.IsRemoteNegotiated);
        Assert.Equal(2, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(2, result.Descriptor.MaximumSchemaVersion);
    }

    [Fact]
    public void Build_IgnorePolicyDoesNotConsumeProvidedRemoteCapabilities()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 2);
        options.RequiredProfile = profile;
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Ignore;
        options.RemoteCapabilities = new NetworkSyncCapabilities(
            9,
            9,
            ClientPlaybackCapabilities.None,
            InputPolicy.None,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None);

        var result = new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);

        Assert.False(result.Descriptor.IsRemoteNegotiated);
        Assert.False(result.Descriptor.RemoteCapabilities.HasValue);
        Assert.Equal(1, result.Descriptor.MinimumSchemaVersion);
        Assert.Equal(2, result.Descriptor.MaximumSchemaVersion);
    }

    [Fact]
    public void Build_LocalCapabilityFailurePrecedesMissingRequiredRemoteDeclaration()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var registry = CreateRegistry(profile, (in int _) => "controller");
        var options = CreateOptions(profile, 1, 1);
        options.RequiredProfile = profile;
        options.AvailableCapabilities = new NetworkSyncCapabilities(
            1,
            1,
            ClientPlaybackCapabilities.PredictRollback,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);
        options.RemoteCapabilityPolicy = NetworkSyncRemoteCapabilityPolicy.Require;

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
        {
            new NetworkSyncSessionBuilder<string, int>(registry, options).Build(0);
        });

        Assert.Contains(exception.Report.Issues,
            issue => issue.Code == NetworkSyncConfigurationIssueCode.MissingInputCapabilities);
    }

    private static NetworkSyncSessionOptions CreateOptions(
        in NetworkSyncProfile profile,
        int minimumSchemaVersion,
        int maximumSchemaVersion)
    {
        return new NetworkSyncSessionOptions
        {
            RequiredMinimumSchemaVersion = minimumSchemaVersion,
            RequiredMaximumSchemaVersion = maximumSchemaVersion,
            AvailableCapabilities = NetworkSyncCapabilities.FromProfile(
                in profile,
                minimumSchemaVersion,
                maximumSchemaVersion)
        };
    }

    private static NetworkSyncProfileControllerRegistry<string, int> CreateRegistry(
        in NetworkSyncProfile profile,
        NetworkSyncProfileControllerBuilder<string, int> builder)
    {
        return CreateRegistry<string, int>(in profile, builder);
    }

    private static NetworkSyncProfileControllerRegistry<TController, TContext> CreateRegistry<TController, TContext>(
        in NetworkSyncProfile profile,
        NetworkSyncProfileControllerBuilder<TController, TContext> builder)
    {
        return new NetworkSyncProfileControllerRegistry<TController, TContext>(
            new Dictionary<NetworkSyncProfile, NetworkSyncProfileControllerBuilder<TController, TContext>>
            {
                [profile] = builder
            });
    }
}
