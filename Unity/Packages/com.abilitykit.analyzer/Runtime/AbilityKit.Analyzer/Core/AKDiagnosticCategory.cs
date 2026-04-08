/// <summary>
/// 文件名称: AKDiagnosticCategory.cs
/// 
/// 功能描述: 定义诊断消息的分类枚举，用于组织和归类不同类型的代码分析结果。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

namespace AbilityKit.Analyzer
{
/// <summary>
/// 诊断消息的分类枚举。
/// </summary>
public enum AKDiagnosticCategory
{
    /// <summary>未分类（默认值）</summary>
    None = 0,

    /// <summary>代码质量问题</summary>
    CodeQuality = 1,

    /// <summary>命名规范违反</summary>
    Naming = 2,

    /// <summary>设计模式问题</summary>
    Design = 3,

    /// <summary>性能相关问题</summary>
    Performance = 4,

    /// <summary>安全问题</summary>
    Security = 5,

    /// <summary>可维护性问题</summary>
    Maintainability = 6,

    /// <summary>Unity 相关问题</summary>
    Unity = 7,

    /// <summary>AbilityKit 框架使用规范</summary>
    Framework = 8,

    /// <summary>生成代码问题</summary>
    GeneratedCode = 9
}
}