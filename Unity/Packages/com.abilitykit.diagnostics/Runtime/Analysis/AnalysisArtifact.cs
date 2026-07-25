using System;
using System.Collections.Generic;

namespace AbilityKit.Diagnostics.Analysis
{
    /// <summary>
    /// 面向 Web 分析产物的稳定 Schema 常量。
    /// </summary>
    public static class AbilityKitAnalysisSchema
    {
        public const string Version = "abilitykit-analysis.v1";
        public const string DefaultProducer = "AbilityKit.Diagnostics";
    }

    /// <summary>
    /// 供 Web 工具消费的根文档。该 DTO 需保持低依赖，确保任意项目都能构建。
    /// </summary>
    public sealed class AbilityKitAnalysisArtifact
    {
        public string SchemaVersion { get; set; } = AbilityKitAnalysisSchema.Version;
        public AnalysisSessionInfo Session { get; set; } = new AnalysisSessionInfo();
        public AnalysisTimeAxis Time { get; set; } = new AnalysisTimeAxis();
        public AnalysisDictionaries Dictionaries { get; set; } = new AnalysisDictionaries();
        public AnalysisProfilerSection Profiler { get; set; } = new AnalysisProfilerSection();
        public AnalysisTraceSection Trace { get; set; } = new AnalysisTraceSection();
        public AnalysisBattleDiagnosticSection BattleDiagnostics { get; set; }
        public AnalysisDiagnosticsSection Diagnostics { get; set; } = new AnalysisDiagnosticsSection();
        public AnalysisRuntimeSection Runtime { get; set; } = new AnalysisRuntimeSection();
        public AnalysisInsightsSection Insights { get; set; } = new AnalysisInsightsSection();
        public AnalysisThresholdProfile ThresholdProfile { get; set; } = new AnalysisThresholdProfile();
        public AnalysisBaselineSection Baseline { get; set; } = new AnalysisBaselineSection();
        public List<AnalysisKeyValue> Metadata { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisSessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public string Producer { get; set; } = AbilityKitAnalysisSchema.DefaultProducer;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public long GeneratedAtUnixMs { get; set; }
        public string UnityVersion { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public sealed class AnalysisTimeAxis
    {
        public long StartUnixMs { get; set; }
        public long EndUnixMs { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public double DurationMs { get; set; }
        public string Clock { get; set; } = string.Empty;
    }

    public sealed class AnalysisDictionaries
    {
        public List<AnalysisDictionaryEntry> TraceKinds { get; set; } = new List<AnalysisDictionaryEntry>();
        public List<AnalysisDictionaryEntry> EndReasons { get; set; } = new List<AnalysisDictionaryEntry>();
        public List<AnalysisDictionaryEntry> MetricKinds { get; set; } = new List<AnalysisDictionaryEntry>();
        public List<AnalysisMetricCatalogEntry> MetricCatalog { get; set; } = new List<AnalysisMetricCatalogEntry>();
        public List<AnalysisDictionaryEntry> DiagnosticCategories { get; set; } = new List<AnalysisDictionaryEntry>();
    }

    public sealed class AnalysisDictionaryEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class AnalysisProfilerSection
    {
        public string SessionId { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public double DurationMs { get; set; }
        public List<AnalysisMetricDefinition> Metrics { get; set; } = new List<AnalysisMetricDefinition>();
        public List<AnalysisCounterRecord> Counters { get; set; } = new List<AnalysisCounterRecord>();
        public List<AnalysisGaugeRecord> Gauges { get; set; } = new List<AnalysisGaugeRecord>();
        public List<AnalysisSampleSummary> Samples { get; set; } = new List<AnalysisSampleSummary>();
        public List<AnalysisRateRecord> Rates { get; set; } = new List<AnalysisRateRecord>();
        public List<AnalysisDurationSummary> Durations { get; set; } = new List<AnalysisDurationSummary>();
        public List<AnalysisProfilerEvent> Events { get; set; } = new List<AnalysisProfilerEvent>();
        public List<AnalysisFlameNode> Flame { get; set; } = new List<AnalysisFlameNode>();
    }

    public sealed class AnalysisMetricDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class AnalysisMetricCatalogEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Stability { get; set; } = string.Empty;
        public string Sampling { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public List<string> Dimensions { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisCounterRecord
    {
        public string Name { get; set; } = string.Empty;
        public long Value { get; set; }
        public long Delta { get; set; }
        public long SampleCount { get; set; }
    }

    public sealed class AnalysisGaugeRecord
    {
        public string Name { get; set; } = string.Empty;
        public long Value { get; set; }
        public long Timestamp { get; set; }
    }

    public sealed class AnalysisSampleSummary
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Sum { get; set; }
        public double Mean { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }

    public sealed class AnalysisRateRecord
    {
        public string Name { get; set; } = string.Empty;
        public long Total { get; set; }
        public long Count1Second { get; set; }
        public long Count5Seconds { get; set; }
        public long Count60Seconds { get; set; }
        public long PeakPerSecond { get; set; }
    }

    public sealed class AnalysisDurationSummary
    {
        public string Name { get; set; } = string.Empty;
        public long Count { get; set; }
        public double SumMs { get; set; }
        public double MeanMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
    }

    public sealed class AnalysisProfilerEvent
    {
        public long Timestamp { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
    }

    public sealed class AnalysisFlameNode
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double TotalMs { get; set; }
        public double SelfMs { get; set; }
        public int Hits { get; set; }
        public List<AnalysisFlameNode> Children { get; set; } = new List<AnalysisFlameNode>();
    }

    public sealed class AnalysisTraceSection
    {
        public List<AnalysisTraceRoot> Roots { get; set; } = new List<AnalysisTraceRoot>();
        public List<AnalysisTraceEdge> Edges { get; set; } = new List<AnalysisTraceEdge>();
        public bool Truncated { get; set; }
    }

    public sealed class AnalysisTraceRoot
    {
        public long RootId { get; set; }
        public bool Truncated { get; set; }
        public List<AnalysisTraceNode> Nodes { get; set; } = new List<AnalysisTraceNode>();
    }

    public sealed class AnalysisTraceNode
    {
        public long ContextId { get; set; }
        public long RootId { get; set; }
        public long ParentId { get; set; }
        public int Kind { get; set; }
        public string KindName { get; set; } = string.Empty;
        public int EndedFrame { get; set; }
        public int EndReason { get; set; }
        public string EndReasonName { get; set; } = string.Empty;
        public int ChildCount { get; set; }
        public bool IsRoot { get; set; }
        public bool IsEnded { get; set; }
        public AnalysisTraceMetadata Metadata { get; set; } = new AnalysisTraceMetadata();
    }

    public sealed class AnalysisTraceMetadata
    {
        public int ConfigId { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public long SourceId { get; set; }
        public long TargetId { get; set; }
        public long OriginSourceId { get; set; }
        public long OriginTargetId { get; set; }
        public string OriginSource { get; set; } = string.Empty;
        public string OriginTarget { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public List<AnalysisKeyValue> Properties { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisTraceEdge
    {
        public string Kind { get; set; } = string.Empty;
        public long FromContextId { get; set; }
        public long ToContextId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class AnalysisDiagnosticsSection
    {
        public List<AnalysisDiagnosticRecord> Warnings { get; set; } = new List<AnalysisDiagnosticRecord>();
        public List<AnalysisDiagnosticRecord> Exceptions { get; set; } = new List<AnalysisDiagnosticRecord>();
        public List<AnalysisDiagnosticAggregate> Aggregates { get; set; } = new List<AnalysisDiagnosticAggregate>();
    }

    public sealed class AnalysisDiagnosticRecord
    {
        public string Key { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool SuppressedAtLimit { get; set; }
        public AnalysisRuntimeCorrelation Correlation { get; set; } = new AnalysisRuntimeCorrelation();
    }

    public sealed class AnalysisDiagnosticAggregate
    {
        public string Name { get; set; } = string.Empty;
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisRuntimeSection
    {
        public List<AnalysisRuntimeRecord> Records { get; set; } = new List<AnalysisRuntimeRecord>();
    }

    public sealed class AnalysisRuntimeRecord
    {
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AnalysisRuntimeCorrelation Correlation { get; set; } = new AnalysisRuntimeCorrelation();
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisRuntimeCorrelation
    {
        public long RootContextId { get; set; }
        public long SourceContextId { get; set; }
        public long RuntimeId { get; set; }
        public long RuntimeRootContextId { get; set; }
        public int ActorId { get; set; }
        public int SkillId { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    public sealed class AnalysisInsightsSection
    {
        public List<AnalysisInsightRecord> Records { get; set; } = new List<AnalysisInsightRecord>();
        public List<AnalysisRiskRecord> Risks { get; set; } = new List<AnalysisRiskRecord>();
        public List<AnalysisRanking> Rankings { get; set; } = new List<AnalysisRanking>();
    }

    public sealed class AnalysisInsightRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public double Score { get; set; }
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisRiskRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public AnalysisRuntimeCorrelation Correlation { get; set; } = new AnalysisRuntimeCorrelation();
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisRanking
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public List<AnalysisRankingItem> Items { get; set; } = new List<AnalysisRankingItem>();
    }

    public sealed class AnalysisRankingItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public AnalysisRuntimeCorrelation Correlation { get; set; } = new AnalysisRuntimeCorrelation();
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisThresholdProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public List<AnalysisThresholdRule> Rules { get; set; } = new List<AnalysisThresholdRule>();
        public List<AnalysisThresholdEvaluation> Evaluations { get; set; } = new List<AnalysisThresholdEvaluation>();
        public List<AnalysisKeyValue> Metadata { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisThresholdRule
    {
        public string Id { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Dimensions { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisThresholdEvaluation
    {
        public string RuleId { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Actual { get; set; }
        public double Expected { get; set; }
        public double Delta { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisBaselineSection
    {
        public string BaselineId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string ComparedAtUtc { get; set; } = string.Empty;
        public List<AnalysisBaselineMetricComparison> Metrics { get; set; } = new List<AnalysisBaselineMetricComparison>();
        public List<AnalysisKeyValue> Metadata { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisBaselineMetricComparison
    {
        public string Metric { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Current { get; set; }
        public double Baseline { get; set; }
        public double Delta { get; set; }
        public double DeltaPercent { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<AnalysisKeyValue> Values { get; set; } = new List<AnalysisKeyValue>();
    }

    public sealed class AnalysisKeyValue
    {
        public AnalysisKeyValue()
        {
        }

        public AnalysisKeyValue(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
