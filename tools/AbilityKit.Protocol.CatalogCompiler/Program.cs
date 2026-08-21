using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

return await CatalogCompilerProgram.RunAsync(args);

internal static class CatalogCompilerProgram
{
    private const int ManifestSchemaVersion = 1;
    private const string GeneratorVersion = "1.0";

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = CompilerOptions.Parse(args);
            if (options.ShowHelp)
            {
                WriteUsage();
                return 0;
            }

            var sources = ResolveSources(options.InputPath);
            if (sources.Count == 0)
                throw new InvalidOperationException($"No *.protocol.yaml files found under '{options.InputPath}'.");

            var catalogs = new List<ProtocolCatalogDefinition>(sources.Count);
            for (var i = 0; i < sources.Count; i++)
                catalogs.Add(await LoadAsync(sources[i]));

            var validation = ProtocolCatalogValidator.Validate(catalogs);
            for (var i = 0; i < validation.Diagnostics.Count; i++)
                Console.Error.WriteLine(validation.Diagnostics[i]);
            if (!validation.IsValid)
                return 2;

            var root = Path.GetFullPath(options.InputPath);
            if (File.Exists(root)) root = Path.GetDirectoryName(root)!;
            var sourceNames = sources
                .Select(path => NormalizePath(Path.GetRelativePath(root, path)))
                .ToArray();

            var manifest = GenerateManifest(catalogs, sourceNames);
            var csharp = GenerateCSharp(catalogs, sourceNames, options.Namespace, options.ClassName);

            if (options.Check)
            {
                var manifestCurrent = ReadExisting(options.ManifestOutput);
                var csharpCurrent = ReadExisting(options.CSharpOutput);
                var matches = string.Equals(manifestCurrent, manifest, StringComparison.Ordinal) &&
                              string.Equals(csharpCurrent, csharp, StringComparison.Ordinal);
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

    private static async Task<ProtocolCatalogDefinition> LoadAsync(string path)
    {
        var yaml = await File.ReadAllTextAsync(path, Encoding.UTF8);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var source = deserializer.Deserialize<CatalogSource>(yaml)
            ?? throw new InvalidDataException($"Catalog '{path}' is empty.");
        if (source.SchemaVersion != ManifestSchemaVersion)
            throw new InvalidDataException($"Catalog '{path}' uses unsupported schemaVersion {source.SchemaVersion}.");

        var defaultCodec = source.DefaultCodec?.Trim() ?? string.Empty;
        var messages = new List<ProtocolMessageDefinition>(source.Messages?.Count ?? 0);
        foreach (var item in source.Messages ?? Enumerable.Empty<MessageSource>())
        {
            messages.Add(new ProtocolMessageDefinition(
                item.Id?.Trim() ?? string.Empty,
                item.OpCode,
                ParseDirection(item.Direction, path, item.Id),
                ParseKind(item.Kind, path, item.Id),
                item.PayloadType?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Codec) ? defaultCodec : item.Codec.Trim(),
                ParseReliability(item.Reliability, path, item.Id),
                item.Response?.Trim(),
                item.MinimumSchemaVersion ?? 1,
                item.MaximumSchemaVersion ?? item.MinimumSchemaVersion ?? 1,
                item.MaximumPayloadBytes ?? 1048576,
                item.CaptureSampleRate ?? 1d,
                item.SensitiveFields?.Select(value => value?.Trim() ?? string.Empty).ToArray()));
        }

        return new ProtocolCatalogDefinition(
            source.CatalogId?.Trim() ?? string.Empty,
            source.ProjectId?.Trim() ?? string.Empty,
            source.Domain?.Trim() ?? string.Empty,
            source.Revision,
            defaultCodec,
            messages);
    }

    private static ProtocolDirection ParseDirection(string? value, string path, string? messageId) =>
        Normalize(value) switch
        {
            "c2s" or "clienttoserver" => ProtocolDirection.ClientToServer,
            "s2c" or "servertoclient" => ProtocolDirection.ServerToClient,
            "bidirectional" or "both" => ProtocolDirection.Bidirectional,
            _ => throw InvalidValue("direction", value, path, messageId)
        };

    private static ProtocolPacketKind ParseKind(string? value, string path, string? messageId) =>
        Normalize(value) switch
        {
            "request" => ProtocolPacketKind.Request,
            "response" => ProtocolPacketKind.Response,
            "push" => ProtocolPacketKind.Push,
            "event" => ProtocolPacketKind.Event,
            _ => throw InvalidValue("kind", value, path, messageId)
        };

    private static ProtocolReliability ParseReliability(string? value, string path, string? messageId) =>
        Normalize(value ?? "reliable") switch
        {
            "reliable" => ProtocolReliability.Reliable,
            "realtime" => ProtocolReliability.Realtime,
            _ => throw InvalidValue("reliability", value, path, messageId)
        };

    private static InvalidDataException InvalidValue(string field, string? value, string path, string? messageId) =>
        new InvalidDataException($"Invalid {field} '{value}' in '{path}', message '{messageId}'.");

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();

    private static string GenerateManifest(
        IReadOnlyList<ProtocolCatalogDefinition> catalogs,
        IReadOnlyList<string> sources)
    {
        var document = new ManifestDocument
        {
            SchemaVersion = ManifestSchemaVersion,
            GeneratorVersion = GeneratorVersion,
            Sources = sources,
            Catalogs = catalogs.Select(ToManifest).ToArray()
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) + Environment.NewLine;
    }

    private static ManifestCatalog ToManifest(ProtocolCatalogDefinition catalog) =>
        new ManifestCatalog
        {
            CatalogId = catalog.CatalogId,
            ProjectId = catalog.ProjectId,
            Domain = catalog.Domain,
            Revision = catalog.Revision,
            DefaultCodec = catalog.DefaultCodec,
            Messages = catalog.Messages.Select(message => new ManifestMessage
            {
                Id = message.Id,
                OpCode = message.OpCode,
                Direction = FormatDirection(message.Direction),
                Kind = message.Kind.ToString().ToLowerInvariant(),
                PayloadType = message.PayloadType,
                Codec = message.Codec,
                Reliability = message.Reliability.ToString().ToLowerInvariant(),
                Response = string.IsNullOrEmpty(message.ResponseId) ? null : message.ResponseId,
                MinimumSchemaVersion = message.MinimumSchemaVersion,
                MaximumSchemaVersion = message.MaximumSchemaVersion,
                MaximumPayloadBytes = message.MaximumPayloadBytes,
                CaptureSampleRate = message.CaptureSampleRate,
                SensitiveFields = message.SensitiveFields.Count == 0 ? null : message.SensitiveFields
            }).ToArray()
        };

    private static string GenerateCSharp(
        IReadOnlyList<ProtocolCatalogDefinition> catalogs,
        IReadOnlyList<string> sources,
        string targetNamespace,
        string className)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.Append("// Sources: ").AppendLine(string.Join(", ", sources));
        builder.AppendLine("using System;");
        builder.AppendLine("using AbilityKit.Protocol.Catalog;");
        builder.AppendLine();
        builder.Append("namespace ").AppendLine(targetNamespace);
        builder.AppendLine("{");
        builder.Append("    public static class ").AppendLine(className);
        builder.AppendLine("    {");
        builder.AppendLine("        private static readonly ProtocolCatalogDefinition[] Catalogs =");
        builder.AppendLine("        {");
        foreach (var catalog in catalogs)
        {
            builder.AppendLine("            new ProtocolCatalogDefinition(");
            builder.Append("                ").Append(Literal(catalog.CatalogId)).AppendLine(",");
            builder.Append("                ").Append(Literal(catalog.ProjectId)).AppendLine(",");
            builder.Append("                ").Append(Literal(catalog.Domain)).AppendLine(",");
            builder.Append("                ").Append(catalog.Revision.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("                ").Append(Literal(catalog.DefaultCodec)).AppendLine(",");
            builder.AppendLine("                new ProtocolMessageDefinition[]");
            builder.AppendLine("                {");
            foreach (var message in catalog.Messages)
            {
                builder.AppendLine("                    new ProtocolMessageDefinition(");
                builder.Append("                        ").Append(Literal(message.Id)).AppendLine(",");
                builder.Append("                        ").Append(message.OpCode.ToString(CultureInfo.InvariantCulture)).AppendLine("u,");
                builder.Append("                        ProtocolDirection.").Append(message.Direction).AppendLine(",");
                builder.Append("                        ProtocolPacketKind.").Append(message.Kind).AppendLine(",");
                builder.Append("                        ").Append(Literal(message.PayloadType)).AppendLine(",");
                builder.Append("                        ").Append(Literal(message.Codec)).AppendLine(",");
                builder.Append("                        ProtocolReliability.").Append(message.Reliability).AppendLine(",");
                builder.Append("                        ").Append(LiteralOrNull(message.ResponseId)).AppendLine(",");
                builder.Append("                        ").Append(message.MinimumSchemaVersion.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("                        ").Append(message.MaximumSchemaVersion.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("                        ").Append(message.MaximumPayloadBytes.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("                        ").Append(message.CaptureSampleRate.ToString("R", CultureInfo.InvariantCulture)).AppendLine("d,");
                AppendSensitiveFields(builder, message.SensitiveFields);
                builder.AppendLine("                    ),");
            }
            builder.AppendLine("                }),");
        }
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("        public static System.Collections.Generic.IReadOnlyList<ProtocolCatalogDefinition> All => Catalogs;");
        builder.AppendLine();
        builder.AppendLine("        public static ProtocolCatalogRegistry CreateRegistry()");
        builder.AppendLine("        {");
        builder.AppendLine("            var registry = new ProtocolCatalogRegistry();");
        builder.AppendLine("            for (var i = 0; i < Catalogs.Length; i++) registry.Register(Catalogs[i]);");
        builder.AppendLine("            return registry;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendSensitiveFields(StringBuilder builder, IReadOnlyList<string> fields)
    {
        if (fields.Count == 0)
        {
            builder.AppendLine("                        null");
            return;
        }

        builder.Append("                        new[] { ");
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(Literal(fields[i]));
        }
        builder.AppendLine(" }");
    }

    private static string FormatDirection(ProtocolDirection direction) =>
        direction switch
        {
            ProtocolDirection.ClientToServer => "c2s",
            ProtocolDirection.ServerToClient => "s2c",
            _ => "bidirectional"
        };

    private static string LiteralOrNull(string value) =>
        string.IsNullOrEmpty(value) ? "null" : Literal(value);

    private static string Literal(string value) =>
        "\"" + (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

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
        Console.WriteLine("  [--namespace <namespace>] [--class <name>] [--check]");
    }
}

internal sealed class CompilerOptions
{
    public required string InputPath { get; init; }
    public required string ManifestOutput { get; init; }
    public required string CSharpOutput { get; init; }
    public string Namespace { get; init; } = "AbilityKit.Protocol.Generated";
    public string ClassName { get; init; } = "BuiltInProtocolCatalogs";
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
        var targetNamespace = "AbilityKit.Protocol.Generated";
        var className = "BuiltInProtocolCatalogs";
        var check = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": input = Next(args, ref i); break;
                case "--manifest": manifest = Next(args, ref i); break;
                case "--csharp": csharp = Next(args, ref i); break;
                case "--namespace": targetNamespace = Next(args, ref i); break;
                case "--class": className = Next(args, ref i); break;
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
            Namespace = targetNamespace,
            ClassName = className,
            Check = check
        };
    }

    private static string Next(string[] args, ref int index)
    {
        if (++index >= args.Length) throw new ArgumentException($"Missing value for '{args[index - 1]}'.");
        return args[index];
    }
}

internal sealed class CatalogSource
{
    public int SchemaVersion { get; set; }
    public string? CatalogId { get; set; }
    public string? ProjectId { get; set; }
    public string? Domain { get; set; }
    public int Revision { get; set; }
    public string? DefaultCodec { get; set; }
    public List<MessageSource>? Messages { get; set; }
}

internal sealed class MessageSource
{
    public string? Id { get; set; }
    public uint OpCode { get; set; }
    public string? Direction { get; set; }
    public string? Kind { get; set; }
    public string? PayloadType { get; set; }
    public string? Codec { get; set; }
    public string? Reliability { get; set; }
    public string? Response { get; set; }
    public int? MinimumSchemaVersion { get; set; }
    public int? MaximumSchemaVersion { get; set; }
    public int? MaximumPayloadBytes { get; set; }
    public double? CaptureSampleRate { get; set; }
    public List<string?>? SensitiveFields { get; set; }
}

internal sealed class ManifestDocument
{
    public int SchemaVersion { get; set; }
    public string GeneratorVersion { get; set; } = string.Empty;
    public IReadOnlyList<string> Sources { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ManifestCatalog> Catalogs { get; set; } = Array.Empty<ManifestCatalog>();
}

internal sealed class ManifestCatalog
{
    public string CatalogId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string DefaultCodec { get; set; } = string.Empty;
    public IReadOnlyList<ManifestMessage> Messages { get; set; } = Array.Empty<ManifestMessage>();
}

internal sealed class ManifestMessage
{
    public string Id { get; set; } = string.Empty;
    public uint OpCode { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Reliability { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int MinimumSchemaVersion { get; set; }
    public int MaximumSchemaVersion { get; set; }
    public int MaximumPayloadBytes { get; set; }
    public double CaptureSampleRate { get; set; }
    public IReadOnlyList<string>? SensitiveFields { get; set; }
}
