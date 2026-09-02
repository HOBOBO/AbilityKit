using System.Text;
using System.Text.Json;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Compatibility;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using AbilityKit.Protocol.CatalogCompiler.Lowering;

internal static class EditorWorkflowCommands
{
    private const int MemoryPackExportManifestSchemaVersion = 2;

    public static async Task<int?> TryRunAsync(string[] args)
    {
        if (Has(args, "--workspace-output")) return await WriteWorkspaceAsync(args);
        if (Has(args, "--write-catalog")) return await WriteCatalogAsync(args);
        if (Has(args, "--write-wire-schema")) return await WriteWireSchemaAsync(args);
        if (Has(args, "--compatibility-baseline")) return await CompatibilityBaselineAsync(args);
        if (Has(args, "--compatibility-check")) return await CompatibilityCheckAsync(args);
        if (Has(args, "--export-memorypack")) return await ExportMemoryPackAsync(args);
        if (Has(args, "--export-protobuf")) return await ExportProtobufAsync(args);
        return null;
    }

    private static async Task<int> WriteWorkspaceAsync(string[] args)
    {
        var catalogs = await LoadCatalogsAsync(Required(args, "--input"));
        var schemas = await LoadWireSchemasAsync(Required(args, "--wire-input"));
        var workspace = ProtocolWorkspaceEmitter.Create(
            catalogs.Values,
            catalogs.Sources,
            schemas.Values,
            schemas.Sources);
        WriteIfChanged(Required(args, "--workspace-output"), ProtocolWorkspaceEmitter.Serialize(workspace));
        Console.WriteLine(
            $"Loaded {workspace.Catalogs.Length} catalog(s), {workspace.WireSchemas.Length} wire schema(s), " +
            $"{workspace.Diagnostics.Length} diagnostic(s).");
        return 0;
    }

    private static async Task<int> WriteCatalogAsync(string[] args)
    {
        var editorJson = await File.ReadAllTextAsync(Required(args, "--write-catalog"), Encoding.UTF8);
        var catalog = ProtocolWorkspaceEmitter.DeserializeCatalog(editorJson);
        var yaml = ProtocolYamlEmitter.EmitCatalog(catalog);
        var parsed = new YamlProtocolSourceParser().Parse(catalog.SourcePath, yaml);
        var validation = ProtocolCatalogValidator.Validate(IrLowering.ToCatalog(parsed));
        WriteDiagnostics(validation);
        if (!validation.IsValid) return 2;
        WriteIfChanged(Required(args, "--output"), yaml);
        Console.WriteLine($"Wrote catalog '{catalog.CatalogId}' ({catalog.Messages.Length} messages).");
        return 0;
    }

    private static async Task<int> WriteWireSchemaAsync(string[] args)
    {
        var editorJson = await File.ReadAllTextAsync(Required(args, "--write-wire-schema"), Encoding.UTF8);
        var schema = ProtocolWorkspaceEmitter.DeserializeWireSchema(editorJson);
        var output = Required(args, "--output");
        var parser = new YamlWireSchemaParser();
        if (schema.SchemaVersion != WireSchemaFormatVersions.Current)
            throw new InvalidDataException(
                $"Unsupported wire schema version {schema.SchemaVersion}; expected {WireSchemaFormatVersions.Current}.");

        var edited = ProtocolWorkspaceEmitter.ToWireSchemaIr(schema);
        WireSchemaDocumentIr updated;
        if (File.Exists(output))
        {
            var document = parser.ParseDocument(
                output,
                await File.ReadAllTextAsync(output, Encoding.UTF8));
            if (!string.Equals(schema.ProjectId, document.ProjectId, StringComparison.Ordinal) ||
                !string.Equals(schema.Namespace, document.TargetNamespace, StringComparison.Ordinal) ||
                !string.Equals(schema.GroupId, document.GroupId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Wire schema projectId, groupId and namespace are document-level values; edit the YAML source to change them.");
            }

            var sourceType = schema.SourceType?.Trim() ?? string.Empty;
            var index = document.Schemas
                .Select((value, valueIndex) => (value, valueIndex))
                .Where(value => string.Equals(value.value.Type, sourceType, StringComparison.Ordinal))
                .Select(value => value.valueIndex)
                .SingleOrDefault(-1);
            var schemas = document.Schemas.ToList();
            if (string.IsNullOrWhiteSpace(sourceType))
            {
                if (schemas.Any(value => string.Equals(value.Type, edited.Type, StringComparison.Ordinal)))
                    throw new InvalidDataException(
                        $"Grouped wire schema type '{edited.Type}' already exists in '{output}'.");
                schemas.Add(edited);
            }
            else
            {
                if (index < 0)
                    throw new InvalidDataException($"Grouped wire schema type '{sourceType}' no longer exists in '{output}'.");
                if (schemas.Select((value, valueIndex) => (value, valueIndex)).Any(value =>
                        value.valueIndex != index && string.Equals(value.value.Type, edited.Type, StringComparison.Ordinal)))
                    throw new InvalidDataException(
                        $"Grouped wire schema type '{edited.Type}' already exists in '{output}'.");
                schemas[index] = edited;
            }
            updated = new WireSchemaDocumentIr(
                document.SchemaVersion,
                document.ProjectId,
                document.TargetNamespace,
                document.GroupId,
                document.DefaultMemoryPackMode,
                document.DefaultDeclarationKind,
                document.DefaultMemberStyle,
                schemas);
        }
        else
        {
            updated = new WireSchemaDocumentIr(
                WireSchemaFormatVersions.Current,
                edited.ProjectId,
                edited.TargetNamespace,
                edited.GroupId,
                edited.MemoryPackMode,
                edited.DeclarationKind,
                edited.MemberStyle,
                new[] { edited });
        }

        var yaml = ProtocolYamlEmitter.EmitWireSchemaDocument(updated);
        _ = parser.ParseDocument(output, yaml);
        WriteIfChanged(output, yaml);
        Console.WriteLine($"Wrote wire schema '{schema.QualifiedType}' ({schema.Fields.Length} fields).");
        return 0;
    }

    private static async Task<int> CompatibilityBaselineAsync(string[] args)
    {
        var catalogs = await LoadCatalogsAsync(Required(args, "--input"));
        var schemas = await LoadWireSchemasAsync(Required(args, "--wire-input"));
        var output = Required(args, "--compatibility-baseline");
        var serialized = ProtocolCompatibilityBaseline.Serialize(
            ProtocolCompatibilityBaseline.Capture(catalogs.Values, schemas.Values));

        if (Has(args, "--check"))
        {
            var current = File.Exists(output) ? await File.ReadAllTextAsync(output, Encoding.UTF8) : string.Empty;
            if (!string.Equals(NormalizeNewLines(current), NormalizeNewLines(serialized), StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Protocol compatibility baseline '{output}' is stale. Review compatibility, then update the baseline explicitly.");
                return 3;
            }

            Console.WriteLine($"Protocol compatibility baseline is current: {catalogs.Values.Count} catalog(s), {schemas.Values.Count} wire schema(s).");
            return 0;
        }

        WriteIfChanged(output, serialized);
        Console.WriteLine($"Wrote protocol compatibility baseline '{output}'.");
        return 0;
    }

    private static async Task<int> CompatibilityCheckAsync(string[] args)
    {
        var catalogs = await LoadCatalogsAsync(Required(args, "--input"));
        var schemas = await LoadWireSchemasAsync(Required(args, "--wire-input"));
        var baselinePath = Required(args, "--compatibility-check");
        if (!File.Exists(baselinePath))
            throw new FileNotFoundException("Protocol compatibility baseline does not exist.", baselinePath);

        var baseline = ProtocolCompatibilityBaseline.Deserialize(
            await File.ReadAllTextAsync(baselinePath, Encoding.UTF8));
        var result = ProtocolCompatibilityCheck.Check(baseline, catalogs.Values, schemas.Values);
        foreach (var change in result.Report.Changes)
            Console.WriteLine($"[{change.Severity}] {change.Kind}: {change.Detail}");
        foreach (var violation in result.RevisionPolicy.Violations)
            Console.Error.WriteLine($"[RevisionPolicy] {violation.Rule}: {violation.Detail}");

        if (!result.RevisionPolicy.IsSatisfied) return 5;
        Console.WriteLine(
            $"Protocol compatibility policy satisfied: {result.Report.Changes.Count} change(s), " +
            $"{result.Report.BreakingChanges.Count} revision-covered breaking change(s).");
        return 0;
    }

    private static async Task<int> ExportMemoryPackAsync(string[] args)
    {
        var projectId = Required(args, "--project");
        var outputDirectory = Path.GetFullPath(Required(args, "--export-memorypack"));
        var catalogInput = Required(args, "--input");
        var targetNamespace = Optional(args, "--namespace") ?? "AbilityKit.Protocol.Generated";
        var includeUnreferenced = Has(args, "--include-unreferenced");
        var strict = Has(args, "--strict");
        var check = Has(args, "--check");
        var catalogs = await LoadCatalogsAsync(catalogInput);
        var schemas = await LoadWireSchemasAsync(Required(args, "--wire-input"));

        var selectedCatalogs = catalogs.Values
            .Select((catalog, index) => (catalog, source: catalogs.Sources[index]))
            .Where(value => string.Equals(value.catalog.ProjectId, projectId, StringComparison.Ordinal))
            .ToArray();
        if (selectedCatalogs.Length == 0)
            throw new InvalidOperationException($"Project '{projectId}' has no protocol catalogs.");

        var referencedTypes = selectedCatalogs
            .SelectMany(value => value.catalog.Messages)
            .Where(message => string.Equals(message.Codec, "memorypack", StringComparison.OrdinalIgnoreCase))
            .Select(message => MemoryPackExportPlanner.ElementType(message.PayloadType))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var ownedSchemas = schemas.Values
            .Select((schema, index) => (schema, source: schemas.Sources[index]))
            .Where(value => string.Equals(value.schema.ProjectId, projectId, StringComparison.Ordinal))
            .ToArray();
        var plan = MemoryPackExportPlanner.Create(
            referencedTypes,
            ownedSchemas.Select(value => value.schema).ToArray(),
            includeUnreferenced);
        var ownedByType = ownedSchemas.ToDictionary(
            value => MemoryPackExportPlanner.QualifiedType(value.schema),
            StringComparer.Ordinal);
        var selectedSchemas = plan.Schemas
            .Select(schema => ownedByType[MemoryPackExportPlanner.QualifiedType(schema)])
            .ToArray();
        var duplicateFileName = selectedSchemas
            .GroupBy(value => value.schema.Type, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateFileName != null)
            throw new InvalidOperationException(
                $"Project '{projectId}' has multiple wire schemas that export as '{duplicateFileName.Key}.MemoryPack.g.cs'.");
        var missingTypes = plan.MissingTypes.ToArray();

        for (var i = 0; i < Math.Min(missingTypes.Length, 10); i++)
            Console.Error.WriteLine($"Warning: no owned wire schema for MemoryPack payload '{missingTypes[i]}'.");
        if (missingTypes.Length > 10)
            Console.Error.WriteLine($"Warning: {missingTypes.Length - 10} additional missing wire schema type(s); see protocol-export.json.");
        if (strict && missingTypes.Length > 0) return 4;

        var catalogValues = selectedCatalogs.Select(value => value.catalog).ToArray();
        var plannedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < selectedSchemas.Length; i++)
        {
            var schema = selectedSchemas[i].schema;
            var protocolMessage = MemoryPackBackendEmitter.ResolveProtocolMessage(schema, catalogValues);
            plannedFiles[schema.Type + ".MemoryPack.g.cs"] = MemoryPackWireEmitter.Emit(schema, protocolMessage);
        }

        plannedFiles["ProtocolCatalogs.g.cs"] = CSharpEmitter.Emit(
            catalogValues,
            selectedCatalogs.Select(value => RelativeSource(catalogInput, value.source)).ToArray(),
            targetNamespace,
            "ProjectProtocolCatalogs");
        plannedFiles["ProjectMemoryPackCodecs.g.cs"] = MemoryPackBackendEmitter.Emit(
            catalogValues,
            selectedSchemas.Select(value => value.schema).ToArray(),
            targetNamespace);
        plannedFiles[MemoryPackExportVerifier.ManifestFileName] = JsonSerializer.Serialize(
            new MemoryPackExportManifest
            {
                SchemaVersion = MemoryPackExportManifestSchemaVersion,
                GeneratorVersion = ProtocolCatalogConstants.GeneratorVersion,
                ProjectId = projectId,
                CatalogIds = selectedCatalogs.Select(value => value.catalog.CatalogId).ToArray(),
                ReferencedMemoryPackTypes = referencedTypes,
                GeneratedTypes = selectedSchemas
                    .Select(value => MemoryPackExportPlanner.QualifiedType(value.schema))
                    .ToArray(),
                GeneratedGroups = selectedSchemas
                    .GroupBy(value => value.schema.GroupId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new MemoryPackExportGroupManifest
                    {
                        GroupId = group.Key,
                        GeneratedTypes = group
                            .Select(value => MemoryPackExportPlanner.QualifiedType(value.schema))
                            .OrderBy(value => value, StringComparer.Ordinal)
                            .ToArray()
                    })
                    .ToArray(),
                MissingWireSchemaTypes = missingTypes,
                GeneratedFiles = plannedFiles.Keys
                    .Where(file => !string.Equals(file, MemoryPackExportVerifier.ManifestFileName, StringComparison.Ordinal))
                    .OrderBy(file => file, StringComparer.Ordinal)
                    .ToArray()
            },
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) + Environment.NewLine;

        if (check)
        {
            var verification = MemoryPackExportVerifier.Verify(outputDirectory, plannedFiles);
            for (var i = 0; i < verification.Mismatches.Count; i++)
            {
                var mismatch = verification.Mismatches[i];
                Console.Error.WriteLine($"Stale wire export [{mismatch.Kind}] {mismatch.FileName}: {mismatch.Detail}.");
            }
            if (!verification.IsCurrent)
            {
                Console.Error.WriteLine(
                    $"Generated protocol wire export for project '{projectId}' is stale. " +
                    "Run the wire export entry without --check and commit the refreshed output.");
                return 3;
            }

            Console.WriteLine(
                $"Protocol wire export for project '{projectId}' is current: {selectedCatalogs.Length} catalog(s), " +
                $"{selectedSchemas.Length} MemoryPack type(s), {missingTypes.Length} missing wire schema(s), " +
                $"{plannedFiles.Count} committed file(s).");
            return 0;
        }

        Directory.CreateDirectory(outputDirectory);
        var generatedFiles = plannedFiles.Keys
            .Where(file => !string.Equals(file, MemoryPackExportVerifier.ManifestFileName, StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        DeleteStaleGeneratedFiles(outputDirectory, projectId, generatedFiles);
        foreach (var planned in plannedFiles.OrderBy(file => file.Key, StringComparer.Ordinal))
            WriteIfChanged(Path.Combine(outputDirectory, planned.Key), planned.Value);

        Console.WriteLine(
            $"Exported project '{projectId}': {selectedCatalogs.Length} catalog(s), " +
            $"{selectedSchemas.Length} MemoryPack type(s), {missingTypes.Length} missing wire schema(s).");
        return 0;
    }

    private static async Task<int> ExportProtobufAsync(string[] args)
    {
        var projectId = Required(args, "--project");
        var outputDirectory = Path.GetFullPath(Required(args, "--export-protobuf"));
        var check = Has(args, "--check");
        var schemas = await LoadWireSchemasAsync(Required(args, "--wire-input"));
        var selectedSchemas = schemas.Values
            .Where(schema => string.Equals(schema.ProjectId, projectId, StringComparison.Ordinal))
            .OrderBy(MemoryPackExportPlanner.QualifiedType, StringComparer.Ordinal)
            .ToArray();
        if (selectedSchemas.Length == 0)
            throw new InvalidOperationException($"Project '{projectId}' has no owned wire schemas.");

        var backend = new ProtocolBackendRegistry(new IProtocolBackend[]
        {
            new MemoryPackProtocolBackend(),
            new ProtobufProtocolBackend()
        }).Resolve("protobuf");
        var plannedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var schema in selectedSchemas)
        {
            foreach (var output in backend.EmitSchema(new ProtocolBackendSchemaContext(schema, null)))
            {
                if (!plannedFiles.TryAdd(output.FileName, output.Content))
                    throw new InvalidOperationException(
                        $"Project '{projectId}' has multiple wire schemas that export as '{output.FileName}'.");
            }
        }

        if (check)
        {
            var verification = MemoryPackExportVerifier.Verify(outputDirectory, plannedFiles);
            foreach (var mismatch in verification.Mismatches)
                Console.Error.WriteLine($"Stale protobuf export [{mismatch.Kind}] {mismatch.FileName}: {mismatch.Detail}.");
            if (!verification.IsCurrent) return 3;
            Console.WriteLine(
                $"Protocol protobuf export for project '{projectId}' is current: {selectedSchemas.Length} schema(s), " +
                $"{plannedFiles.Count} committed file(s).");
            return 0;
        }

        Directory.CreateDirectory(outputDirectory);
        foreach (var existing in Directory.EnumerateFiles(outputDirectory, "*.proto", SearchOption.TopDirectoryOnly))
        {
            if (!plannedFiles.ContainsKey(Path.GetFileName(existing))) File.Delete(existing);
        }
        foreach (var planned in plannedFiles.OrderBy(file => file.Key, StringComparer.Ordinal))
            WriteIfChanged(Path.Combine(outputDirectory, planned.Key), planned.Value);
        Console.WriteLine(
            $"Exported protobuf project '{projectId}': {selectedSchemas.Length} schema(s), {plannedFiles.Count} file(s).");
        return 0;
    }

    private static async Task<LoadedSources<ProtocolCatalogIr>> LoadCatalogsAsync(string inputPath)
    {
        var sources = ResolveSources(inputPath, "*.protocol.yaml");
        if (sources.Count == 0)
            throw new InvalidOperationException($"No *.protocol.yaml files found under '{inputPath}'.");
        var parser = new YamlProtocolSourceParser();
        var values = new ProtocolCatalogIr[sources.Count];
        for (var i = 0; i < sources.Count; i++)
            values[i] = parser.Parse(sources[i], await File.ReadAllTextAsync(sources[i], Encoding.UTF8));
        return new LoadedSources<ProtocolCatalogIr>(values, sources);
    }

    private static async Task<LoadedSources<WireSchemaIr>> LoadWireSchemasAsync(string inputPath)
    {
        var sources = ResolveSources(inputPath, "*.wire.yaml");
        var parser = new YamlWireSchemaParser();
        var values = new List<WireSchemaIr>();
        var expandedSources = new List<string>();
        for (var i = 0; i < sources.Count; i++)
        {
            var document = parser.ParseDocument(
                sources[i],
                await File.ReadAllTextAsync(sources[i], Encoding.UTF8));
            foreach (var schema in document.Schemas)
            {
                values.Add(schema);
                expandedSources.Add(sources[i]);
            }
        }
        var duplicateGroup = values
            .Select((schema, index) => (schema.ProjectId, schema.GroupId, source: expandedSources[index]))
            .Distinct()
            .GroupBy(value => (value.ProjectId, value.GroupId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateGroup != null)
        {
            throw new InvalidDataException(
                $"Project '{duplicateGroup.Key.ProjectId}' defines wire group '{duplicateGroup.Key.GroupId}' in multiple files.");
        }
        return new LoadedSources<WireSchemaIr>(values, expandedSources);
    }

    private static IReadOnlyList<string> ResolveSources(string inputPath, string pattern)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (File.Exists(fullPath)) return new[] { fullPath };
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Protocol input '{fullPath}' does not exist.");
        return Directory.EnumerateFiles(fullPath, pattern, SearchOption.AllDirectories)
            .OrderBy(NormalizePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void WriteDiagnostics(ProtocolCatalogValidationResult validation)
    {
        for (var i = 0; i < validation.Diagnostics.Count; i++)
            Console.Error.WriteLine(validation.Diagnostics[i]);
    }

    private static bool Has(string[] args, string name) => args.Contains(name, StringComparer.Ordinal);

    private static string Required(string[] args, string name) =>
        Optional(args, name) ?? throw new ArgumentException($"{name} is required.");

    private static string? Optional(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for '{name}'.");
        return args[index + 1];
    }

    private static void WriteIfChanged(string path, string content)
    {
        var current = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        if (string.Equals(current, content, StringComparison.Ordinal)) return;
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content, new UTF8Encoding(false));
    }

    private static void DeleteStaleGeneratedFiles(
        string outputDirectory,
        string projectId,
        IReadOnlyCollection<string> currentFiles)
    {
        var manifestPath = Path.Combine(outputDirectory, "protocol-export.json");
        if (!File.Exists(manifestPath)) return;
        var previous = JsonSerializer.Deserialize<MemoryPackExportManifest>(
            File.ReadAllText(manifestPath, Encoding.UTF8),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (previous == null) return;
        if (!string.Equals(previous.ProjectId, projectId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Export folder already belongs to project '{previous.ProjectId}', not '{projectId}'.");

        var root = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var file in previous.GeneratedFiles ?? Array.Empty<string>())
        {
            if (currentFiles.Contains(file, StringComparer.Ordinal) ||
                !file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.GetFullPath(Path.Combine(outputDirectory, file));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Managed export file escapes output directory: '{file}'.");
            if (File.Exists(target)) File.Delete(target);
        }
    }

    private static string RelativeSource(string inputPath, string source)
    {
        var root = Path.GetFullPath(inputPath);
        if (File.Exists(root)) root = Path.GetDirectoryName(root)!;
        return NormalizePath(Path.GetRelativePath(root, source));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed record LoadedSources<T>(IReadOnlyList<T> Values, IReadOnlyList<string> Sources);

    private sealed class MemoryPackExportManifest
    {
        public int SchemaVersion { get; set; }
        public string GeneratorVersion { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string[] CatalogIds { get; set; } = Array.Empty<string>();
        public string[] ReferencedMemoryPackTypes { get; set; } = Array.Empty<string>();
        public string[] GeneratedTypes { get; set; } = Array.Empty<string>();
        public MemoryPackExportGroupManifest[] GeneratedGroups { get; set; } =
            Array.Empty<MemoryPackExportGroupManifest>();
        public string[] MissingWireSchemaTypes { get; set; } = Array.Empty<string>();
        public string[] GeneratedFiles { get; set; } = Array.Empty<string>();
    }

    private sealed class MemoryPackExportGroupManifest
    {
        public string GroupId { get; set; } = string.Empty;
        public string[] GeneratedTypes { get; set; } = Array.Empty<string>();
    }
}
