using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// Compares the current protocol catalogs and wire schemas against a committed baseline and
/// classifies every difference. Wire layout facts (opcode, direction, kind, payload, codec,
/// request-response pairing, wire fields, reserved ids) are breaking when a peer built against
/// the baseline can no longer interoperate; purely additive or observational changes are
/// compatible. Classification never depends on the codec backend except through the wire schema's
/// own MemoryPack mode, which is itself a classified fact.
/// </summary>
public static class ProtocolCompatibilityDiff
{
    public static ProtocolCompatibilityReport Compare(
        ProtocolCompatibilityBaselineDocument baseline,
        IReadOnlyList<ProtocolCatalogIr> currentCatalogs,
        IReadOnlyList<WireSchemaIr> currentWireSchemas)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(currentCatalogs);
        ArgumentNullException.ThrowIfNull(currentWireSchemas);

        var changes = new List<ProtocolCompatibilityChange>();
        var baselineCatalogs = IndexBy(
            baseline.Catalogs,
            catalog => catalog.CatalogId,
            id => $"Duplicate baseline catalog id '{id}'.");
        var currentCatalogsById = IndexBy(
            currentCatalogs,
            catalog => catalog.CatalogId,
            id => $"Duplicate current catalog id '{id}'.");

        foreach (var baselineCatalog in baseline.Catalogs)
        {
            if (!currentCatalogsById.TryGetValue(baselineCatalog.CatalogId, out var currentCatalog))
            {
                changes.Add(new ProtocolCompatibilityChange(
                    baselineCatalog.CatalogId, null, null,
                    ProtocolCompatibilityChangeKind.CatalogRemoved,
                    ProtocolCompatibilitySeverity.Breaking,
                    $"Catalog '{baselineCatalog.CatalogId}' was removed."));
                continue;
            }

            CompareCatalog(baselineCatalog, currentCatalog, changes);
        }

        foreach (var currentCatalog in currentCatalogs)
        {
            if (!baselineCatalogs.ContainsKey(currentCatalog.CatalogId))
                changes.Add(new ProtocolCompatibilityChange(
                    currentCatalog.CatalogId, null, null,
                    ProtocolCompatibilityChangeKind.CatalogAdded,
                    ProtocolCompatibilitySeverity.Compatible,
                    $"Catalog '{currentCatalog.CatalogId}' was added."));
        }

        var baselineWireSchemas = IndexBy(
            baseline.WireSchemas,
            schema => schema.QualifiedType,
            type => $"Duplicate baseline wire schema type '{type}'.");
        var currentWireSchemasByType = IndexBy(
            currentWireSchemas.Select(schema => (schema, type: MemoryPackExportPlanner.QualifiedType(schema))),
            pair => pair.type,
            type => $"Duplicate current wire schema type '{type}'.");

        foreach (var baselineWireSchema in baseline.WireSchemas)
        {
            if (!currentWireSchemasByType.TryGetValue(baselineWireSchema.QualifiedType, out var currentPair))
            {
                changes.Add(new ProtocolCompatibilityChange(
                    string.Empty, null, baselineWireSchema.QualifiedType,
                    ProtocolCompatibilityChangeKind.WireSchemaRemoved,
                    ProtocolCompatibilitySeverity.Breaking,
                    $"Wire schema '{baselineWireSchema.QualifiedType}' was removed."));
                continue;
            }

            CompareWireSchema(baselineWireSchema, currentPair.schema, changes);
        }

        foreach (var currentPair in currentWireSchemasByType.Values)
        {
            if (!baselineWireSchemas.ContainsKey(currentPair.type))
                changes.Add(new ProtocolCompatibilityChange(
                    string.Empty, null, currentPair.type,
                    ProtocolCompatibilityChangeKind.WireSchemaAdded,
                    ProtocolCompatibilitySeverity.Compatible,
                    $"Wire schema '{currentPair.type}' was added."));
        }

        return new ProtocolCompatibilityReport(changes);
    }

    private static void CompareCatalog(
        ProtocolCompatibilityBaselineCatalog baseline,
        ProtocolCatalogIr current,
        List<ProtocolCompatibilityChange> changes)
    {
        if (!string.Equals(baseline.DefaultCodec, current.DefaultCodec, StringComparison.Ordinal))
            changes.Add(new ProtocolCompatibilityChange(
                current.CatalogId, null, null,
                ProtocolCompatibilityChangeKind.CatalogDefaultCodecChanged,
                ProtocolCompatibilitySeverity.Compatible,
                $"Catalog '{current.CatalogId}' default codec changed from '{baseline.DefaultCodec}' to " +
                $"'{current.DefaultCodec}' (per-message codecs carry the wire contract)."));

        var baselineMessages = IndexBy(
            baseline.Messages,
            message => message.Id,
            id => $"Duplicate baseline message id '{id}' in catalog '{baseline.CatalogId}'.");
        var currentMessages = IndexBy(
            current.Messages,
            message => message.Id,
            id => $"Duplicate current message id '{id}' in catalog '{current.CatalogId}'.");

        foreach (var baselineMessage in baseline.Messages)
        {
            if (!currentMessages.TryGetValue(baselineMessage.Id, out var currentMessage))
            {
                changes.Add(new ProtocolCompatibilityChange(
                    baseline.CatalogId, baselineMessage.Id, null,
                    ProtocolCompatibilityChangeKind.MessageRemoved,
                    ProtocolCompatibilitySeverity.Breaking,
                    $"Message '{baseline.CatalogId}/{baselineMessage.Id}' was removed."));
                continue;
            }

            CompareMessage(baseline.CatalogId, baselineMessage, currentMessage, changes);
        }

        foreach (var currentMessage in current.Messages)
        {
            if (!baselineMessages.ContainsKey(currentMessage.Id))
                changes.Add(new ProtocolCompatibilityChange(
                    current.CatalogId, currentMessage.Id, null,
                    ProtocolCompatibilityChangeKind.MessageAdded,
                    ProtocolCompatibilitySeverity.Compatible,
                    $"Message '{current.CatalogId}/{currentMessage.Id}' was added."));
        }

        CompareOpCodeOwnership(baseline, current, changes);
    }

    private static void CompareMessage(
        string catalogId,
        ProtocolCompatibilityBaselineMessage baseline,
        ProtocolMessageIr current,
        List<ProtocolCompatibilityChange> changes)
    {
        void Add(
            ProtocolCompatibilityChangeKind kind,
            ProtocolCompatibilitySeverity severity,
            string detail) =>
            changes.Add(new ProtocolCompatibilityChange(catalogId, baseline.Id, null, kind, severity, detail));

        if (baseline.OpCode != current.OpCode)
            Add(ProtocolCompatibilityChangeKind.MessageOpCodeChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' opcode changed from {baseline.OpCode} to {current.OpCode}.");

        if (!string.Equals(baseline.Direction, ProtocolCompatibilityNames.Direction(current.Direction), StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessageDirectionChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' direction changed from '{baseline.Direction}' to " +
                $"'{ProtocolCompatibilityNames.Direction(current.Direction)}'.");

        if (!string.Equals(baseline.Kind, ProtocolCompatibilityNames.Kind(current.Kind), StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessageKindChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' kind changed from '{baseline.Kind}' to " +
                $"'{ProtocolCompatibilityNames.Kind(current.Kind)}'.");

        if (!string.Equals(baseline.PayloadType, current.PayloadType, StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessagePayloadChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' payload changed from '{baseline.PayloadType}' to '{current.PayloadType}'.");

        if (!string.Equals(baseline.Codec, current.Codec, StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessageCodecChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' codec changed from '{baseline.Codec}' to '{current.Codec}'.");

        if (!string.Equals(baseline.Reliability, ProtocolCompatibilityNames.Reliability(current.Reliability), StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessageReliabilityChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' reliability changed from '{baseline.Reliability}' to " +
                $"'{ProtocolCompatibilityNames.Reliability(current.Reliability)}'.");

        var baselineResponse = baseline.Response ?? string.Empty;
        if (!string.Equals(baselineResponse, current.ResponseId, StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.MessageResponseChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Message '{catalogId}/{baseline.Id}' request-response pairing changed from " +
                $"{FormatReference(baselineResponse)} to {FormatReference(current.ResponseId)}.");

        if (baseline.MinimumSchemaVersion != current.MinimumSchemaVersion ||
            baseline.MaximumSchemaVersion != current.MaximumSchemaVersion)
            Add(ProtocolCompatibilityChangeKind.MessageSchemaWindowChanged, ProtocolCompatibilitySeverity.Compatible,
                $"Message '{catalogId}/{baseline.Id}' schema window changed from " +
                $"[{baseline.MinimumSchemaVersion}, {baseline.MaximumSchemaVersion}] to " +
                $"[{current.MinimumSchemaVersion}, {current.MaximumSchemaVersion}].");

        if (baseline.MaximumPayloadBytes != current.MaximumPayloadBytes)
            Add(ProtocolCompatibilityChangeKind.MessageBudgetChanged, ProtocolCompatibilitySeverity.Compatible,
                $"Message '{catalogId}/{baseline.Id}' payload budget changed from " +
                $"{baseline.MaximumPayloadBytes} to {current.MaximumPayloadBytes} bytes.");
    }

    private static void CompareOpCodeOwnership(
        ProtocolCompatibilityBaselineCatalog baseline,
        ProtocolCatalogIr current,
        List<ProtocolCompatibilityChange> changes)
    {
        var baselineOwner = FirstOwnerByOpCode(baseline.Messages.Select(message => (message.OpCode, message.Id)));
        var currentOwner = FirstOwnerByOpCode(current.Messages.Select(message => (message.OpCode, message.Id)));

        foreach (var (opCode, baselineMessageId) in baselineOwner)
        {
            if (currentOwner.TryGetValue(opCode, out var currentMessageId) &&
                !string.Equals(currentMessageId, baselineMessageId, StringComparison.Ordinal))
                changes.Add(new ProtocolCompatibilityChange(
                    baseline.CatalogId, currentMessageId, null,
                    ProtocolCompatibilityChangeKind.MessageOpCodeReassigned,
                    ProtocolCompatibilitySeverity.Breaking,
                    $"Opcode {opCode} in catalog '{baseline.CatalogId}' was reassigned from message " +
                    $"'{baselineMessageId}' to '{currentMessageId}'."));
        }
    }

    private static Dictionary<uint, string> FirstOwnerByOpCode(IEnumerable<(uint OpCode, string Id)> messages)
    {
        var owners = new Dictionary<uint, string>();
        foreach (var (opCode, messageId) in messages)
            owners.TryAdd(opCode, messageId);
        return owners;
    }

    private static void CompareWireSchema(
        ProtocolCompatibilityBaselineWireSchema baseline,
        WireSchemaIr current,
        List<ProtocolCompatibilityChange> changes)
    {
        var wireType = baseline.QualifiedType;

        void Add(
            ProtocolCompatibilityChangeKind kind,
            ProtocolCompatibilitySeverity severity,
            string detail) =>
            changes.Add(new ProtocolCompatibilityChange(string.Empty, null, wireType, kind, severity, detail));

        var currentMode = ProtocolCompatibilityNames.MemoryPackMode(current.MemoryPackMode);
        if (!string.Equals(baseline.MemoryPackMode, currentMode, StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.WireMemoryPackModeChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Wire schema '{wireType}' MemoryPack mode changed from '{baseline.MemoryPackMode}' to '{currentMode}'.");

        var currentDeclaration = ProtocolCompatibilityNames.Declaration(current.DeclarationKind);
        var currentMemberStyle = ProtocolCompatibilityNames.MemberStyle(current.MemberStyle);
        if (!string.Equals(baseline.DeclarationKind, currentDeclaration, StringComparison.Ordinal) ||
            !string.Equals(baseline.MemberStyle, currentMemberStyle, StringComparison.Ordinal))
            Add(ProtocolCompatibilityChangeKind.WireDeclarationShapeChanged, ProtocolCompatibilitySeverity.Compatible,
                $"Wire schema '{wireType}' declaration shape changed from {baseline.DeclarationKind}/" +
                $"{baseline.MemberStyle} to {currentDeclaration}/{currentMemberStyle} (generated code shape only).");

        var baselineReserved = new HashSet<uint>(baseline.ReservedIds);
        var currentReserved = new HashSet<uint>(current.ReservedIds);

        foreach (var field in current.Fields)
        {
            if (baselineReserved.Contains(field.Id))
                Add(ProtocolCompatibilityChangeKind.WireReservedIdConsumed, ProtocolCompatibilitySeverity.Breaking,
                    $"Wire schema '{wireType}' field '{field.Name}' consumes reserved id {field.Id}.");
        }

        if (!baselineReserved.SetEquals(currentReserved))
            Add(ProtocolCompatibilityChangeKind.WireReservationChanged, ProtocolCompatibilitySeverity.Compatible,
                $"Wire schema '{wireType}' reserved ids changed: " +
                $"[{string.Join(", ", baseline.ReservedIds)}] -> [{string.Join(", ", current.ReservedIds)}].");

        var baselineFieldsById = IndexBy(
            baseline.Fields,
            field => field.Id,
            id => $"Duplicate baseline field id {id} in wire schema '{wireType}'.");
        var currentFieldsById = IndexBy(
            current.Fields,
            field => field.Id,
            id => $"Duplicate current field id {id} in wire schema '{wireType}'.");
        var baselineFieldsByName = new Dictionary<string, ProtocolCompatibilityBaselineWireField>(StringComparer.Ordinal);
        foreach (var field in baseline.Fields)
            baselineFieldsByName.TryAdd(field.Name, field);

        // A field that keeps its name but moves to a new id is one identity change, not an
        // add plus a remove; mark both sides as explained so the passes below stay quiet.
        var explainedBaselineIds = new HashSet<uint>();
        var explainedCurrentIds = new HashSet<uint>();
        foreach (var currentField in current.Fields)
        {
            if (!baselineFieldsByName.TryGetValue(currentField.Name, out var baselineField) ||
                baselineField.Id == currentField.Id)
                continue;

            explainedBaselineIds.Add(baselineField.Id);
            explainedCurrentIds.Add(currentField.Id);
            Add(ProtocolCompatibilityChangeKind.WireFieldIdChanged, ProtocolCompatibilitySeverity.Breaking,
                $"Wire schema '{wireType}' field '{currentField.Name}' changed id from {baselineField.Id} to {currentField.Id}.");
        }

        foreach (var baselineField in baseline.Fields)
        {
            if (currentFieldsById.ContainsKey(baselineField.Id) || explainedBaselineIds.Contains(baselineField.Id))
                continue;

            var reserved = currentReserved.Contains(baselineField.Id);
            var severity = current.MemoryPackMode == WireMemoryPackMode.Sequential || !reserved
                ? ProtocolCompatibilitySeverity.Breaking
                : ProtocolCompatibilitySeverity.Compatible;
            var reservationNote = current.MemoryPackMode == WireMemoryPackMode.Sequential
                ? string.Empty
                : reserved
                    ? " and its id is now reserved"
                    : " without reserving its id";
            Add(ProtocolCompatibilityChangeKind.WireFieldRemoved, severity,
                $"Wire schema '{wireType}' field '{baselineField.Name}' (id {baselineField.Id}) was removed{reservationNote}.");
        }

        foreach (var currentField in current.Fields)
        {
            if (baselineFieldsById.ContainsKey(currentField.Id) || explainedCurrentIds.Contains(currentField.Id))
                continue;

            var severity = current.MemoryPackMode == WireMemoryPackMode.Sequential || currentField.IsRequired
                ? ProtocolCompatibilitySeverity.Breaking
                : ProtocolCompatibilitySeverity.Compatible;
            Add(ProtocolCompatibilityChangeKind.WireFieldAdded, severity,
                $"Wire schema '{wireType}' field '{currentField.Name}' (id {currentField.Id}) was added as " +
                $"{(currentField.IsRequired ? "required" : "optional")}.");
        }

        foreach (var currentField in current.Fields)
        {
            if (!baselineFieldsById.TryGetValue(currentField.Id, out var baselineField))
                continue;

            if (!string.Equals(baselineField.Name, currentField.Name, StringComparison.Ordinal))
                Add(ProtocolCompatibilityChangeKind.WireFieldRenamed, ProtocolCompatibilitySeverity.Compatible,
                    $"Wire schema '{wireType}' field id {currentField.Id} was renamed from " +
                    $"'{baselineField.Name}' to '{currentField.Name}' (ids, not names, hit the wire).");

            if (!string.Equals(baselineField.ScalarType, currentField.ScalarType, StringComparison.Ordinal) ||
                !string.Equals(baselineField.TypeName, currentField.TypeName, StringComparison.Ordinal) ||
                baselineField.IsArray != currentField.IsArray)
                Add(ProtocolCompatibilityChangeKind.WireFieldTypeChanged, ProtocolCompatibilitySeverity.Breaking,
                    $"Wire schema '{wireType}' field '{currentField.Name}' (id {currentField.Id}) type changed from " +
                    $"{FormatFieldType(baselineField)} to {FormatFieldType(currentField)}.");

            if (baselineField.IsOptional != currentField.IsOptional)
                Add(ProtocolCompatibilityChangeKind.WireFieldRequirednessChanged,
                    currentField.IsRequired ? ProtocolCompatibilitySeverity.Breaking : ProtocolCompatibilitySeverity.Compatible,
                    $"Wire schema '{wireType}' field '{currentField.Name}' (id {currentField.Id}) changed from " +
                    $"{(baselineField.IsOptional ? "optional" : "required")} to " +
                    $"{(currentField.IsOptional ? "optional" : "required")}.");
        }
    }

    private static string FormatFieldType(ProtocolCompatibilityBaselineWireField field) =>
        field.TypeName.Length > 0
            ? field.TypeName + (field.IsArray ? "[]" : string.Empty)
            : field.ScalarType + (field.IsArray ? "[]" : string.Empty);

    private static string FormatFieldType(WireFieldIr field) =>
        field.IsCustomType
            ? field.TypeName + (field.IsArray ? "[]" : string.Empty)
            : field.ScalarType + (field.IsArray ? "[]" : string.Empty);

    private static string FormatReference(string value) =>
        string.IsNullOrEmpty(value) ? "(none)" : $"'{value}'";

    private static Dictionary<TKey, TValue> IndexBy<TValue, TKey>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> key,
        Func<TKey, string> duplicateMessage)
        where TKey : notnull
    {
        var index = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            var id = key(value);
            if (!index.TryAdd(id, value))
                throw new InvalidDataException(duplicateMessage(id));
        }

        return index;
    }
}
