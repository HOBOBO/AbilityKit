using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkSyncProfileRegistryTests
{
    [Fact]
    public void Registry_CoversEveryKnownCompatibilityModel()
    {
        var registered = NetworkSyncProfileRegistry.Models().ToArray();
        var declared = Enum.GetValues<NetworkSyncModel>();

        Assert.Equal(declared.Length, NetworkSyncProfileRegistry.Count);
        Assert.Equal(declared.OrderBy(m => m), registered.OrderBy(m => m));
    }

    [Fact]
    public void Models_AreEnumeratedInEnumOrder()
    {
        var registered = NetworkSyncProfileRegistry.Models().ToArray();
        var ordered = registered.OrderBy(m => (int)m).ToArray();

        Assert.Equal(ordered, registered);
    }

    [Fact]
    public void Resolve_MapsEveryModelToProfileWithSameCompatibilityModel()
    {
        foreach (var model in Enum.GetValues<NetworkSyncModel>())
        {
            var profile = NetworkSyncProfileRegistry.Resolve(model);

            Assert.Equal(model, profile.CompatibilityModel);
        }
    }

    [Fact]
    public void Resolve_AgreesWithLegacyFromCompatibilityModel()
    {
        foreach (var model in Enum.GetValues<NetworkSyncModel>())
        {
            Assert.Equal(NetworkSyncProfiles.FromCompatibilityModel(model), NetworkSyncProfileRegistry.Resolve(model));
        }
    }

    [Fact]
    public void Resolve_ThrowsForUnknownModel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NetworkSyncProfileRegistry.Resolve((NetworkSyncModel)999));
    }

    [Fact]
    public void TryResolve_ReturnsTrueAndProfileForKnownModel()
    {
        var resolved = NetworkSyncProfileRegistry.TryResolve(NetworkSyncModel.FastReconnect, out var profile);

        Assert.True(resolved);
        Assert.Equal(NetworkSyncProfiles.FastReconnect, profile);
    }

    [Fact]
    public void TryResolve_ReturnsFalseAndUnspecifiedForUnknownModel()
    {
        var resolved = NetworkSyncProfileRegistry.TryResolve((NetworkSyncModel)999, out var profile);

        Assert.False(resolved);
        Assert.Equal(NetworkSyncProfiles.Unspecified, profile);
    }

    [Fact]
    public void GetName_MatchesEnumMemberName()
    {
        foreach (var model in Enum.GetValues<NetworkSyncModel>())
        {
            Assert.Equal(model.ToString(), NetworkSyncProfileRegistry.GetName(model));
        }
    }

    [Fact]
    public void GetName_ThrowsForUnknownModel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NetworkSyncProfileRegistry.GetName((NetworkSyncModel)999));
    }

    [Fact]
    public void Profiles_MatchResolveForEveryModel()
    {
        var models = NetworkSyncProfileRegistry.Models().ToArray();
        var profiles = NetworkSyncProfileRegistry.Profiles().ToArray();

        Assert.Equal(models.Length, profiles.Length);
        for (var i = 0; i < models.Length; i++)
        {
            Assert.Equal(NetworkSyncProfileRegistry.Resolve(models[i]), profiles[i]);
        }
    }

    [Fact]
    public void Profiles_HaveDistinctCompatibilityModels()
    {
        var compatibilityModels = NetworkSyncProfileRegistry.Profiles()
            .Select(p => p.CompatibilityModel)
            .ToArray();

        Assert.Equal(compatibilityModels.Length, compatibilityModels.Distinct().Count());
    }

    [Fact]
    public void DefaultCatalog_IsFrozenAndMatchesStaticRegistry()
    {
        var catalog = NetworkSyncProfileRegistry.DefaultCatalog;

        Assert.True(catalog.IsFrozen);
        Assert.Equal(NetworkSyncProfileRegistry.Count, catalog.Count);
        Assert.Equal(
            NetworkSyncProfileRegistry.Resolve(NetworkSyncModel.PredictRollback),
            catalog.Resolve(NetworkSyncModel.PredictRollback));
        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register("Replacement", CustomProfile(NetworkSyncModel.PredictRollback)));
    }

    [Fact]
    public void MutableCatalog_CanRegisterProjectSpecificModelAndResolveByName()
    {
        const NetworkSyncModel customModel = (NetworkSyncModel)1001;
        var customProfile = CustomProfile(customModel);
        var catalog = NetworkSyncProfileRegistry.CreateMutableCatalog();

        catalog.Register("Project.CustomSync", customProfile);

        Assert.Equal(NetworkSyncProfileRegistry.Count + 1, catalog.Count);
        Assert.Equal(customProfile, catalog.Resolve(customModel));
        Assert.Equal(customProfile, catalog.Resolve("Project.CustomSync"));
        Assert.Equal("Project.CustomSync", catalog.GetName(customModel));
        Assert.False(NetworkSyncProfileRegistry.TryResolve(customModel, out _));
    }

    [Fact]
    public void Register_AllowsNamedVariantsButRejectsDuplicateStableName()
    {
        var catalog = new NetworkSyncProfileCatalog();
        var model = (NetworkSyncModel)1001;
        var canonical = CustomProfile(model);
        var variant = new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.HoldLatest,
            InputPolicy.NoClientInput,
            SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.OwnerRelevant,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly);
        catalog.Register("First", canonical);
        catalog.Register("First.Variant", variant);

        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register("First", variant));
        Assert.Equal(canonical, catalog.Resolve(model));
        Assert.Equal(variant, catalog.Resolve("First.Variant"));
    }

    [Fact]
    public void ReplaceExisting_PreservesEntryOrderAndUpdatesNamedAndDefaultResolution()
    {
        const NetworkSyncModel firstModel = (NetworkSyncModel)1001;
        const NetworkSyncModel secondModel = (NetworkSyncModel)1002;
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("First", CustomProfile(firstModel));
        catalog.Register("Second", CustomProfile(secondModel));
        var replacement = new NetworkSyncProfile(
            firstModel,
            ClientPlaybackPolicy.HoldLatest,
            InputPolicy.NoClientInput,
            SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.OwnerRelevant,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly);

        catalog.Register("First", replacement, NetworkSyncProfileRegistrationMode.ReplaceExisting);

        var entries = catalog.Entries();
        Assert.Equal(firstModel, entries[0].Model);
        Assert.Equal(secondModel, entries[1].Model);
        Assert.Equal("First", entries[0].Name);
        Assert.Equal(replacement, catalog.Resolve(firstModel));
        Assert.Equal(replacement, catalog.Resolve("First"));
    }

    [Fact]
    public void ReplaceExisting_NonDefaultVariantDoesNotChangeModelDefault()
    {
        var model = (NetworkSyncModel)1001;
        var canonical = CustomProfile(model);
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("Canonical", canonical);
        catalog.Register("Variant", new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.HoldLatest,
            InputPolicy.NoClientInput,
            SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.OwnerRelevant,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly));
        var replacement = new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.DeltaSnapshot,
            InterestPolicy.DistanceAoi,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);

        catalog.Register("Variant", replacement, NetworkSyncProfileRegistrationMode.ReplaceExisting);

        Assert.Equal(canonical, catalog.Resolve(model));
        Assert.Equal(replacement, catalog.Resolve("Variant"));
        Assert.Throws<InvalidOperationException>(() =>
            catalog.Register(
                "Variant",
                CustomProfile((NetworkSyncModel)1002),
                NetworkSyncProfileRegistrationMode.ReplaceExisting));
    }

    [Fact]
    public void SetDefault_SelectsNamedVariantWithoutChangingEntryOrder()
    {
        var model = (NetworkSyncModel)1001;
        var canonical = CustomProfile(model);
        var variant = new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.HoldLatest,
            InputPolicy.NoClientInput,
            SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.OwnerRelevant,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly);
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("Canonical", canonical);
        catalog.Register("Variant", variant);

        catalog.SetDefault(model, "Variant");

        Assert.Equal(variant, catalog.Resolve(model));
        Assert.Equal(new[] { "Canonical", "Variant" }, catalog.Entries().Select(entry => entry.Name));
        Assert.Throws<InvalidOperationException>(() =>
            catalog.SetDefault((NetworkSyncModel)1002, "Variant"));
        Assert.Throws<KeyNotFoundException>(() => catalog.SetDefault(model, "Missing"));
    }

    [Fact]
    public void Entries_ReturnStableSnapshotAcrossLaterRegistration()
    {
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("First", CustomProfile((NetworkSyncModel)1001));
        var before = catalog.Entries();

        catalog.Register("Second", CustomProfile((NetworkSyncModel)1002));

        Assert.Single(before);
        Assert.Equal(2, catalog.Entries().Count);
    }

    [Fact]
    public void Entries_ReusesReadOnlyViewUntilCatalogChanges()
    {
        var catalog = new NetworkSyncProfileCatalog();
        catalog.Register("First", CustomProfile((NetworkSyncModel)1001));
        var firstView = catalog.Entries();

        Assert.Same(firstView, catalog.Entries());

        catalog.Register("Second", CustomProfile((NetworkSyncModel)1002));

        Assert.NotSame(firstView, catalog.Entries());
        Assert.Single(firstView);
    }

    [Fact]
    public void MutableCopy_IsIndependentAndCanBeFrozen()
    {
        var source = NetworkSyncProfileRegistry.CreateMutableCatalog();
        var copy = source.CreateMutableCopy();
        copy.Register("Project.CustomSync", CustomProfile((NetworkSyncModel)1001));
        copy.Freeze();

        Assert.Equal(NetworkSyncProfileRegistry.Count, source.Count);
        Assert.Equal(NetworkSyncProfileRegistry.Count + 1, copy.Count);
        Assert.True(copy.IsFrozen);
        Assert.Throws<InvalidOperationException>(() =>
            copy.Register("Another", CustomProfile((NetworkSyncModel)1002)));
        Assert.Throws<InvalidOperationException>(() =>
            copy.SetDefault(NetworkSyncModel.Unspecified, nameof(NetworkSyncModel.Unspecified)));
    }

    [Fact]
    public void MutableCopy_PreservesSelectedDefaultVariant()
    {
        var model = (NetworkSyncModel)1001;
        var source = new NetworkSyncProfileCatalog();
        source.Register("Canonical", CustomProfile(model));
        var variant = new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.HoldLatest,
            InputPolicy.NoClientInput,
            SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.OwnerRelevant,
            RecoveryPolicy.RequestKeyFrame,
            ServerValidationPolicy.AuthoritativeOnly);
        source.Register("Variant", variant);
        source.SetDefault(model, "Variant");

        var copy = source.CreateMutableCopy();

        Assert.Equal(variant, copy.Resolve(model));
        Assert.Equal(variant, copy.Resolve("Variant"));
    }

    [Fact]
    public void Catalog_ValidatesNamesAndRegistrationMode()
    {
        var catalog = new NetworkSyncProfileCatalog();
        var profile = CustomProfile((NetworkSyncModel)1001);

        Assert.Throws<ArgumentException>(() => catalog.Register(" ", profile));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.Register("Custom", profile, (NetworkSyncProfileRegistrationMode)999));
        Assert.False(catalog.TryResolve((string?)null, out var missing));
        Assert.Equal(NetworkSyncProfiles.Unspecified, missing);
    }

    [Fact]
    public void StableNames_AreCaseSensitive()
    {
        var catalog = new NetworkSyncProfileCatalog();
        var upper = CustomProfile((NetworkSyncModel)1001);
        var lower = CustomProfile((NetworkSyncModel)1002);

        catalog.Register("Project.Sync", upper);
        catalog.Register("project.sync", lower);

        Assert.Equal(upper, catalog.Resolve("Project.Sync"));
        Assert.Equal(lower, catalog.Resolve("project.sync"));
        Assert.False(catalog.TryResolve("PROJECT.SYNC", out _));
    }

    private static NetworkSyncProfile CustomProfile(NetworkSyncModel model)
    {
        return new NetworkSyncProfile(
            model,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.DeltaSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);
    }
}
