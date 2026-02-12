using System;

namespace AbilityKit.Ability.Share.Common.Config
{
    public sealed class ModuleInstallerConfigSet
    {
        public ModuleInstallerConfig[] Modules;

        public ModuleInstallerConfig FindModule(string moduleKey)
        {
            var ms = Modules;
            if (ms == null || ms.Length == 0) return null;

            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (m == null || !m.IsValid) continue;
                if (string.Equals(m.ModuleKey, moduleKey, StringComparison.Ordinal)) return m;
            }

            return null;
        }
    }
}
