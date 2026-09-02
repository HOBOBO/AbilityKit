using System.Text;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using AbilityKit.Protocol.CatalogCompiler.Lowering;

return await CatalogCompilerProgram.RunAsync(args);

public static class CatalogCompilerProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var editorWorkflowResult = await EditorWorkflowCommands.TryRunAsync(args);
            if (editorWorkflowResult.HasValue) return editorWorkflowResult.Value;

            var options = CompilerOptions.Parse(args);
            if (options.ShowHelp)
            {
                WriteUsage();
                return 0;
            }

            var sources = ResolveSources(options.InputPath);
            if (sources.Count == 0)
                throw new InvalidOperationException($"No *.protocol.yaml files found under '{options.InputPath}'.");

            var parser = new YamlProtocolSourceParser();
            var catalogs = new List<ProtocolCatalogIr>(sources.Count);
            for (var i = 0; i < sources.Count; i++)
            {
                var yaml = await File.ReadAllTextAsync(sources[i], Encoding.UTF8);
                catalogs.Add(parser.Parse(sources[i], yaml));
            }

            var runtimeCatalogs = IrLowering.ToRuntime(catalogs);
            var validation = ProtocolCatalogValidator.Validate(runtimeCatalogs);
            for (var i = 0; i < validation.Diagnostics.Count; i++)
                Console.Error.WriteLine(validation.Diagnostics[i]);
            if (!validation.IsValid)
                return 2;

            var root = Path.GetFullPath(options.InputPath);
            if (File.Exists(root)) root = Path.GetDirectoryName(root)!;
            var sourceNames = sources
                .Select(path => NormalizePath(Path.GetRelativePath(root, path)))
                .ToArray();

            var manifest = ManifestEmitter.Emit(catalogs, sourceNames);
            var csharp = CSharpEmitter.Emit(catalogs, sourceNames, options.Namespace, options.ClassName);
            var metadata = options.MetadataOutput == null
                ? null
                : ProtocolMetadataEmitter.Emit(catalogs, sourceNames, options.Namespace, options.MetadataClassName);

            if (options.Check)
            {
                var manifestCurrent = ReadExisting(options.ManifestOutput);
                var csharpCurrent = ReadExisting(options.CSharpOutput);
                var metadataCurrent = options.MetadataOutput == null ? null : ReadExisting(options.MetadataOutput);
                var matches = string.Equals(manifestCurrent, manifest, StringComparison.Ordinal) &&
                              string.Equals(csharpCurrent, csharp, StringComparison.Ordinal) &&
                              (metadata == null || string.Equals(metadataCurrent, metadata, StringComparison.Ordinal));
                if (!matches)
                {
                    Console.Error.WriteLine("Generated protocol catalog outputs are stale. Run the catalog compiler without --check.");
                    return 3;
                }

                Console.WriteLine($"Protocol catalogs are current: {catalogs.Count} catalog(s), {catalogs.Sum(c => c.Messages.Count)} message(s).");
                return 0;
            }

            WriteIfChanged(options.ManifestOutput, manifest);
            WriteIfChanged(options.CSharpOutput, csharp);
            if (options.MetadataOutput != null && metadata != null)
                WriteIfChanged(options.MetadataOutput, metadata);
            Console.WriteLine($"Compiled {catalogs.Count} protocol catalog(s), {catalogs.Sum(c => c.Messages.Count)} message(s).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static IReadOnlyList<string> ResolveSources(string inputPath)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (File.Exists(fullPath)) return new[] { fullPath };
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Protocol catalog input '{fullPath}' does not exist.");

        return Directory
            .EnumerateFiles(fullPath, "*.protocol.yaml", SearchOption.AllDirectories)
            .OrderBy(path => NormalizePath(path), StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string ReadExisting(string path) =>
        File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

    private static void WriteIfChanged(string path, string content)
    {
        var current = ReadExisting(path);
        if (string.Equals(current, content, StringComparison.Ordinal)) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteUsage()
    {
        Console.WriteLine("AbilityKit protocol catalog compiler");
        Console.WriteLine("  --input <file-or-directory>");
        Console.WriteLine("  --manifest <output.json>");
        Console.WriteLine("  --csharp <output.g.cs>");
        Console.WriteLine("  [--metadata <output.g.cs>] [--metadata-class <name>]");
        Console.WriteLine("  [--namespace <namespace>] [--class <name>] [--check]");
        Console.WriteLine();
        Console.WriteLine("Editor/workspace and governance commands:");
        Console.WriteLine("  --input <catalog-root> --wire-input <wire-root> --workspace-output <workspace.json>");
        Console.WriteLine("  --write-catalog <catalog.json> --output <catalog.protocol.yaml>");
        Console.WriteLine("  --write-wire-schema <schema.json> --output <schema.wire.yaml>");
        Console.WriteLine("    appends a type to an existing grouped v2 document when schema.sourceType is empty");
        Console.WriteLine("  --input <catalog-root> --wire-input <wire-root> --compatibility-baseline <baseline.json> [--check]");
        Console.WriteLine("  --input <catalog-root> --wire-input <wire-root> --compatibility-check <baseline.json>");
        Console.WriteLine("    exits 5 when breaking changes are not covered by the owning catalog revision bump");
        Console.WriteLine("  --input <catalog-root> --wire-input <wire-root> --export-memorypack <folder>");
        Console.WriteLine("    --project <project-id> [--namespace <catalog-namespace>] [--include-unreferenced] [--strict]");
        Console.WriteLine("    [--check]  check-only: compare committed export files with the deterministic");
        Console.WriteLine("               export, exit 3 when stale, 4 when --strict finds missing wire schemas");
        Console.WriteLine("  --wire-input <wire-root> --project <project-id> --export-protobuf <folder> [--check]");
        Console.WriteLine("    emits deterministic proto3 contracts through the protobuf backend SPI");
    }
}

internal sealed class CompilerOptions
{
    public required string InputPath { get; init; }
    public required string ManifestOutput { get; init; }
    public required string CSharpOutput { get; init; }
    public string? MetadataOutput { get; init; }
    public string Namespace { get; init; } = "AbilityKit.Protocol.Generated";
    public string ClassName { get; init; } = "BuiltInProtocolCatalogs";
    public string MetadataClassName { get; init; } = "BuiltInProtocolMetadata";
    public bool Check { get; init; }
    public bool ShowHelp { get; init; }

    public static CompilerOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            return new CompilerOptions
            {
                InputPath = string.Empty,
                ManifestOutput = string.Empty,
                CSharpOutput = string.Empty,
                ShowHelp = true
            };
        }

        string? input = null;
        string? manifest = null;
        string? csharp = null;
        string? metadata = null;
        var targetNamespace = "AbilityKit.Protocol.Generated";
        var className = "BuiltInProtocolCatalogs";
        var metadataClassName = "BuiltInProtocolMetadata";
        var check = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": input = Next(args, ref i); break;
                case "--manifest": manifest = Next(args, ref i); break;
                case "--csharp": csharp = Next(args, ref i); break;
                case "--metadata": metadata = Next(args, ref i); break;
                case "--namespace": targetNamespace = Next(args, ref i); break;
                case "--class": className = Next(args, ref i); break;
                case "--metadata-class": metadataClassName = Next(args, ref i); break;
                case "--check": check = true; break;
                default: throw new ArgumentException($"Unknown argument '{args[i]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(manifest) || string.IsNullOrWhiteSpace(csharp))
            throw new ArgumentException("--input, --manifest and --csharp are required.");

        return new CompilerOptions
        {
            InputPath = input,
            ManifestOutput = manifest,
            CSharpOutput = csharp,
            MetadataOutput = metadata,
            Namespace = targetNamespace,
            ClassName = className,
            MetadataClassName = metadataClassName,
            Check = check
        };
    }

    private static string Next(string[] args, ref int index)
    {
        if (++index >= args.Length) throw new ArgumentException($"Missing value for '{args[index - 1]}'.");
        return args[index];
    }
}
