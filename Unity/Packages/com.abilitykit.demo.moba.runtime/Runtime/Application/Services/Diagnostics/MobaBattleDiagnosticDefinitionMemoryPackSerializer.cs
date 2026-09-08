using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleDiagnosticDefinitionMemoryPackSerializer :
        IBattleDiagnosticDefinitionCatalogSerializer
    {
        public const string MemoryPackFormatId =
            "abilitykit-battle-diagnostic-definitions.memorypack.v1";
        private const uint Magic = 0x444B4241U;
        private const int MajorVersion = 1;

        public string FormatId => MemoryPackFormatId;
        public string MediaType => "application/x-memorypack";

        public byte[] Serialize(BattleDiagnosticDefinitionCatalogSnapshot catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return MemoryPackSerializer.Serialize(ToWire(catalog));
        }

        public BattleDiagnosticDefinitionCatalogSnapshot Deserialize(ReadOnlySpan<byte> artifact)
        {
            if (artifact.IsEmpty) throw new MobaBattleDiagnosticArtifactException(
                "DefinitionCatalog.Empty",
                "Definition catalog payload is empty.");
            try
            {
                var wire = MemoryPackSerializer.Deserialize<DefinitionCatalogWire>(artifact);
                if (wire == null)
                    throw new MobaBattleDiagnosticArtifactException(
                        "DefinitionCatalog.Invalid",
                        "Definition catalog payload could not be deserialized.");
                if (wire.Magic != Magic)
                    throw new MobaBattleDiagnosticArtifactException(
                        "DefinitionCatalog.Format",
                        "Definition catalog payload has an invalid format marker.");
                if (wire.MajorVersion != MajorVersion)
                    throw new MobaBattleDiagnosticArtifactException(
                        "DefinitionCatalog.Version",
                        "Unsupported definition catalog major version: " + wire.MajorVersion);

                var scope = new BattleDiagnosticSessionScope(
                    wire.SessionId,
                    wire.WorldId,
                    wire.WorldEpoch);
                var source = wire.Items ?? Array.Empty<DefinitionWire>();
                var items = new List<BattleDiagnosticDefinition>(source.Length);
                for (var i = 0; i < source.Length; i++)
                    items.Add(FromWire(source[i]));
                return new BattleDiagnosticDefinitionCatalogSnapshot(
                    scope,
                    wire.Revision,
                    items);
            }
            catch (MobaBattleDiagnosticArtifactException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MobaBattleDiagnosticArtifactException(
                    "DefinitionCatalog.MalformedBinary",
                    "Definition catalog MemoryPack payload is malformed.",
                    ex);
            }
        }

        private static DefinitionCatalogWire ToWire(
            BattleDiagnosticDefinitionCatalogSnapshot catalog)
        {
            var items = new DefinitionWire[catalog.Items.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = ToWire(catalog.Items[i]);
            return new DefinitionCatalogWire
            {
                Magic = Magic,
                MajorVersion = MajorVersion,
                SessionId = catalog.Scope.SessionId,
                WorldId = catalog.Scope.WorldId,
                WorldEpoch = catalog.Scope.WorldEpoch,
                Revision = catalog.Revision,
                Items = items
            };
        }

        private static DefinitionWire ToWire(BattleDiagnosticDefinition definition)
        {
            var metadata = new DefinitionMetadataWire[definition.Metadata.Count];
            for (var i = 0; i < metadata.Length; i++)
            {
                var item = definition.Metadata[i];
                metadata[i] = new DefinitionMetadataWire
                {
                    Key = item.Key,
                    ValueKind = (int)item.ValueKind,
                    StringValue = item.StringValue,
                    IntegerValue = item.IntegerValue,
                    NumberValue = item.NumberValue,
                    BooleanValue = item.BooleanValue
                };
            }
            return new DefinitionWire
            {
                Kind = (int)definition.Kind,
                DefinitionId = definition.DefinitionId,
                DisplayName = definition.DisplayName,
                Revision = definition.Revision,
                ContentHash = definition.ContentHash,
                SourcePath = definition.SourcePath,
                Resolution = (int)definition.Resolution,
                Metadata = metadata
            };
        }

        private static BattleDiagnosticDefinition FromWire(DefinitionWire wire)
        {
            if (wire == null) throw new MobaBattleDiagnosticArtifactException(
                "DefinitionCatalog.Definition",
                "Definition catalog contains a null item.");
            var reference = BattleDiagnosticDefinitionReference.Create(
                (BattleDiagnosticDefinitionKind)wire.Kind,
                wire.DefinitionId);
            var source = wire.Metadata ?? Array.Empty<DefinitionMetadataWire>();
            var metadata = new List<BattleDiagnosticDefinitionMetadataEntry>(source.Length);
            for (var i = 0; i < source.Length; i++)
                metadata.Add(FromWire(source[i]));
            return new BattleDiagnosticDefinition(
                in reference,
                wire.DisplayName,
                wire.Revision,
                wire.ContentHash,
                wire.SourcePath,
                (BattleDiagnosticDefinitionResolution)wire.Resolution,
                metadata);
        }

        private static BattleDiagnosticDefinitionMetadataEntry FromWire(
            DefinitionMetadataWire wire)
        {
            if (wire == null) throw new MobaBattleDiagnosticArtifactException(
                "DefinitionCatalog.Metadata",
                "Definition catalog contains a null metadata item.");
            switch ((BattleDiagnosticDefinitionMetadataValueKind)wire.ValueKind)
            {
                case BattleDiagnosticDefinitionMetadataValueKind.String:
                    return BattleDiagnosticDefinitionMetadataEntry.String(
                        wire.Key,
                        wire.StringValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Integer:
                    return BattleDiagnosticDefinitionMetadataEntry.Integer(
                        wire.Key,
                        wire.IntegerValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Number:
                    return BattleDiagnosticDefinitionMetadataEntry.Number(
                        wire.Key,
                        wire.NumberValue);
                case BattleDiagnosticDefinitionMetadataValueKind.Boolean:
                    return BattleDiagnosticDefinitionMetadataEntry.Boolean(
                        wire.Key,
                        wire.BooleanValue);
                default:
                    throw new MobaBattleDiagnosticArtifactException(
                        "DefinitionCatalog.MetadataKind",
                        "Definition metadata value kind is invalid: " + wire.ValueKind);
            }
        }

    }

    [MemoryPackable]
    internal sealed partial class DefinitionCatalogWire
    {
        [MemoryPackOrder(0)] public uint Magic;
        [MemoryPackOrder(1)] public int MajorVersion;
        [MemoryPackOrder(2)] public string SessionId;
        [MemoryPackOrder(3)] public string WorldId;
        [MemoryPackOrder(4)] public long WorldEpoch;
        [MemoryPackOrder(5)] public long Revision;
        [MemoryPackOrder(6)] public DefinitionWire[] Items;
    }

    [MemoryPackable]
    internal sealed partial class DefinitionWire
    {
        [MemoryPackOrder(0)] public int Kind;
        [MemoryPackOrder(1)] public int DefinitionId;
        [MemoryPackOrder(2)] public string DisplayName;
        [MemoryPackOrder(3)] public string Revision;
        [MemoryPackOrder(4)] public string ContentHash;
        [MemoryPackOrder(5)] public string SourcePath;
        [MemoryPackOrder(6)] public int Resolution;
        [MemoryPackOrder(7)] public DefinitionMetadataWire[] Metadata;
    }

    [MemoryPackable]
    internal sealed partial class DefinitionMetadataWire
    {
        [MemoryPackOrder(0)] public string Key;
        [MemoryPackOrder(1)] public int ValueKind;
        [MemoryPackOrder(2)] public string StringValue;
        [MemoryPackOrder(3)] public long IntegerValue;
        [MemoryPackOrder(4)] public double NumberValue;
        [MemoryPackOrder(5)] public bool BooleanValue;
    }
}
