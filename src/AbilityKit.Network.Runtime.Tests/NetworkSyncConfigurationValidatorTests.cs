using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkSyncConfigurationValidatorTests
{
    [Fact]
    public void BuiltInProfiles_AreInternallyValid()
    {
        foreach (var profile in NetworkSyncProfileRegistry.Profiles())
        {
            var report = NetworkSyncConfigurationValidator.ValidateProfile(in profile);

            Assert.True(report.IsValid);
            Assert.Empty(report.Issues);
        }
    }

    [Fact]
    public void ValidateProfile_ReportsAllIndependentConflicts()
    {
        var profile = new NetworkSyncProfile(
            (NetworkSyncModel)1001,
            ClientPlaybackPolicy.PredictRollback,
            InputPolicy.NoClientInput,
            SnapshotPolicy.None,
            InterestPolicy.AllEntities | InterestPolicy.DistanceAoi,
            RecoveryPolicy.RequestAoiSlice,
            ServerValidationPolicy.InputValidation);

        var report = NetworkSyncConfigurationValidator.ValidateProfile(in profile);

        Assert.False(report.IsValid);
        Assert.Equal(4, report.ErrorCount);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.ConflictingInterestPolicies);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.PredictionRequiresSubmittedInput);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.RecoveryRequiresSnapshotStream);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.InputValidationRequiresSubmittedInput);
    }

    [Fact]
    public void ValidateProfile_ReportsUnknownEnumValuesAndFlags()
    {
        var profile = new NetworkSyncProfile(
            (NetworkSyncModel)1001,
            (ClientPlaybackPolicy)99,
            (InputPolicy)int.MinValue,
            (SnapshotPolicy)(1 << 20),
            (InterestPolicy)(1 << 20),
            (RecoveryPolicy)(1 << 20),
            (ServerValidationPolicy)(1 << 20));

        var report = NetworkSyncConfigurationValidator.ValidateProfile(in profile);

        Assert.Equal(6, report.ErrorCount);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.UnknownClientPlaybackPolicy);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.UnknownServerValidationPolicyBits);
    }

    [Fact]
    public void Negotiate_ReturnsSchemaIntersectionWhenCapabilitiesCoverProfile()
    {
        var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
        var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 2, 5);

        var result = NetworkSyncConfigurationValidator.Negotiate(
            in profile,
            requiredMinimumSchemaVersion: 1,
            requiredMaximumSchemaVersion: 3,
            in capabilities);

        Assert.True(result.IsCompatible);
        Assert.Equal(2, result.MinimumSchemaVersion);
        Assert.Equal(3, result.MaximumSchemaVersion);
    }

    [Fact]
    public void Negotiate_ReportsEveryMissingCapabilityDimensionAndVersionMismatch()
    {
        var profile = NetworkSyncProfiles.HybridHeroPrediction;
        var capabilities = new NetworkSyncCapabilities(
            10,
            12,
            ClientPlaybackCapabilities.None,
            InputPolicy.None,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None);

        var result = NetworkSyncConfigurationValidator.Negotiate(in profile, 1, 3, in capabilities);

        Assert.False(result.IsCompatible);
        Assert.Equal(8, result.Report.ErrorCount);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.SchemaVersionMismatch);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingClientPlaybackCapability);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingRecoveryCapabilities);
        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingReliableEventCapabilities);
    }

    [Fact]
    public void ValidateProfile_RejectsConflictingReliableEventAcknowledgementOwnership()
    {
        var profile = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery |
            ReliableEventPolicy.AutomaticAcknowledgement |
            ReliableEventPolicy.ExternalAcknowledgement);

        var report = NetworkSyncConfigurationValidator.ValidateProfile(in profile);

        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.ConflictingReliableEventAcknowledgementPolicies);
    }

    [Fact]
    public void ValidateCapabilities_RejectsBufferedDeliveryWithoutOrdering()
    {
        var capabilities = new NetworkSyncCapabilities(
            1,
            1,
            ClientPlaybackCapabilities.None,
            InputPolicy.None,
            SnapshotPolicy.EventStream,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None,
            ReliableEventCapabilities.BufferedOutOfOrder);

        var report = NetworkSyncConfigurationValidator.ValidateCapabilities(in capabilities);

        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.ReliableEventBufferRequiresOrderedDelivery);
    }

    [Fact]
    public void Negotiate_RejectsEndpointWithoutRequiredAcknowledgementOwnership()
    {
        var profile = NetworkSyncProfiles.PredictRollback;
        var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1);
        capabilities = new NetworkSyncCapabilities(
            capabilities.MinimumSchemaVersion,
            capabilities.MaximumSchemaVersion,
            capabilities.ClientPlayback,
            capabilities.Input,
            capabilities.Snapshot,
            capabilities.Interest,
            capabilities.Recovery,
            capabilities.ServerValidation,
            capabilities.ReliableEvent & ~ReliableEventCapabilities.ExternalAcknowledgement);

        var result = NetworkSyncConfigurationValidator.Negotiate(in profile, 1, 1, in capabilities);

        Assert.Contains(result.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingReliableEventCapabilities);
    }

    [Fact]
    public void ValidateCapabilities_ReportsInvalidRangeAndUnknownFlags()
    {
        var capabilities = new NetworkSyncCapabilities(
            3,
            2,
            (ClientPlaybackCapabilities)(1 << 20),
            InputPolicy.None,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None);

        var report = NetworkSyncConfigurationValidator.ValidateCapabilities(in capabilities);

        Assert.Equal(2, report.ErrorCount);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.InvalidSchemaVersionRange);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.UnknownClientPlaybackCapabilityBits);
    }

    [Fact]
    public void Report_ThrowsExceptionThatRetainsStructuredIssues()
    {
        var invalid = new NetworkSyncProfile(
            (NetworkSyncModel)1001,
            ClientPlaybackPolicy.PredictRollback,
            InputPolicy.None,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None);
        var report = NetworkSyncConfigurationValidator.ValidateProfile(in invalid);

        var exception = Assert.Throws<NetworkSyncConfigurationException>(() =>
            report.ThrowIfInvalid("Project.Profile"));

        Assert.Same(report, exception.Report);
        Assert.Contains("Project.Profile", exception.Message);
    }

    [Fact]
    public void Catalog_RejectsInvalidProfileBeforePublication()
    {
        var catalog = new NetworkSyncProfileCatalog();
        var invalid = new NetworkSyncProfile(
            (NetworkSyncModel)1001,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.None,
            InterestPolicy.AllEntities,
            RecoveryPolicy.None,
            ServerValidationPolicy.AuthoritativeOnly);

        Assert.Throws<NetworkSyncConfigurationException>(() => catalog.Register("Invalid", invalid));
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void OptionsReport_MergesRequiredMembersAndCapabilityProblems()
    {
        var profile = NetworkSyncProfiles.BatchStateSync;
        var options = new ClientSnapshotSyncOptions<int>(1, 3, null!, null!)
        {
            RequiredProfile = profile,
            AvailableCapabilities = new NetworkSyncCapabilities(
                1,
                3,
                ClientPlaybackCapabilities.None,
                InputPolicy.None,
                SnapshotPolicy.None,
                InterestPolicy.None,
                RecoveryPolicy.None,
                ServerValidationPolicy.None)
        };

        var report = options.ValidateConfiguration();

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingEnvelopeFactory);
        Assert.Contains(report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingSnapshotCapabilities);
    }

    [Fact]
    public void Pipeline_UsesNegotiatedSchemaVersionRange()
    {
        var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
        var capabilities = NetworkSyncCapabilities.FromProfile(in profile, 2, 5);
        var options = new ClientSnapshotSyncOptions<int>(1, 3, CreateEnvelope, static (in int _) => { })
        {
            RequiredProfile = profile,
            AvailableCapabilities = capabilities
        };

        var pipeline = new ClientSnapshotSyncPipeline<int>(options);

        Assert.Equal(2, pipeline.MinimumSupportedVersion);
        Assert.Equal(3, pipeline.MaximumSupportedVersion);
        Assert.True(pipeline.Negotiation.HasValue);
        Assert.True(pipeline.Negotiation.Value.IsCompatible);
    }

    [Fact]
    public void Options_RequireProfileWhenCapabilitiesAreProvided()
    {
        var options = new ClientSnapshotSyncOptions<int>(1, 1, CreateEnvelope, static (in int _) => { })
        {
            AvailableCapabilities = new NetworkSyncCapabilities(
                1,
                1,
                ClientPlaybackCapabilities.None,
                InputPolicy.None,
                SnapshotPolicy.None,
                InterestPolicy.None,
                RecoveryPolicy.None,
                ServerValidationPolicy.None)
        };

        var exception = Assert.Throws<NetworkSyncConfigurationException>(options.Validate);

        Assert.Contains(exception.Report.Issues, issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.MissingRequiredProfile);
    }

    private static SnapshotStreamEnvelope CreateEnvelope(in int frame)
    {
        return new SnapshotStreamEnvelope(
            worldId: 1,
            schemaVersion: 2,
            sequence: frame,
            frame,
            SnapshotStreamSnapshotKind.FullBaseline,
            baselineFrame: frame,
            baselineHash: 1,
            stateHash: 1);
    }
}
