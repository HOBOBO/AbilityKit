using System;
using System.IO;
using System.Text;
using AbilityKit.ProtocolEditor.Schema;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ProtocolEditor.Generator
{
    internal sealed class CustomBinaryCodecBackendGenerator : ICodecBackendGenerator
    {
        public ProtocolDefinition.CodecBackend Backend => ProtocolDefinition.CodecBackend.CustomBinary;

        public void AppendGlueInvocation(StringBuilder sb, ProtocolDefinition def, ProtocolDefinition.MessageDefinition msg,
            string backendClassName, string methodName, string payloadType, int indent)
        {
            var pad = new string(' ', indent);

            switch (msg.Channel)
            {
                case ProtocolDefinition.ChannelKind.SnapshotDecoder:
                    sb.AppendLine($"{pad}{backendClassName}.TryDecode_{methodName}(in snap, ref handled, ref value);");
                    break;

                case ProtocolDefinition.ChannelKind.SnapshotCmdHandler:
                    sb.AppendLine($"{pad}{backendClassName}.Handle_{methodName}(owner, packet, value, ref handled);");
                    break;

                case ProtocolDefinition.ChannelKind.SnapshotPipelineStage:
                    sb.AppendLine($"{pad}{backendClassName}.Stage_{methodName}(owner, packet, value, ref handled);");
                    break;
            }
        }

        public void AppendPartialDeclarations(StringBuilder sb, ProtocolDefinition def, ProtocolDefinition.MessageDefinition msg,
            string backendClassName, string methodName, string payloadType, int indent)
        {
            var pad = new string(' ', indent);

            switch (msg.Channel)
            {
                case ProtocolDefinition.ChannelKind.SnapshotDecoder:
                    sb.AppendLine($"{pad}static partial void TryDecode_{methodName}(in WorldStateSnapshot snap, ref bool handled, ref {payloadType} value);");
                    sb.AppendLine();
                    break;

                case ProtocolDefinition.ChannelKind.SnapshotCmdHandler:
                    sb.AppendLine($"{pad}static partial void Handle_{methodName}(object owner, FramePacket packet, {payloadType} value, ref bool handled);");
                    sb.AppendLine();
                    break;

                case ProtocolDefinition.ChannelKind.SnapshotPipelineStage:
                    sb.AppendLine($"{pad}static partial void Stage_{methodName}(object owner, FramePacket packet, {payloadType} value, ref bool handled);");
                    sb.AppendLine();
                    break;
            }
        }

        public void GenerateImplementationStubs(ProtocolDefinition def, string outputFolder, string @namespace)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required", nameof(outputFolder));
            if (string.IsNullOrWhiteSpace(@namespace)) throw new ArgumentException("Namespace is required", nameof(@namespace));
            if (string.IsNullOrWhiteSpace(def.RegistryId)) throw new ArgumentException("RegistryId is required in ProtocolDefinition", nameof(def));

            Directory.CreateDirectory(outputFolder);

            var className = $"SnapshotCustomBinary_{SanitizeIdentifier(def.RegistryId)}";
            var fileName = $"SnapshotCustomBinary.{SanitizeIdentifier(def.RegistryId)}.cs";

            var filePath = Path.Combine(outputFolder, fileName);
            if (File.Exists(filePath))
            {
                Debug.Log($"Skip (already exists): {filePath}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("using AbilityKit.Ability.Host;");
            sb.AppendLine();
            sb.AppendLine($"namespace {@namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    internal static partial class {className}");
            sb.AppendLine("    {");

            foreach (var msg in def.Messages)
            {
                if (msg == null) continue;
                if (msg.Backend != ProtocolDefinition.CodecBackend.CustomBinary) continue;
                if (string.IsNullOrWhiteSpace(msg.Name)) continue;
                if (string.IsNullOrWhiteSpace(msg.PayloadTypeName)) continue;

                var methodName = SanitizeIdentifier(msg.Name);
                var payloadType = msg.PayloadTypeName.Trim();

                switch (msg.Channel)
                {
                    case ProtocolDefinition.ChannelKind.SnapshotDecoder:
                        sb.AppendLine($"        static partial void TryDecode_{methodName}(in WorldStateSnapshot snap, ref bool handled, ref {payloadType} value)");
                        sb.AppendLine("        {");
                        sb.AppendLine("            // TODO: decode snap.Payload and set handled/value");
                        sb.AppendLine("        }");
                        sb.AppendLine();
                        break;

                    case ProtocolDefinition.ChannelKind.SnapshotCmdHandler:
                        sb.AppendLine($"        static partial void Handle_{methodName}(object owner, FramePacket packet, {payloadType} value, ref bool handled)");
                        sb.AppendLine("        {");
                        sb.AppendLine("            // TODO: handle command");
                        sb.AppendLine("        }");
                        sb.AppendLine();
                        break;

                    case ProtocolDefinition.ChannelKind.SnapshotPipelineStage:
                        sb.AppendLine($"        static partial void Stage_{methodName}(object owner, FramePacket packet, {payloadType} value, ref bool handled)");
                        sb.AppendLine("        {");
                        sb.AppendLine("            // TODO: pipeline stage");
                        sb.AppendLine("        }");
                        sb.AppendLine();
                        break;
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            AssetDatabase.Refresh();
            Debug.Log($"Generated: {filePath}");
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Msg";

            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(ch);
                }
            }

            var result = sb.ToString();
            if (string.IsNullOrEmpty(result)) return "Msg";
            if (char.IsDigit(result[0])) return "Msg_" + result;
            return result;
        }
    }
}
