using System.Globalization;
using System.Text;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>
/// Emits the small typed API layer that sits above the transport SDK. The generated surface keeps
/// protocol identities in the catalog while leaving serialization, transport and business logic
/// supplied by the consuming project.
/// </summary>
public static class ProtocolApiEmitter
{
    public static string EmitClients(
        IReadOnlyList<ProtocolCatalogIr> catalogs,
        string targetNamespace,
        string? sourceComment = null)
    {
        var builder = Header(targetNamespace, sourceComment);
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using AbilityKit.Network.Sdk;");
        builder.AppendLine("using AbilityKit.Protocol;");
        builder.AppendLine("using AbilityKit.Protocol.Catalog;");
        builder.AppendLine();

        foreach (var catalog in catalogs)
        {
            var className = ToCatalogTypeName(catalog) + "ProtocolClient";
            builder.Append("    public sealed class ").Append(className).AppendLine(" : IDisposable");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly NetworkProtocolAgent _agent;");
            builder.AppendLine("        private readonly ProtocolCatalogRegistry _catalog;");
            builder.Append("        public const string CatalogId = ").Append(Literal(catalog.CatalogId)).AppendLine(";");
            builder.AppendLine();
            builder.Append("        public ").Append(className).AppendLine("(NetworkSdkClient sdk, global::AbilityKit.Protocol.INetworkProtocolCodec codec, ProtocolCatalogRegistry catalog)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (sdk == null) throw new ArgumentNullException(nameof(sdk));");
            builder.AppendLine("            _agent = sdk.CreateProtocolAgent(codec ?? throw new ArgumentNullException(nameof(codec))); ");
            builder.AppendLine("            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));");
            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request))
            {
                var response = catalog.Messages.FirstOrDefault(m =>
                    string.Equals(m.Id, message.ResponseId, StringComparison.Ordinal));
                if (response == null) continue;
                var method = ToMethodName(message.Id, "Async");
                builder.Append("        public Task<global::").Append(response.PayloadType)
                    .Append("> ").Append(method).AppendLine("(");
                builder.Append("            global::").Append(message.PayloadType)
                    .Append(" request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)").AppendLine();
                builder.AppendLine("        {");
                builder.Append("            return _agent.RequestAsync<global::").Append(message.PayloadType)
                    .Append(", global::").Append(response.PayloadType).Append(">(");
                builder.AppendLine("                _catalog, CatalogId, " + Literal(message.Id) + ", request, timeout, cancellationToken);");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Push))
            {
                var method = "Subscribe" + ToTypeName(message.Id);
                builder.Append("        public IDisposable ").Append(method).Append("(Action<global::")
                    .Append(message.PayloadType).AppendLine("> handler)");
                builder.AppendLine("        {");
                builder.Append("            return _agent.Subscribe<global::").Append(message.PayloadType)
                    .AppendLine(">(_catalog, CatalogId, " + Literal(message.Id) + ", handler);" );
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine("        public void Dispose() => _agent.Dispose();");
            builder.AppendLine("    }");
            builder.AppendLine();

            var pushSenderName = ToCatalogTypeName(catalog) + "ProtocolPushSender";
            var pushes = catalog.Messages.Where(m => m.Kind == IrPacketKind.Push).ToArray();
            if (pushes.Length > 0)
            {
                builder.Append("    public sealed class ").Append(pushSenderName).AppendLine();
                builder.AppendLine("    {");
                builder.AppendLine("        private readonly global::AbilityKit.Protocol.INetworkProtocolCodec _codec;");
                builder.Append("        public ").Append(pushSenderName).AppendLine("(global::AbilityKit.Protocol.INetworkProtocolCodec codec)");
                builder.AppendLine("        {");
                builder.AppendLine("            _codec = codec ?? throw new ArgumentNullException(nameof(codec));");
                builder.AppendLine("        }");
                foreach (var message in pushes)
                {
                    builder.Append("        public void Send").Append(ToMethodName(message.Id, string.Empty))
                        .Append("(global::AbilityKit.Network.Host.IServerNetworkSession session, global::").Append(message.PayloadType).AppendLine(" message)");
                    builder.AppendLine("        {");
                    builder.AppendLine("            if (session == null) throw new ArgumentNullException(nameof(session));");
                    builder.AppendLine("            var payload = _codec.Serialize(in message);");
                    builder.Append("            session.SendPush(").Append(message.OpCode.ToString(CultureInfo.InvariantCulture)).AppendLine("u, payload);");
                    builder.AppendLine("        }");
                }
                builder.AppendLine("    }");
                builder.AppendLine();
            }
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    public static string EmitHandlers(
        IReadOnlyList<ProtocolCatalogIr> catalogs,
        string targetNamespace,
        string? sourceComment = null)
    {
        var builder = Header(targetNamespace, sourceComment);
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using AbilityKit.Network.Host;");
        builder.AppendLine("using AbilityKit.Network.Runtime;");
        builder.AppendLine("using AbilityKit.Network.Protocol;");
        builder.AppendLine("using AbilityKit.Network.Sdk;");
        builder.AppendLine("using AbilityKit.Protocol;");
        builder.AppendLine();

        foreach (var catalog in catalogs)
        {
            var typeName = ToCatalogTypeName(catalog);
            var interfaceName = "I" + typeName + "ProtocolHandlers";
            var adapterName = typeName + "ProtocolRequestHandler";
            builder.Append("    public interface ").Append(interfaceName).AppendLine();
            builder.AppendLine("    {");
            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request))
            {
                var response = catalog.Messages.FirstOrDefault(m =>
                    string.Equals(m.Id, message.ResponseId, StringComparison.Ordinal));
                if (response == null) continue;
                builder.Append("        Task<global::").Append(response.PayloadType).Append("> ")
                    .Append(ToMethodName(message.Id, "Async"));
                builder.Append("(global::").Append(message.PayloadType)
                    .Append(" request, IServerNetworkSession session, CancellationToken cancellationToken);");
                builder.AppendLine();
            }
            builder.AppendLine("    }");
            builder.AppendLine();

            var baseName = typeName + "ProtocolHandlersBase";
            builder.Append("    public abstract class ").Append(baseName).Append(" : ").Append(interfaceName).AppendLine();
            builder.AppendLine("    {");
            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request))
            {
                var response = catalog.Messages.FirstOrDefault(m => string.Equals(m.Id, message.ResponseId, StringComparison.Ordinal));
                if (response == null) continue;
                builder.Append("        public virtual Task<global::").Append(response.PayloadType).Append("> ")
                    .Append(ToMethodName(message.Id, "Async"));
                builder.Append("(global::").Append(message.PayloadType)
                    .AppendLine(" request, IServerNetworkSession session, CancellationToken cancellationToken) => throw new NotSupportedException();");
            }
            builder.AppendLine("    }");
            builder.AppendLine();

            builder.Append("    public sealed class ").Append(adapterName).AppendLine(" : IAsyncServerRequestHandler");
            builder.AppendLine("    {");
            builder.Append("        private readonly ").Append(interfaceName).AppendLine(" _handlers;");
            builder.AppendLine("        private readonly global::AbilityKit.Protocol.INetworkProtocolCodec _codec;");
            builder.AppendLine("        private readonly IRequestResponseEnvelopeEncoder _envelope;");
            builder.AppendLine();
            builder.Append("        public ").Append(adapterName).Append("(").Append(interfaceName)
                .AppendLine(" handlers, global::AbilityKit.Protocol.INetworkProtocolCodec codec, IRequestResponseEnvelopeEncoder? envelope = null)");
            builder.AppendLine("        {");
            builder.AppendLine("            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));");
            builder.AppendLine("            _codec = codec ?? throw new ArgumentNullException(nameof(codec));");
            builder.AppendLine("            _envelope = envelope ?? RawRequestResponseEnvelopeCodec.Instance;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public Task HandleAsync(IServerNetworkSession session, NetworkPacketHeader header, ArraySegment<byte> payload, CancellationToken cancellationToken)");
            builder.AppendLine("        {");
            builder.AppendLine("            return header.OpCode switch");
            builder.AppendLine("            {");
            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request))
            {
                var response = catalog.Messages.FirstOrDefault(m => string.Equals(m.Id, message.ResponseId, StringComparison.Ordinal));
                if (response == null) continue;
                builder.Append("                ").Append(message.OpCode.ToString(CultureInfo.InvariantCulture)).Append("u => Handle")
                    .Append(ToMethodName(message.Id, "Async")).Append("(session, header, payload, cancellationToken),").AppendLine();
            }
            builder.AppendLine("                _ => Task.FromException(new InvalidOperationException($\"Unknown protocol opcode {header.OpCode}.\")),");
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        public AsyncServerRequestRouter Register(AsyncServerRequestRouter router)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (router == null) throw new ArgumentNullException(nameof(router));");
            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request)
                         .Where(m => catalog.Messages.Any(candidate => string.Equals(candidate.Id, m.ResponseId, StringComparison.Ordinal))))
                builder.Append("            router.Register(").Append(message.OpCode.ToString(CultureInfo.InvariantCulture)).AppendLine("u, this);");
            builder.AppendLine("            return router;");
            builder.AppendLine("        }");
            builder.AppendLine();
            foreach (var message in catalog.Messages.Where(m => m.Kind == IrPacketKind.Request))
            {
                var response = catalog.Messages.FirstOrDefault(m => string.Equals(m.Id, message.ResponseId, StringComparison.Ordinal));
                if (response == null) continue;
                var method = ToMethodName(message.Id, "Async");
                builder.Append("        private async Task Handle").Append(method).AppendLine("(IServerNetworkSession session, NetworkPacketHeader header, ArraySegment<byte> payload, CancellationToken cancellationToken)");
                builder.AppendLine("        {");
                builder.Append("            var request = _codec.Deserialize<global::").Append(message.PayloadType).AppendLine(">(payload);");
                builder.Append("            var response = await _handlers.").Append(method).AppendLine("(request, session, cancellationToken).ConfigureAwait(false);");
                builder.Append("            var body = _codec.Serialize(in response);").AppendLine();
                builder.AppendLine("            session.SendResponse(header.OpCode, header.Seq, _envelope.EncodeSuccess(header.OpCode, header.Seq, body));");
                builder.AppendLine("        }");
                builder.AppendLine();
            }
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static StringBuilder Header(string targetNamespace, string? sourceComment)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        if (!string.IsNullOrWhiteSpace(sourceComment)) builder.Append("// Sources: ").AppendLine(sourceComment);
        builder.AppendLine("#nullable enable");
        builder.Append("namespace ").Append(targetNamespace).AppendLine();
        builder.AppendLine("{");
        return builder;
    }

    private static string ToMethodName(string id, string suffix)
    {
        var result = ToTypeName(id);
        return result.EndsWith(suffix, StringComparison.Ordinal) ? result : result + suffix;
    }

    private static string ToCatalogTypeName(ProtocolCatalogIr catalog)
    {
        var projectSegments = (catalog.ProjectId ?? string.Empty)
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
        var project = projectSegments.Length == 0 ? string.Empty : projectSegments[^1];
        return ToTypeName(project) + ToTypeName(catalog.Domain);
    }

    private static string ToTypeName(string value)
    {
        value = value ?? string.Empty;
        var segments = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && (string.Equals(segments[^1], "request", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(segments[^1], "response", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(segments[^1], "push", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(segments[^1], "event", StringComparison.OrdinalIgnoreCase)))
            value = string.Join('.', segments, 0, segments.Length - 1);
        var builder = new StringBuilder();
        var upper = true;
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch)) { upper = true; continue; }
            builder.Append(upper ? char.ToUpperInvariant(ch) : ch);
            upper = false;
        }
        if (builder.Length == 0) builder.Append("Protocol");
        if (char.IsDigit(builder[0])) builder.Insert(0, '_');
        return builder.ToString();
    }

    private static string Literal(string value) =>
        "\"" + (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
