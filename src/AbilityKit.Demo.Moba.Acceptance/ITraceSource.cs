using System.IO;
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
}
