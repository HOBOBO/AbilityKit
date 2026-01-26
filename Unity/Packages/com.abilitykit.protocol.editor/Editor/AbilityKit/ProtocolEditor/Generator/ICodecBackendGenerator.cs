using System.Text;
using AbilityKit.ProtocolEditor.Schema;

namespace AbilityKit.ProtocolEditor.Generator
{
    internal interface ICodecBackendGenerator
    {
        ProtocolDefinition.CodecBackend Backend { get; }

        void AppendGlueInvocation(StringBuilder sb, ProtocolDefinition def, ProtocolDefinition.MessageDefinition msg,
            string backendClassName, string methodName, string payloadType, int indent);

        void AppendPartialDeclarations(StringBuilder sb, ProtocolDefinition def, ProtocolDefinition.MessageDefinition msg,
            string backendClassName, string methodName, string payloadType, int indent);

        void GenerateImplementationStubs(ProtocolDefinition def, string outputFolder, string @namespace);
    }
}
