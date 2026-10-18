using AbilityKit.Entitas.CodeGen.Diagnostics;

namespace AbilityKit.Entitas.CodeGen.Configuration;

/// <summary>
/// 生成配置（schema 2）。每个 consuming 项目一份（约定命名 tools/entitas-codegen.&lt;project&gt;.json，
/// 也可用 --config 显式指定任意路径）。路径相对仓库根（repoRoot = 从配置文件位置向上找到的 .git 根）。
/// 严格反序列化：未知键报错，防止配置拼错被静默忽略。
/// </summary>
public sealed class CodeGenConfig
{
    public const int CurrentSchema = 2;

    public int Schema { get; set; }

    /// <summary>项目名（仅用于日志与报错定位；--project 发现约定也依赖它）。</summary>
    public string Project { get; set; } = "";

    public SourcesSection Sources { get; set; } = new();
    public TypeIndexSection TypeIndex { get; set; } = new();
    public OutputSection Output { get; set; } = new();
    public EditorSection? Editor { get; set; }

    /// <summary>
    /// 组件源扫描器："regex"（默认，零依赖、快）| "roslyn"（语义级 FQN 解析，CLI 内使用 Microsoft.CodeAnalysis）。
    /// 两者经 parity 测试保证生成物一致。
    /// </summary>
    public string Scanner { get; set; } = "regex";

    /// <summary>
    /// 组件序列化策略（业务层选项——不是每个 Entitas 项目都需要 ECS 回滚序列化）：
    /// "off"（默认，通用中立：不生成序列化、无覆盖要求）| "opt-in"（pin 里 serialize:true 主动加入）|
    /// "auto"（默认全量+豁免制：凡可序列化组件自动入集，不入集必须用 coverage 声明豁免理由）。
    /// </summary>
    public SerializationSection? Serialization { get; set; }

    /// <summary>context 列表；顺序决定 Contexts.cs 生成顺序。</summary>
    public List<string> Contexts { get; set; } = new();

    /// <summary>组件索引锁定文件（相对仓库根）。</summary>
    public string Pin { get; set; } = "";

    /// <summary>
    /// 生成基线清单（相对仓库根，path→内容哈希）。配置了它，默认校验就走基线（生成器自洽），
    /// 示例包目录对比降级为 --diff-generated-dir 按需报告；未配置则沿用直接对比 generatedDir。
    /// </summary>
    public string? Baseline { get; set; }

    public sealed class SourcesSection
    {
        /// <summary>组件定义源目录（扫描 [Context] IComponent 类）。可多目录。</summary>
        public List<string> Components { get; set; } = new();
    }

    public sealed class TypeIndexSection
    {
        /// <summary>类型索引扫描根（把字段类型短名解析为全限定名）。</summary>
        public List<string> Roots { get; set; } = new();

        /// <summary>扫描时跳过的目录名（缺省见 <see cref="Defaults.SkipDirs"/>）。</summary>
        public List<string>? SkipDirs { get; set; }
    }

    public sealed class OutputSection
    {
        /// <summary>既有生成物目录：check 模式的对比基准；--write 的默认目标由 --out 决定，绝不默认写这里。</summary>
        public string GeneratedDir { get; set; } = "";

        /// <summary>"utf8-bom"（默认，与历史产物一致）| "utf8"。</summary>
        public string Encoding { get; set; } = "utf8-bom";

        /// <summary>"crlf"（默认，与历史产物一致）| "lf"。</summary>
        public string LineEndings { get; set; } = "crlf";

        /// <summary>
        /// "abilitykit"（默认）：署名为本工具——生成器定义规范形态，不迁就历史手改；
        /// "entitas-parity"：保留 Entitas.CodeGeneration.Plugins.* 署名（仅历史参照）。
        /// </summary>
        public string Attribution { get; set; } = "abilitykit";

        /// <summary>写盘时删除生成集中不存在、目标目录中存在的生成文件（含对应 .meta）。接入替换时才开。</summary>
        public bool CleanStale { get; set; }
    }

    public sealed class SerializationSection
    {
        /// <summary>"off" | "opt-in" | "auto"。</summary>
        public string Mode { get; set; } = "off";
    }

    /// <summary>
    /// 编辑器专属代码段。缺省 = 复刻现状（observer partial + Feature.cs + UNITY_EDITOR 宏）。
    /// 纯服务端/无 Unity Editor 的生成目标：关掉 emit* 或换成项目自定义宏。
    /// </summary>
    public sealed class EditorSection
    {
        /// <summary>是否生成 Contexts.cs 的可视化调试 observer partial（含 UnityEngine 引用）。</summary>
        public bool EmitContextObserver { get; set; } = true;

        /// <summary>是否生成 Feature.cs。</summary>
        public bool EmitFeatureClass { get; set; } = true;

        /// <summary>守卫宏（默认 ["UNITY_EDITOR"]，多个用 &amp;&amp; 连接进 #if）。</summary>
        public List<string> DefineSymbols { get; set; } = new() { "UNITY_EDITOR" };
    }

    public static class Defaults
    {
        public static readonly string[] SkipDirs = { "obj", "bin", "node_modules", "Library", ".git", "TestResults", "Bee", "Logs", "DotNet~", "Samples", "Samples~" };
    }

    // ---- 归一化与校验 ----

    /// <summary>把缺省段落填成显式默认值（导出有效配置/生成模型用同一形状）。</summary>
    public CodeGenConfig WithDefaultsApplied()
    {
        Editor ??= new EditorSection();
        Serialization ??= new SerializationSection();
        TypeIndex.SkipDirs ??= Defaults.SkipDirs.ToList();
        return this;
    }

    public string SerializationMode => (Serialization ??= new SerializationSection()).Mode;

    public void Validate(string repoRoot, string configPath)
    {
        WithDefaultsApplied();

        var errors = new List<string>();

        if (Schema != CurrentSchema)
            errors.Add($"schema 必须是 {CurrentSchema}（当前 {Schema}）。配置结构变更时同步升版本。");
        if (string.IsNullOrWhiteSpace(Project))
            errors.Add("project 不能为空。");
        if (Contexts.Count == 0)
            errors.Add("contexts 不能为空。");
        if (Contexts.Count != Contexts.Distinct(StringComparer.Ordinal).Count())
            errors.Add("contexts 存在重复项。");
        foreach (var ctx in Contexts)
        {
            if (!IsValidIdentifier(ctx))
                errors.Add($"context 名不是合法 C# 标识符: {ctx}");
        }
        if (Sources.Components.Count == 0)
            errors.Add("sources.components 不能为空。");
        if (TypeIndex.Roots.Count == 0)
            errors.Add("typeIndex.roots 不能为空。");
        if (string.IsNullOrWhiteSpace(Output.GeneratedDir))
            errors.Add("output.generatedDir 不能为空。");
        if (string.IsNullOrWhiteSpace(Pin))
            errors.Add("pin 不能为空。");

        if (Output.Encoding is not ("utf8-bom" or "utf8"))
            errors.Add($"output.encoding 只支持 utf8-bom|utf8，当前: {Output.Encoding}");
        if (Output.LineEndings is not ("crlf" or "lf"))
            errors.Add($"output.lineEndings 只支持 crlf|lf，当前: {Output.LineEndings}");
        if (Output.Attribution is not ("entitas-parity" or "abilitykit"))
            errors.Add($"output.attribution 只支持 entitas-parity|abilitykit，当前: {Output.Attribution}");

        if (Editor!.DefineSymbols.Count == 0)
            errors.Add("editor.defineSymbols 不能为空（关编辑器代码请用 emitContextObserver/emitFeatureClass）。");
        foreach (var sym in Editor.DefineSymbols)
        {
            if (!IsValidIdentifier(sym))
                errors.Add($"editor.defineSymbols 含非法宏标识符: {sym}");
        }

        if (Serialization?.Mode is not ("off" or "opt-in" or "auto"))
            errors.Add($"serialization.mode 只支持 off|opt-in|auto，当前: {Serialization?.Mode}");
        if (Scanner is not ("regex" or "roslyn"))
            errors.Add($"scanner 只支持 regex|roslyn，当前: {Scanner}");

        foreach (var dir in Sources.Components)
        {
            if (!Directory.Exists(Resolve(repoRoot, dir)))
                errors.Add($"sources.components 目录不存在: {dir}");
        }
        foreach (var root in TypeIndex.Roots)
        {
            if (!Directory.Exists(Resolve(repoRoot, root)))
                errors.Add($"typeIndex.roots 目录不存在: {root}");
        }

        if (errors.Count > 0)
        {
            throw new CodeGenException(
                $"配置校验失败（{Path.GetFileName(configPath)}）:\n  " + string.Join("\n  ", errors),
                ExitCodes.UsageOrConfig);
        }
    }

    public static bool IsValidIdentifier(string s) =>
        s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');

    public string Resolve(string repoRoot, string relative) => Path.GetFullPath(Path.Combine(repoRoot, relative));

    public Model.ModelEditorOptions ToEditorOptions() => new()
    {
        EmitContextObserver = Editor!.EmitContextObserver,
        EmitFeatureClass = Editor!.EmitFeatureClass,
        DefineSymbols = Editor!.DefineSymbols.ToList(),
    };
}
