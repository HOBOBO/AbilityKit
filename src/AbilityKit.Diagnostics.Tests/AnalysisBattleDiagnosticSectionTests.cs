using AbilityKit.Diagnostics.Analysis;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// 战斗诊断区段的 DTO 契约：Schema 版本、轨道默认帧（-1 表示"尚未观测/未结束"）
/// 与集合字段的非空默认值。
/// </summary>
public sealed class AnalysisBattleDiagnosticSectionTests
{
    [Fact]
    public void Schema_version_constant_is_stable()
    {
        Assert.Equal("abilitykit-battle-diagnostics.v1", AnalysisBattleDiagnosticSchema.Version);
    }

    [Fact]
    public void Section_defaults_the_schema_version_and_all_tracks()
    {
        var section = new AnalysisBattleDiagnosticSection();

        Assert.Equal(AnalysisBattleDiagnosticSchema.Version, section.SchemaVersion);
        Assert.Equal(0L, section.CapturedAtTimestamp);
        Assert.NotNull(section.Session);
        Assert.NotNull(section.Events);
        Assert.NotNull(section.State);
        Assert.NotNull(section.Trace);
        Assert.NotNull(section.Attributes);
        Assert.NotNull(section.Buffs);
        Assert.NotNull(section.Tags);
        Assert.NotNull(section.Effects);
        Assert.Empty(section.Events.Items);
        Assert.NotNull(section.Events.Metrics);
        Assert.Empty(section.State.Actors);
        Assert.Null(section.State.World); // 世界快照在观测前为空
    }

    [Fact]
    public void Open_ended_tracks_default_their_frame_to_minus_one()
    {
        Assert.Equal(-1, new AnalysisBattleDiagnosticStateTrack().Frame);
        Assert.Equal(-1, new AnalysisBattleDiagnosticAttributeTrack().Frame);
        Assert.Equal(-1, new AnalysisBattleDiagnosticBuffTrack().Frame);
        Assert.Equal(-1, new AnalysisBattleDiagnosticTagTrack().Frame);
        Assert.Equal(-1, new AnalysisBattleDiagnosticEffectTrack().Frame);
        Assert.Equal(-1, new AnalysisBattleDiagnosticTraceNode().EndFrame);
        Assert.Equal(0, new AnalysisBattleDiagnosticTraceNode().StartFrame);
        Assert.Equal(string.Empty, new AnalysisBattleDiagnosticTraceNode().EndReason);
    }

    [Fact]
    public void Event_defaults_are_zeroed_and_the_payload_is_optional()
    {
        var evt = new AnalysisBattleDiagnosticEvent();

        Assert.Equal(0, evt.Frame);
        Assert.Equal(0L, evt.Sequence);
        Assert.Equal(0L, evt.MonotonicTimestamp);
        Assert.Equal(0, evt.Kind);
        Assert.Equal(0, evt.Channel);
        Assert.Equal(0, evt.Outcome);
        Assert.Equal(string.Empty, evt.Summary);
        Assert.Null(evt.Payload);
    }
}
