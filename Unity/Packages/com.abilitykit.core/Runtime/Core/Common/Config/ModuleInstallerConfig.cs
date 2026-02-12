namespace AbilityKit.Ability.Share.Common.Config
{
    public sealed class ModuleInstallerConfig
    {
        public string ModuleKey;
        public string InstallerType;
        public string InstallerMethod;

        public bool IsValid => !string.IsNullOrEmpty(ModuleKey) && !string.IsNullOrEmpty(InstallerType);

        public string GetEffectiveMethod() => string.IsNullOrEmpty(InstallerMethod) ? "InstallAsCurrent" : InstallerMethod;
    }
}
