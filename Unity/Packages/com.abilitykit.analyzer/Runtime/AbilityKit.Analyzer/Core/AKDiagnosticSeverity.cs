/// <summary>
/// 文件名称: AKDiagnosticSeverity.cs
/// 
/// 功能描述: 定义诊断消息的严重级别枚举，用于区分不同重要程度的代码问题。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

namespace AbilityKit.Analyzer
{
/// <summary>
/// 诊断消息的严重级别枚举。
/// </summary>
public enum AKDiagnosticSeverity
{
    /// <summary>错误级别，阻止编译成功</summary>
    Error = 0,

    /// <summary>警告级别，不阻止编译但建议修复</summary>
    Warning = 1,

    /// <summary>信息级别，仅供参考</summary>
    Info = 2,

    /// <summary>隐藏级别，IDE 内部使用</summary>
    Hidden = 3
}
}