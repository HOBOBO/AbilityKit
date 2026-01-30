using System;

namespace AbilityKit.Ability.Host.Framework
{
    public interface IHostRuntimeModule
    {
        void Install(HostRuntime runtime, HostRuntimeOptions options);
        void Uninstall(HostRuntime runtime, HostRuntimeOptions options);
    }
}
