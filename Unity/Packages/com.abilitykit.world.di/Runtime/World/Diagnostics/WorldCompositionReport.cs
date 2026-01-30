using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.Diagnostics
{
    public sealed class WorldCompositionReport
    {
        public WorldCompositionReport(string worldId, string worldType)
        {
            WorldId = worldId;
            WorldType = worldType;
            CreatedUtc = DateTime.UtcNow;
        }

        public string WorldId { get; }
        public string WorldType { get; }
        public DateTime CreatedUtc { get; }

        public IReadOnlyList<ModuleEntry> Modules => _modules;
        public IReadOnlyList<string> Installers => _installers;
        public IReadOnlyList<string> RegisteredServices => _registeredServices;

        private readonly List<ModuleEntry> _modules = new List<ModuleEntry>(32);
        private readonly List<string> _installers = new List<string>(16);
        private readonly List<string> _registeredServices = new List<string>(128);

        public void AddModule(ModuleEntry entry) => _modules.Add(entry);
        public void AddInstaller(string installerType) => _installers.Add(installerType);
        public void AddRegisteredService(string serviceType) => _registeredServices.Add(serviceType);

        public readonly struct ModuleEntry
        {
            public ModuleEntry(int index, int sourceIndex, int order, string id, string type)
            {
                Index = index;
                SourceIndex = sourceIndex;
                Order = order;
                Id = id;
                Type = type;
            }

            public int Index { get; }
            public int SourceIndex { get; }
            public int Order { get; }
            public string Id { get; }
            public string Type { get; }
        }
    }
}
