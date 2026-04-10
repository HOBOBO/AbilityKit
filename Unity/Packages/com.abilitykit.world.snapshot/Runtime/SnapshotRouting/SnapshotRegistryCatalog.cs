using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    public sealed class SnapshotRegistryCatalog
    {
        private readonly List<IIdentifiedSnapshotRegistry> _registries = new List<IIdentifiedSnapshotRegistry>();
        private readonly Dictionary<string, IIdentifiedSnapshotRegistry> _byId = new Dictionary<string, IIdentifiedSnapshotRegistry>(StringComparer.Ordinal);

        public IReadOnlyList<IIdentifiedSnapshotRegistry> Registries => _registries;

        public SnapshotRegistryCatalog Add(IIdentifiedSnapshotRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            if (_byId.ContainsKey(registry.RegistryId))
            {
                throw new InvalidOperationException($"Snapshot registry already exists: registryId='{registry.RegistryId}'");
            }

            _byId.Add(registry.RegistryId, registry);
            _registries.Add(registry);
            return this;
        }

        public SnapshotRegistryCatalog Add(
            string registryId,
            Action<ISnapshotDecoderRegistry, ISnapshotDecoderRegistry, ISnapshotPipelineStageRegistry, ISnapshotCmdHandlerRegistry> register)
        {
            return Add(SnapshotRoutingBuilder.From(registryId, register));
        }

        public bool TryGet(string registryId, out IIdentifiedSnapshotRegistry registry)
        {
            if (registryId == null)
            {
                registry = null;
                return false;
            }

            return _byId.TryGetValue(registryId, out registry);
        }
    }
}
