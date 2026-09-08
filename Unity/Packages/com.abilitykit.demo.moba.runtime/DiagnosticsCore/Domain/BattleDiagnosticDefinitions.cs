using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticDefinitionResolution
    {
        Unknown = 0,
        Resolved = 1,
        Unresolved = 2,
    }

    public enum BattleDiagnosticDefinitionMetadataValueKind
    {
        Unknown = 0,
        String = 1,
        Integer = 2,
        Number = 3,
        Boolean = 4,
    }

    [Serializable]
    public readonly struct BattleDiagnosticDefinitionMetadataEntry
    {
        private BattleDiagnosticDefinitionMetadataEntry(
            string key,
            BattleDiagnosticDefinitionMetadataValueKind valueKind,
            string stringValue,
            long integerValue,
            double numberValue,
            bool booleanValue)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(
                "A metadata key is required.",
                nameof(key));
            Key = key;
            ValueKind = valueKind;
            StringValue = stringValue ?? string.Empty;
            IntegerValue = integerValue;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
        }

        public string Key { get; }
        public BattleDiagnosticDefinitionMetadataValueKind ValueKind { get; }
        public string StringValue { get; }
        public long IntegerValue { get; }
        public double NumberValue { get; }
        public bool BooleanValue { get; }

        public static BattleDiagnosticDefinitionMetadataEntry String(string key, string value) =>
            new BattleDiagnosticDefinitionMetadataEntry(
                key,
                BattleDiagnosticDefinitionMetadataValueKind.String,
                value,
                0L,
                0d,
                false);

        public static BattleDiagnosticDefinitionMetadataEntry Integer(string key, long value) =>
            new BattleDiagnosticDefinitionMetadataEntry(
                key,
                BattleDiagnosticDefinitionMetadataValueKind.Integer,
                string.Empty,
                value,
                0d,
                false);

        public static BattleDiagnosticDefinitionMetadataEntry Number(string key, double value) =>
            new BattleDiagnosticDefinitionMetadataEntry(
                key,
                BattleDiagnosticDefinitionMetadataValueKind.Number,
                string.Empty,
                0L,
                value,
                false);

        public static BattleDiagnosticDefinitionMetadataEntry Boolean(string key, bool value) =>
            new BattleDiagnosticDefinitionMetadataEntry(
                key,
                BattleDiagnosticDefinitionMetadataValueKind.Boolean,
                string.Empty,
                0L,
                0d,
                value);
    }

    [Serializable]
    public sealed class BattleDiagnosticDefinition
    {
        public BattleDiagnosticDefinition(
            in BattleDiagnosticDefinitionReference reference,
            string displayName,
            string revision,
            string contentHash,
            string sourcePath,
            BattleDiagnosticDefinitionResolution resolution,
            IList<BattleDiagnosticDefinitionMetadataEntry> metadata = null)
        {
            if (!reference.HasDefinitionId) throw new ArgumentException(
                "A definition reference with an ID is required.",
                nameof(reference));
            if (!Enum.IsDefined(typeof(BattleDiagnosticDefinitionResolution), resolution) ||
                resolution == BattleDiagnosticDefinitionResolution.Unknown)
                throw new ArgumentOutOfRangeException(nameof(resolution));

            Reference = reference;
            DisplayName = displayName ?? string.Empty;
            Revision = revision ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Resolution = resolution;
            Metadata = new ReadOnlyCollection<BattleDiagnosticDefinitionMetadataEntry>(
                metadata == null
                    ? new List<BattleDiagnosticDefinitionMetadataEntry>()
                    : new List<BattleDiagnosticDefinitionMetadataEntry>(metadata));
        }

        public BattleDiagnosticDefinitionReference Reference { get; }
        public BattleDiagnosticDefinitionKind Kind => Reference.Kind;
        public int DefinitionId => Reference.DefinitionId;
        public string DisplayName { get; }
        public string Revision { get; }
        public string ContentHash { get; }
        public string SourcePath { get; }
        public BattleDiagnosticDefinitionResolution Resolution { get; }
        public IReadOnlyList<BattleDiagnosticDefinitionMetadataEntry> Metadata { get; }
        public bool IsResolved => Resolution == BattleDiagnosticDefinitionResolution.Resolved;

        public static BattleDiagnosticDefinition Unresolved(
            in BattleDiagnosticDefinitionReference reference)
        {
            return new BattleDiagnosticDefinition(
                in reference,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                BattleDiagnosticDefinitionResolution.Unresolved);
        }
    }

    [Serializable]
    public sealed class BattleDiagnosticDefinitionCatalogSnapshot
    {
        public BattleDiagnosticDefinitionCatalogSnapshot(
            BattleDiagnosticSessionScope scope,
            long revision,
            IList<BattleDiagnosticDefinition> items)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid scope is required.", nameof(scope));
            if (revision < 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            Scope = scope;
            Revision = revision;
            Items = new ReadOnlyCollection<BattleDiagnosticDefinition>(
                items == null
                    ? new List<BattleDiagnosticDefinition>()
                    : new List<BattleDiagnosticDefinition>(items));

            var unresolvedCount = 0;
            var references = new HashSet<BattleDiagnosticDefinitionReference>();
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i] == null) throw new ArgumentException(
                    "Definition catalog items cannot contain null.",
                    nameof(items));
                if (!references.Add(Items[i].Reference)) throw new ArgumentException(
                    "Definition catalog items must have unique kind and ID references.",
                    nameof(items));
                if (!Items[i].IsResolved) unresolvedCount++;
            }
            UnresolvedCount = unresolvedCount;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision { get; }
        public IReadOnlyList<BattleDiagnosticDefinition> Items { get; }
        public int UnresolvedCount { get; }
        public bool IsComplete => UnresolvedCount == 0;

        public bool TryResolve(
            in BattleDiagnosticDefinitionReference reference,
            out BattleDiagnosticDefinition definition)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                var candidate = Items[i];
                if (candidate.Reference != reference) continue;
                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }

        public static BattleDiagnosticDefinitionCatalogSnapshot Empty(
            BattleDiagnosticSessionScope scope)
        {
            return new BattleDiagnosticDefinitionCatalogSnapshot(
                scope,
                0L,
                Array.Empty<BattleDiagnosticDefinition>());
        }
    }
}
