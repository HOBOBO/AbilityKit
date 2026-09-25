using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Server;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Orleans.Grains.Gameplay;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Rooms;

public sealed class TinySyncModeProfileTests
{
    [Theory]
    [InlineData(TinySyncTemplates.State, NetworkSyncModel.AuthoritativeInterpolation)]
    [InlineData(TinySyncTemplates.Frame, NetworkSyncModel.Lockstep)]
    [InlineData(TinySyncTemplates.Hybrid, NetworkSyncModel.HybridHeroPrediction)]
    public void TinyTemplateDeclaresNegotiatedModel(string template, NetworkSyncModel expected)
    {
        var module = TinyServerGameplayModule.Create();
        var profile = module.SyncProfile;
        Assert.True(profile.TryResolveTemplate(template, out var selected));
        Assert.Equal(template, selected.TemplateId);
        var capability = module.ResolveSyncCapabilities(null, template);
        Assert.Equal(expected.ToString(), capability.ProfileName);
        var selectedProfile = capability.Profile;
        Assert.True(NetworkSyncConfigurationValidator.ValidateProfile(in selectedProfile).IsValid);
        if (expected == NetworkSyncModel.Lockstep)
        {
            Assert.True((selectedProfile.Snapshot & SnapshotPolicy.FullSnapshot) != 0);
            Assert.True((selectedProfile.Recovery & RecoveryPolicy.RequestFullSnapshot) != 0);
        }
    }
}
