using System;

namespace AbilityKit.Core.Configuration
{
    [Obsolete("Module installation policy no longer belongs in Core. Move it to the owning bootstrap package; this compatibility API will be removed in the next major version.")]
    public sealed class ModuleInstallerConfig
    {
        public string ModuleKey;
        public string InstallerType;
        public string InstallerMethod;

        public bool IsValid => !string.IsNullOrEmpty(ModuleKey) && !string.IsNullOrEmpty(InstallerType);

        public string GetEffectiveMethod() => string.IsNullOrEmpty(InstallerMethod) ? "InstallAsCurrent" : InstallerMethod;
    }
}
