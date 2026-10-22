#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Recording.FrameRecord
{
    public enum CanonicalValueKind
    {
        Signed = 0,
        Unsigned = 1,
        FixedRaw = 2,
        Bool = 3,
        Enum = 4,
        Bytes = 5,
        Text = 6,
    }

    [Serializable]
    public sealed class CanonicalStateFile
    {
        public int SchemaVersion { get; set; } = 1;
        public string WorldId { get; set; } = string.Empty;
        public string BuildId { get; set; } = string.Empty;
        public string ConfigHash { get; set; } = string.Empty;
        public List<CanonicalStateEntry> Entries { get; set; } = new List<CanonicalStateEntry>();
    }

    [Serializable]
    public sealed class CanonicalStateEntry
    {
        public int Frame { get; set; }
        public int PhaseId { get; set; }
        public long EntityId { get; set; }
        public int ComponentId { get; set; }
        public int FieldId { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public CanonicalValueKind ValueKind { get; set; }
        public string RawValue { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
    }

    public enum CanonicalStateDiffStatus
    {
        Identical = 0,
        Diverged = 1,
        WorldMismatch = 2,
        SchemaMismatch = 3,
        InvalidInput = 4,
    }

    public enum CanonicalStateDifferenceKind
    {
        ValueChanged = 0,
        MissingLeft = 1,
        MissingRight = 2,
    }

    [Serializable]
    public sealed class CanonicalStateDiffOptions
    {
        public int MaxDifferences { get; set; } = 64;
    }

    [Serializable]
    public sealed class CanonicalStateDiffReport
    {
        public int ReportSchemaVersion { get; set; } = 1;
        public CanonicalStateDiffStatus Status { get; set; }
        public bool Matched { get; set; }
        public string Error { get; set; } = string.Empty;
        public string LeftWorldId { get; set; } = string.Empty;
        public string RightWorldId { get; set; } = string.Empty;
        public string LeftBuildId { get; set; } = string.Empty;
        public string RightBuildId { get; set; } = string.Empty;
        public string LeftConfigHash { get; set; } = string.Empty;
        public string RightConfigHash { get; set; } = string.Empty;
        public int LeftSchemaVersion { get; set; }
        public int RightSchemaVersion { get; set; }
        public int LeftEntryCount { get; set; }
        public int RightEntryCount { get; set; }
        public int ComparedEntryCount { get; set; }
        public int DifferenceCount { get; set; }
        public bool DifferencesTruncated { get; set; }
        public List<CanonicalStateDifference> Differences { get; set; } = new List<CanonicalStateDifference>();

        public CanonicalStateDifference? FirstDifference => Differences.Count == 0 ? null : Differences[0];
    }

    [Serializable]
    public sealed class CanonicalStateDifference
    {
        public CanonicalStateDifferenceKind Kind { get; set; }
        public int Frame { get; set; }
        public int PhaseId { get; set; }
        public long EntityId { get; set; }
        public int ComponentId { get; set; }
        public int FieldId { get; set; }
        public CanonicalStateEntry? Left { get; set; }
        public CanonicalStateEntry? Right { get; set; }
    }

    public sealed class CanonicalStateDiffAnalyzer
    {
        public CanonicalStateDiffReport Compare(
            CanonicalStateFile left,
            CanonicalStateFile right,
            CanonicalStateDiffOptions? options = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            var report = new CanonicalStateDiffReport
            {
                LeftWorldId = left.WorldId ?? string.Empty,
                RightWorldId = right.WorldId ?? string.Empty,
                LeftBuildId = left.BuildId ?? string.Empty,
                RightBuildId = right.BuildId ?? string.Empty,
                LeftConfigHash = left.ConfigHash ?? string.Empty,
                RightConfigHash = right.ConfigHash ?? string.Empty,
                LeftSchemaVersion = left.SchemaVersion,
                RightSchemaVersion = right.SchemaVersion,
                LeftEntryCount = left.Entries?.Count ?? 0,
                RightEntryCount = right.Entries?.Count ?? 0,
            };

            if (left.SchemaVersion <= 0 || right.SchemaVersion <= 0)
            {
                report.Status = CanonicalStateDiffStatus.InvalidInput;
                report.Error = "Canonical state schema version must be positive.";
                return report;
            }

            if (left.SchemaVersion != right.SchemaVersion)
            {
                report.Status = CanonicalStateDiffStatus.SchemaMismatch;
                report.Error = $"Canonical state schema versions differ: left={left.SchemaVersion}, right={right.SchemaVersion}.";
                return report;
            }

            if (!string.Equals(report.LeftWorldId, report.RightWorldId, StringComparison.Ordinal))
            {
                report.Status = CanonicalStateDiffStatus.WorldMismatch;
                report.Error = "Canonical state world identifiers differ.";
                return report;
            }

            var maxDifferences = Math.Max(1, options?.MaxDifferences ?? 64);
            string? leftError;
            string? rightError;
            Dictionary<CanonicalStateKey, CanonicalStateEntry> leftIndex;
            Dictionary<CanonicalStateKey, CanonicalStateEntry> rightIndex;
            var leftValid = TryBuildIndex(left.Entries, out leftIndex, out leftError);
            var rightValid = TryBuildIndex(right.Entries, out rightIndex, out rightError);
            if (!leftValid || !rightValid)
            {
                report.Status = CanonicalStateDiffStatus.InvalidInput;
                report.Error = leftError ?? rightError ?? "Canonical state contains invalid entries.";
                return report;
            }

            var keys = new List<CanonicalStateKey>(leftIndex.Count + rightIndex.Count);
            foreach (var key in leftIndex.Keys) keys.Add(key);
            foreach (var key in rightIndex.Keys)
            {
                if (!leftIndex.ContainsKey(key)) keys.Add(key);
            }
            keys.Sort();

            var commonEntryCount = 0;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                leftIndex.TryGetValue(key, out var leftEntry);
                rightIndex.TryGetValue(key, out var rightEntry);
                if (leftEntry != null && rightEntry != null) commonEntryCount++;
                if (leftEntry != null && rightEntry != null &&
                    leftEntry.ValueKind == rightEntry.ValueKind &&
                    string.Equals(leftEntry.RawValue, rightEntry.RawValue, StringComparison.Ordinal))
                {
                    continue;
                }

                report.DifferenceCount++;
                if (report.Differences.Count >= maxDifferences)
                {
                    report.DifferencesTruncated = true;
                    continue;
                }

                report.Differences.Add(new CanonicalStateDifference
                {
                    Kind = leftEntry == null
                        ? CanonicalStateDifferenceKind.MissingLeft
                        : rightEntry == null
                            ? CanonicalStateDifferenceKind.MissingRight
                            : CanonicalStateDifferenceKind.ValueChanged,
                    Frame = key.Frame,
                    PhaseId = key.PhaseId,
                    EntityId = key.EntityId,
                    ComponentId = key.ComponentId,
                    FieldId = key.FieldId,
                    Left = leftEntry,
                    Right = rightEntry,
                });
            }

            report.ComparedEntryCount = commonEntryCount;
            report.Status = report.DifferenceCount == 0
                ? CanonicalStateDiffStatus.Identical
                : CanonicalStateDiffStatus.Diverged;
            report.Matched = report.Status == CanonicalStateDiffStatus.Identical;
            return report;
        }

        private static bool TryBuildIndex(
            List<CanonicalStateEntry>? entries,
            out Dictionary<CanonicalStateKey, CanonicalStateEntry> index,
            out string? error)
        {
            index = new Dictionary<CanonicalStateKey, CanonicalStateEntry>();
            error = null;
            if (entries == null) return true;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    error = $"Canonical state entry {i} is null.";
                    return false;
                }
                if (!Enum.IsDefined(typeof(CanonicalValueKind), entry.ValueKind))
                {
                    error = $"Canonical state entry {i} has unknown value kind {(int)entry.ValueKind}.";
                    return false;
                }
                if (!IsValidRawValue(entry))
                {
                    error = $"Canonical state entry {i} has invalid raw value '{entry.RawValue}'.";
                    return false;
                }

                var key = new CanonicalStateKey(entry);
                if (!index.TryAdd(key, entry))
                {
                    error = $"Canonical state contains duplicate key {key}.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidRawValue(CanonicalStateEntry entry)
        {
            if (entry.RawValue == null) return false;
            if (entry.ValueKind == CanonicalValueKind.FixedRaw ||
                entry.ValueKind == CanonicalValueKind.Signed ||
                entry.ValueKind == CanonicalValueKind.Enum)
            {
                return long.TryParse(entry.RawValue, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) &&
                    string.Equals(entry.RawValue, value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal);
            }
            if (entry.ValueKind == CanonicalValueKind.Unsigned)
            {
                return ulong.TryParse(entry.RawValue, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) &&
                    string.Equals(entry.RawValue, value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal);
            }
            if (entry.ValueKind == CanonicalValueKind.Bool)
            {
                return string.Equals(entry.RawValue, "0", StringComparison.Ordinal) ||
                    string.Equals(entry.RawValue, "1", StringComparison.Ordinal);
            }
            return true;
        }

        private readonly struct CanonicalStateKey : IComparable<CanonicalStateKey>
        {
            public CanonicalStateKey(CanonicalStateEntry entry)
            {
                Frame = entry.Frame;
                PhaseId = entry.PhaseId;
                EntityId = entry.EntityId;
                ComponentId = entry.ComponentId;
                FieldId = entry.FieldId;
            }

            public readonly int Frame;
            public readonly int PhaseId;
            public readonly long EntityId;
            public readonly int ComponentId;
            public readonly int FieldId;

            public int CompareTo(CanonicalStateKey other)
            {
                var result = Frame.CompareTo(other.Frame);
                if (result != 0) return result;
                result = PhaseId.CompareTo(other.PhaseId);
                if (result != 0) return result;
                result = EntityId.CompareTo(other.EntityId);
                if (result != 0) return result;
                result = ComponentId.CompareTo(other.ComponentId);
                return result != 0 ? result : FieldId.CompareTo(other.FieldId);
            }

            public override int GetHashCode() => HashCode.Combine(Frame, PhaseId, EntityId, ComponentId, FieldId);
            public override bool Equals(object? obj) => obj is CanonicalStateKey other && CompareTo(other) == 0;
            public override string ToString() => $"({Frame},{PhaseId},{EntityId},{ComponentId},{FieldId})";
        }
    }
}
