namespace AbilityKit.Entitas.CodeGen.Diagnostics;

/// <summary>进程退出码约定（gate/CI 依赖，勿改数值）。</summary>
public static class ExitCodes
{
    /// <summary>成功：生成/校验/对比全部一致。</summary>
    public const int Ok = 0;

    /// <summary>对比或 pin 对账存在差异（漂移）。</summary>
    public const int DriftOrDiff = 1;

    /// <summary>用法/配置错误（参数、schema、路径不存在）。</summary>
    public const int UsageOrConfig = 2;

    /// <summary>源码校验失败（组件书写不合规、类型解析失败）。</summary>
    public const int SourceValidation = 3;

    /// <summary>未捕获内部错误。</summary>
    public const int Internal = 4;
}

/// <summary>结构化错误：携带退出码，由顶层统一打印，不再让异常栈直接漏给用户。</summary>
public sealed class CodeGenException(string message, int exitCode, Exception? inner = null)
    : Exception(message, inner)
{
    public int ExitCode { get; } = exitCode;
}

/// <summary>控制台输出门面（--verbose 可控；测试里用捕获实现断言输出）。</summary>
public interface IConsoleSink
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class RealConsoleSink : IConsoleSink
{
    public void Info(string message) => Console.WriteLine(message);
    public void Warn(string message) => Console.WriteLine("[warn] " + message);
    public void Error(string message) => Console.Error.WriteLine("[error] " + message);
}

public sealed class CapturingConsoleSink : IConsoleSink
{
    public List<string> Lines { get; } = new();
    public void Info(string message) => Lines.Add(message);
    public void Warn(string message) => Lines.Add("[warn] " + message);
    public void Error(string message) => Lines.Add("[error] " + message);
}
