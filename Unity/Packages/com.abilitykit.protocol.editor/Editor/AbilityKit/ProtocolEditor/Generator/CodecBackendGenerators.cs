using System;
using System.Collections.Generic;
using AbilityKit.ProtocolEditor.Schema;

namespace AbilityKit.ProtocolEditor.Generator
{
    internal static class CodecBackendGenerators
    {
        private static readonly Dictionary<ProtocolDefinition.CodecBackend, ICodecBackendGenerator> _generators =
            new();

        static CodecBackendGenerators()
        {
            Register(new CustomBinaryCodecBackendGenerator());
            Register(new ProtobufCodecBackendGenerator());
        }

        public static void Register(ICodecBackendGenerator generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            _generators[generator.Backend] = generator;
        }

        public static bool TryGet(ProtocolDefinition.CodecBackend backend, out ICodecBackendGenerator generator)
        {
            return _generators.TryGetValue(backend, out generator);
        }
    }
}
