namespace AbilityKit.Entitas.CodeGen.Model;

/// <summary>context 及其按 pin 顺序排列的组件视图。</summary>
public sealed class ContextInfo(string name)
{
    public string Name { get; } = name;
    public List<PinnedComponent> Components { get; } = new();
}

/// <summary>pin 条目 + 对应组件定义（generate:false 的条目没有下游消费方，但仍参与漂移对账）。</summary>
public sealed class PinnedComponent(string name, bool custom, bool generate, bool serialize, string? coverage, ComponentInfo? definition)
{
    public string Name { get; } = name;
    public bool Custom { get; } = custom;
    public bool Generate { get; } = generate;

    /// <summary>参与生成的逐字段序列化（Write/Restore 覆盖集）。</summary>
    public bool Serialize { get; } = serialize;

    /// <summary>回滚覆盖归类（serialize:true 时归一为 "serialized"）。</summary>
    public string Coverage { get; } = serialize ? "serialized" : coverage ?? "";

    public ComponentInfo? Definition { get; } = definition;
}

/// <summary>
/// 管线数据模型（数据提供者阶段的产物，生成器只读它）。
/// 生成器与扫描/解析解耦：未来换 Roslyn 扫描器或加新生成器都不动这一层。
/// </summary>
public sealed class CodeGenModel
{
    public required List<ContextInfo> Contexts { get; init; }

    /// <summary>编辑器专属代码的守卫宏（如 UNITY_EDITOR）与开关已并入此处，生成器统一从模型读取。</summary>
    public required ModelEditorOptions Editor { get; init; }

    /// <summary>手工维护的生成物（custom 组件的 API 文件）：不生成、写盘不清理、对比做存在性校验。</summary>
    public required List<string> PreservedFiles { get; init; }

    /// <summary>可序列化普通类型（嵌套数据类/枚举，fqn → 字段清单），供序列化生成器展开。</summary>
    public required Dictionary<string, PlainTypeInfo> SerializablePlainTypes { get; init; }

    /// <summary>生成物头部署名策略："entitas-parity" | "abilitykit"。</summary>
    public required string Attribution { get; init; }

    /// <summary>按 context 名取组件（生成器用）。</summary>
    public ContextInfo Context(string name) =>
        Contexts.FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException($"模型中不存在 context: {name}");
}

/// <summary>普通类型信息：kind（class/struct/enum）+ 已解析字段。枚举无字段。</summary>
public sealed class PlainTypeInfo(string kind, List<FieldInfo> fields)
{
    public string Kind { get; } = kind;
    public List<FieldInfo> Fields { get; } = fields;
    public bool IsEnum => Kind == "enum";
}

/// <summary>编辑器专属生成物的开关与守卫宏（来自配置 editor 段，默认复刻现状）。</summary>
public sealed record ModelEditorOptions
{
    /// <summary>是否生成 Contexts.cs 的可视化调试 observer partial（引用 UnityEngine，需宏守卫）。</summary>
    public bool EmitContextObserver { get; init; } = true;

    /// <summary>是否生成 Feature.cs（可视化调试基类）。</summary>
    public bool EmitFeatureClass { get; init; } = true;

    /// <summary>守卫宏列表（默认 UNITY_EDITOR）。纯服务端/生产目标可换自定义宏或直接关掉 emit*。</summary>
    public List<string> DefineSymbols { get; init; } = new() { "UNITY_EDITOR" };

    public string GuardExpression => string.Join(" && ", DefineSymbols);
}

/// <summary>生成器署名解析：entitas-parity 保持历史署名字节级复刻；abilitykit 用于接入替换之后。</summary>
public static class HeaderNames
{
    public static string Resolve(CodeGenModel model, string shortName, string parityFullName) =>
        model.Attribution == "abilitykit"
            ? $"AbilityKit.Entitas.CodeGen.{shortName}Generator"
            : parityFullName;
}
