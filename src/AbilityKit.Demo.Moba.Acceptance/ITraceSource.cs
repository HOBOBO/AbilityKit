using System.IO;
using System.Security.Cryptography;
using AbilityKit.Game.Test.UnitTest;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>
/// Trace 源抽象（Seam #2 的 dotnet 侧）。判定器与批量 runner 只认这个接口，
/// 不关心 trace 是「从文件读的」「从 Unity run 捕获的」还是「未来从 dotnet sim 实时取的」。
/// 这是把「判定」与「trace 来源」解耦的关键：现在用 <see cref="FileTraceSource"/> 喂捕获的 jsonl，
/// 未来 Seam #4（sim 去 Unity 化）落地后换一个 live-sim 实现即可，判定层零改动。
/// </summary>
public interface ITraceSource
{
    /// <summary>按 caseId 取观测 trace；无则返回 false（该用例标记 needs-trace）。</summary>
    bool TryGetRecords(string caseId, out MobaAcceptanceTraceRecord[] records);

    /// <summary>
    /// 读取记录和可选的完整性证据；旧来源仍可用于正向断言。
    /// </summary>
    bool TryGetEvidence(string caseId, out MobaAcceptanceTraceRecord[] records, out MobaAcceptanceTraceEvidence? evidence)
    {
        evidence = null;
        return TryGetRecords(caseId, out records);
    }
}

/// <summary>
/// 文件 trace 源：从目录读 <c>&lt;caseId&gt;_trace.jsonl</c>（与生产 <c>MobaAcceptanceTraceExporter</c> 落盘命名一致）。
/// 用于把 Unity/任意 run 产出的 trace.jsonl 喂给 dotnet 判定器做回归。
/// </summary>
public sealed class FileTraceSource : ITraceSource
{
    private readonly string _directory;
    public FileTraceSource(string directory) => _directory = directory;

    public bool TryGetRecords(string caseId, out MobaAcceptanceTraceRecord[] records)
    {
        records = null!;
        if (string.IsNullOrEmpty(caseId)) return false;
        var path = Path.Combine(_directory, caseId + "_trace.jsonl");
        if (!File.Exists(path)) return false;
        records = AcceptanceJsonCodec.LoadTraceRecords(path);
        return true;
    }

    public bool TryGetEvidence(string caseId, out MobaAcceptanceTraceRecord[] records, out MobaAcceptanceTraceEvidence? evidence)
    {
        records = null!;
        evidence = null;
        if (string.IsNullOrEmpty(caseId)) return false;
        var tracePath = Path.Combine(_directory, caseId + "_trace.jsonl");
        if (!File.Exists(tracePath)) return false;
        var traceBytes = File.ReadAllBytes(tracePath);
        using var reader = new StreamReader(new MemoryStream(traceBytes, writable: false));
        records = AcceptanceJsonCodec.ReadTraceRecords(reader, allowPartial: true, out var malformed);
        if (malformed) return true;
        var summaryPath = Path.Combine(_directory, caseId + "_summary.json");
        if (!File.Exists(summaryPath)) return true;
        MobaAcceptanceSummary summary;
        try
        {
            summary = AcceptanceJsonCodec.ParseSummary(File.ReadAllText(summaryPath));
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException)
        {
            return true;
        }
        var candidate = summary.traceEvidence;
        if (summary.caseId != caseId || !MobaAcceptanceTraceEvidence.CanProveAbsence(candidate, records.Length)
            || string.IsNullOrEmpty(candidate.traceSha256)
            || records.Any(record => record.caseId != caseId || record.frame < candidate.startFrame || record.frame > candidate.endFrame))
            return true;
        if (string.Equals(candidate.traceSha256, Convert.ToHexString(SHA256.HashData(traceBytes)), StringComparison.OrdinalIgnoreCase))
            evidence = candidate;
        return true;
    }
}

/// <summary>无 trace 源：所有用例都判 needs-trace。用于「只校验期望可加载、不跑判定」的场合。</summary>
public sealed class NullTraceSource : ITraceSource
{
    public bool TryGetRecords(string caseId, out MobaAcceptanceTraceRecord[] records)
    {
        records = null!;
        return false;
    }
}

/// <summary>
/// 组合 trace 源：按顺序尝试多个源，首个命中即返回。用于「真实 trace 优先，合成 fixture 兜底」：
/// <c>new CompositeTraceSource(new FileTraceSource(Traces 真实), new FileTraceSource(Fixtures 合成))</c>。
/// </summary>
public sealed class CompositeTraceSource(params ITraceSource[] sources) : ITraceSource
{
    public bool TryGetRecords(string caseId, out MobaAcceptanceTraceRecord[] records)
    {
        records = null!;
        foreach (var source in sources)
        {
            if (source is not null && source.TryGetRecords(caseId, out records)) return true;
        }
        return false;
    }

    public bool TryGetEvidence(string caseId, out MobaAcceptanceTraceRecord[] records, out MobaAcceptanceTraceEvidence? evidence)
    {
        records = null!;
        evidence = null;
        foreach (var source in sources)
        {
            if (source is not null && source.TryGetEvidence(caseId, out records, out evidence)) return true;
        }
        return false;
    }
}
