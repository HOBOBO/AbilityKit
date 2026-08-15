using AbilityKit.Demo.Moba.Bootstrap;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Bootstrap;

public sealed class MobaBootstrapInfrastructureTests
{
    [Fact]
    public void Module_installer_set_finds_valid_module_by_ordinal_key()
    {
        var expected = new ModuleInstallerConfig
        {
            ModuleKey = "protocol.wire_serializer",
            InstallerType = typeof(TestInstaller).AssemblyQualifiedName,
        };
        var set = new ModuleInstallerConfigSet
        {
            Modules = new[] { new ModuleInstallerConfig(), expected },
        };

        Assert.Same(expected, set.FindModule("protocol.wire_serializer"));
        Assert.Null(set.FindModule("PROTOCOL.WIRE_SERIALIZER"));
        Assert.Equal("InstallAsCurrent", expected.GetEffectiveMethod());
    }

    [Fact]
    public void Module_installer_invoker_only_invokes_configured_public_static_method()
    {
        TestInstaller.InvocationCount = 0;
        var installer = new ModuleInstallerConfig
        {
            ModuleKey = "test",
            InstallerType = typeof(TestInstaller).AssemblyQualifiedName,
            InstallerMethod = nameof(TestInstaller.Install),
        };

        Assert.True(ModuleInstallerInvoker.TryInvoke(installer));
        Assert.Equal(1, TestInstaller.InvocationCount);
    }

    public static class TestInstaller
    {
        public static int InvocationCount { get; set; }
        public static void Install() => InvocationCount++;
    }
}
