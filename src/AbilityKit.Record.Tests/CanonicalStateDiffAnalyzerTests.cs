using System.Text.Json;
using AbilityKit.Core.Recording.FrameRecord;
using Xunit;

namespace AbilityKit.Record.Tests;

public sealed class CanonicalStateDiffAnalyzerTests
{
    [Fact]
    public void CompareFindsFirstFixedRawDifferenceInStableKeyOrder()
    {
        var left = State(
            Entry(frame: 5, entity: 9, component: 4, field: 2, CanonicalValueKind.FixedRaw, "9223372036854775806"),
            Entry(frame: 5, entity: 3, component: 4, field: 1, CanonicalValueKind.FixedRaw, "10"));
        var right = State(
            Entry(frame: 5, entity: 9, component: 4, field: 2, CanonicalValueKind.FixedRaw, "9223372036854775807"),
            Entry(frame: 5, entity: 3, component: 4, field: 1, CanonicalValueKind.FixedRaw, "10"));

        var report = new CanonicalStateDiffAnalyzer().Compare(left, right);

        Assert.Equal(CanonicalStateDiffStatus.Diverged, report.Status);
        Assert.False(report.Matched);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(CanonicalStateDifferenceKind.ValueChanged, difference.Kind);
        Assert.Equal(9, difference.EntityId);
        Assert.Equal(2, difference.FieldId);
        Assert.Equal("9223372036854775806", difference.Left!.RawValue);
        Assert.Equal("9223372036854775807", difference.Right!.RawValue);
    }

    [Fact]
    public void CompareReportsMissingEntriesAndBoundsReportSize()
    {
        var left = State(Entry(1, 1, 1, 1, CanonicalValueKind.Signed, "1"));
        var right = State(
            Entry(1, 1, 1, 1, CanonicalValueKind.Signed, "1"),
            Entry(1, 2, 1, 1, CanonicalValueKind.Signed, "2"),
            Entry(1, 3, 1, 1, CanonicalValueKind.Signed, "3"));

        var report = new CanonicalStateDiffAnalyzer().Compare(
            left, right, new CanonicalStateDiffOptions { MaxDifferences = 1 });

        Assert.Equal(2, report.DifferenceCount);
        Assert.True(report.DifferencesTruncated);
        Assert.Equal(CanonicalStateDifferenceKind.MissingLeft, Assert.Single(report.Differences).Kind);
    }

    [Fact]
    public void CompareRejectsDuplicateKeysInvalidRawValuesAndIncompatibleSchemas()
    {
        var duplicate = Entry(1, 1, 1, 1, CanonicalValueKind.FixedRaw, "1");
        var duplicateReport = new CanonicalStateDiffAnalyzer().Compare(State(duplicate, duplicate), State());
        Assert.Equal(CanonicalStateDiffStatus.InvalidInput, duplicateReport.Status);

        var invalidReport = new CanonicalStateDiffAnalyzer().Compare(
            State(Entry(1, 1, 1, 1, CanonicalValueKind.FixedRaw, "1.25")), State());
        Assert.Equal(CanonicalStateDiffStatus.InvalidInput, invalidReport.Status);

        var schemaMismatch = new CanonicalStateDiffAnalyzer().Compare(
            State(), new CanonicalStateFile { SchemaVersion = 2, WorldId = "world-1" });
        Assert.Equal(CanonicalStateDiffStatus.SchemaMismatch, schemaMismatch.Status);
    }

    [Fact]
    public void JsonRoundTripPreservesRawInt64AsString()
    {
        var state = State(Entry(1, 2, 3, 4, CanonicalValueKind.FixedRaw, long.MinValue.ToString()));
        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<CanonicalStateFile>(json)!;

        Assert.Equal(long.MinValue.ToString(), Assert.Single(restored.Entries).RawValue);
    }

    private static CanonicalStateFile State(params CanonicalStateEntry[] entries) => new()
    {
        SchemaVersion = 1,
        WorldId = "world-1",
        Entries = entries.ToList(),
    };

    private static CanonicalStateEntry Entry(
        int frame,
        long entity,
        int component,
        int field,
        CanonicalValueKind kind,
        string raw) => new()
    {
        Frame = frame,
        PhaseId = 1,
        EntityId = entity,
        ComponentId = component,
        FieldId = field,
        ComponentName = "Transform",
        FieldName = "PositionXRaw",
        ValueKind = kind,
        RawValue = raw,
    };
}
