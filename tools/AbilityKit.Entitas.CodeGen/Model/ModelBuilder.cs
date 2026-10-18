using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Scanning;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Model;

/// <summary>模型构建结果：漂移被阻断时 Model 为 null。</summary>
public sealed class ModelBuildResult
{
    public CodeGenModel? Model { get; init; }
    public required List<string> DriftFailures { get; init; }
    public required List<string> Warnings { get; init; }
    public bool HasDrift => DriftFailures.Count > 0;
}

/// <summary>
/// 数据提供者阶段：扫描 → 类型解析 → 源校验 → pin 对账 → 有序模型。
/// 这一层的契约：给生成器一份确定性的、已通过校验的 <see cref="CodeGenModel"/>。
/// </summary>
public static class ModelBuilder
{
    public static ModelBuildResult Build(CodeGenConfig config, string repoRoot, PinFile pin, IConsoleSink sink,
        IComponentSourceScanner? scanner = null)
    {
        var warnings = new List<string>();

        sink.Info($"[1/4] 构建类型索引（roots: {string.Join(", ", config.TypeIndex.Roots)}）…");
        var typeIndex = TypeIndex.Build(
            config.TypeIndex.Roots.Select(r => config.Resolve(repoRoot, r)),
            config.TypeIndex.SkipDirs);
        sink.Info($"      索引完成：{typeIndex.Count} 个短名。");

        sink.Info("[2/4] 扫描组件定义…");
        var sourceDirs = config.Sources.Components.Select(r => config.Resolve(repoRoot, r)).ToList();
        var scannerImpl = scanner ?? (config.Scanner == "roslyn"
            ? new RoslynComponentScanner()
            : (IComponentSourceScanner)new Scanner());
        sink.Info($"      扫描器: {scannerImpl.GetType().Name}");
        var rawComponents = scannerImpl.Scan(sourceDirs);

        // 普通类型（嵌套数据类/枚举）：字段解析后供序列化生成器展开。
        var plainTypes = new Dictionary<string, PlainTypeInfo>(StringComparer.Ordinal);
        foreach (var t in scannerImpl.ScanPlainTypes(sourceDirs))
        {
            var fields = new List<FieldInfo>();
            foreach (var f in t.Fields)
            {
                fields.Add(new FieldInfo(
                    scannerImpl.ProvidesResolvedFieldTypes
                        ? f.TypeText
                        : TypeResolver.Resolve(f.TypeText, t.Namespace, t.Usings, typeIndex, f.SourceFile, f.Line),
                    f.Name));
            }
            var fqn = t.Namespace.Length == 0 ? t.Name : t.Namespace + "." + t.Name;
            plainTypes[fqn] = new PlainTypeInfo(t.Kind, fields);
        }

        var definitions = new List<ComponentInfo>();
        foreach (var raw in rawComponents)
        {
            var contexts = new HashSet<string>(raw.Attributes.Intersect(config.Contexts, StringComparer.Ordinal), StringComparer.Ordinal);
            if (contexts.Count == 0)
            {
                if (raw.Attributes.Any(a => a.StartsWith("DontGenerate", StringComparison.Ordinal)))
                {
                    sink.Info($"      跳过 [DontGenerate]: {raw.Class}（{Path.GetFileName(raw.SourceFile)}）");
                    continue;
                }
                throw new CodeGenException(
                    $"{raw.SourceFile}: IComponent 类 {raw.Class} 缺少 context 属性（期望之一: {string.Join("/", config.Contexts)}；属性需为 [Context名] 短名形式）",
                    ExitCodes.SourceValidation);
            }

            if (!raw.SealedClass)
                warnings.Add($"{raw.Class} 不是 sealed（Entitas 组件约定 sealed，组件池按具体类型回收）：{raw.SourceFile}");
            if (!CodeGenConfig.IsValidIdentifier(raw.ComponentName))
                throw new CodeGenException(
                    $"{raw.SourceFile}: 组件名不是合法 C# 标识符: {raw.ComponentName}", ExitCodes.SourceValidation);

            var fields = new List<FieldInfo>();
            foreach (var f in raw.Fields)
            {
                var fqn = scannerImpl.ProvidesResolvedFieldTypes
                    ? f.TypeText
                    : TypeResolver.Resolve(f.TypeText, raw.Namespace, raw.Usings, typeIndex, f.SourceFile, f.Line);
                fields.Add(new FieldInfo(fqn, f.Name));
            }

            var classFqn = raw.Namespace.Length == 0 ? raw.Class : raw.Namespace + "." + raw.Class;
            definitions.Add(new ComponentInfo(raw.ComponentName, classFqn, fields, contexts, raw.SourceFile));
        }
        sink.Info($"      发现 {definitions.Count} 个组件（{config.Contexts.Count} 个 context）。");

        // ---- pin 对账（双向）----
        var failures = new List<string>();
        foreach (var ctx in config.Contexts)
        {
            var discovered = definitions.Where(d => d.Contexts.Contains(ctx)).ToList();
            var discoveredNames = discovered.Select(d => d.ComponentName).ToHashSet(StringComparer.Ordinal);

            pin.Contexts.TryGetValue(ctx, out var pinCtx);
            var pinned = pinCtx?.Components ?? new List<PinFile.PinComponent>();
            if (pinned.Select(p => p.Name).GroupBy(n => n, StringComparer.Ordinal).Any(g => g.Count() > 1))
                failures.Add($"pin 里 {ctx} 存在重复条目");

            foreach (var p in pinned.Where(p => p.Generate))
            {
                if (!discoveredNames.Contains(p.Name))
                    failures.Add($"pin 里 {ctx}.{p.Name} 已不存在于源码（pin 过期，删掉该条目）");
            }

            foreach (var name in discoveredNames.Except(pinned.Select(p => p.Name)))
            {
                failures.Add($"源码发现 {ctx}.{name} 但 pin 里没有（新增组件未锁定索引；用 --update-pin 追加）");
            }
        }

        if (failures.Count > 0)
        {
            return new ModelBuildResult { Model = null, DriftFailures = failures, Warnings = warnings };
        }

        // ---- 有序模型 + 序列化入集策略（业务选项，三档：off / opt-in / auto）----
        var mode = config.SerializationMode;
        var policyErrors = new List<string>();
        var modelContexts = new List<ContextInfo>();
        var preserved = new List<string>();

        foreach (var ctx in config.Contexts)
        {
            var info = new ContextInfo(ctx);
            var byName = definitions.Where(d => d.Contexts.Contains(ctx))
                .ToDictionary(d => d.ComponentName, StringComparer.Ordinal);
            foreach (var p in (pin.Contexts.TryGetValue(ctx, out var pc) ? pc.Components : new List<PinFile.PinComponent>()))
            {
                if (!p.Generate) continue;
                byName.TryGetValue(p.Name, out var def);
                if (def is null) continue; // 前面对账已拦截

                var hasSerializeFlag = p.Serialize;
                var hasCoverage = !string.IsNullOrWhiteSpace(p.Coverage);

                switch (mode)
                {
                    case "off":
                        if (hasSerializeFlag || hasCoverage)
                            policyErrors.Add($"{ctx}.{p.Name}: serialization.mode=off 时 pin 不应出现 serialize/coverage 字段（本项目未启用序列化）");
                        info.Components.Add(new PinnedComponent(p.Name, p.Custom, p.Generate, false, null, def));
                        break;

                    case "opt-in":
                        if (hasCoverage)
                            policyErrors.Add($"{ctx}.{p.Name}: serialization.mode=opt-in 没有 coverage 概念（豁免制仅 auto 模式）");
                        info.Components.Add(new PinnedComponent(p.Name, p.Custom, p.Generate, p.Serialize, null, def));
                        break;

                    case "auto":
                    {
                        if (hasSerializeFlag)
                            policyErrors.Add($"{ctx}.{p.Name}: auto 模式下 serialize 字段无效（默认全量；不入集用 coverage 声明豁免）");
                        if (hasCoverage && !IsValidCoverage(p.Coverage!))
                            policyErrors.Add($"{ctx}.{p.Name}: coverage 豁免值不合法: {p.Coverage}（provider:<名> / immutable / projection / transient）");
                        // 无豁免 = 默认入集；入集前提是可序列化（不可序列化且未豁免 → 报错，fail-closed）。
                        info.Components.Add(new PinnedComponent(p.Name, p.Custom, p.Generate, !hasCoverage, p.Coverage, def));
                        break;
                    }
                }

                if (p.Custom) preserved.Add($"{ctx}/Components/{ctx}{p.Name}Component.cs");
            }
            modelContexts.Add(info);
        }

        // ---- 可行性校验：凡入集组件必须可序列化（宁可失败，不可生成编不过的序列化代码）----
        var serialErrors = new List<string>();
        foreach (var ctxInfo in modelContexts)
        {
            foreach (var pinned in ctxInfo.Components.Where(c => c.Serialize))
            {
                foreach (var reason in CheckSerializable(ctxInfo.Name, pinned.Definition!, plainTypes))
                {
                    serialErrors.Add($"{ctxInfo.Name}.{pinned.Name}: {reason}" +
                        (mode == "auto"
                            ? "（不可序列化的组件须在 pin 用 coverage 声明豁免：provider:<名>/immutable/projection/transient）"
                            : "（blob 组件应走专属回滚 provider，不标 serialize）"));
                }
            }
        }
        if (serialErrors.Count > 0)
        {
            throw new CodeGenException("序列化入集组件存在不可序列化字段:\n  " + string.Join("\n  ", serialErrors), ExitCodes.SourceValidation);
        }

        if (policyErrors.Count > 0)
        {
            throw new CodeGenException(
                $"serialization.mode={mode} 策略校验失败:\n  " + string.Join("\n  ", policyErrors),
                ExitCodes.UsageOrConfig);
        }

        foreach (var w in warnings) sink.Warn(w);

        return new ModelBuildResult
        {
            Model = new CodeGenModel
            {
                Contexts = modelContexts,
                Editor = config.ToEditorOptions(),
                PreservedFiles = preserved,
                Attribution = config.Output.Attribution,
                SerializablePlainTypes = plainTypes,
            },
            DriftFailures = failures,
            Warnings = warnings,
        };
    }

    private static bool IsValidCoverage(string coverage) =>
        coverage is "immutable" or "projection" or "transient"
        || coverage.StartsWith("provider:", StringComparison.Ordinal);

    private static readonly HashSet<string> SerializablePrimitives = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int",
        "uint", "long", "ulong", "short", "ushort", "string",
    };

    /// <summary>递归校验组件（及其嵌套普通类型）字段全部可序列化；返回失败原因（空 = 通过）。</summary>
    private static List<string> CheckSerializable(string ctx, ComponentInfo def,
        Dictionary<string, PlainTypeInfo> plainTypes)
    {
        var errors = new List<string>();
        CheckFields(ctx, def.ComponentName, def.Fields, plainTypes, errors, depth: 0);
        return errors;
    }

    private static void CheckFields(string ctx, string owner, List<FieldInfo> fields,
        Dictionary<string, PlainTypeInfo> plainTypes, List<string> errors, int depth)
    {
        if (depth > 4)
        {
            errors.Add($"{owner} 嵌套层级超过 4，不支持生成序列化");
            return;
        }

        foreach (var f in fields)
        {
            var reason = CheckField(f, plainTypes, depth);
            if (reason is not null) errors.Add($"{owner}.{f.Name} ({f.TypeFqn}): {reason}");
        }
    }

    private static string? CheckField(FieldInfo f, Dictionary<string, PlainTypeInfo> plainTypes, int depth)
    {
        var t = f.TypeFqn;
        if (SerializablePrimitives.Contains(t)) return null;

        // 框架定点/向量类型：字段形状固定（RawValue / XYZ float），生成器内置展开规则。
        if (SerializationTemplates.KnownLeafTypes.Contains(t)) return null;

        if (t.EndsWith("[]", StringComparison.Ordinal))
        {
            var elem = t[..^2];
            return plainTypes.TryGetValue(elem, out var info) && !info.IsEnum
                ? CheckNested(elem, info, plainTypes, depth)
                : $"数组元素类型不支持: {elem}";
        }

        if (plainTypes.TryGetValue(t, out var single))
        {
            return single.IsEnum ? null : CheckNested(t, single, plainTypes, depth);
        }

        return "类型不支持（基元/枚举/平值数据类/内置定点向量类型及其数组之外）";
    }

    private static string? CheckNested(string fqn, PlainTypeInfo info,
        Dictionary<string, PlainTypeInfo> plainTypes, int depth)
    {
        var nestedErrors = new List<string>();
        CheckFields("", fqn, info.Fields, plainTypes, nestedErrors, depth + 1);
        return nestedErrors.Count == 0 ? null : string.Join("; ", nestedErrors);
    }
}
