using System;
using System.IO;
using System.Text;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>Deterministic, bounded wire format for pipeline progress; no runtime object references.</summary>
    internal static class PipelineRollbackStateCodec
    {
        private const int MaxPhases = 4096;
        private const int MaxDepth = 64;
        private const int MaxValues = 4096;
        private const int MaxPayloadBytes = 4 * 1024 * 1024;

        public static byte[] Serialize(PipelineRunRollbackState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Version != PipelineRunRollbackState.CurrentVersion ||
                state.ConfigName == null || state.ConfigName.Length > 512)
                throw new InvalidDataException("Invalid pipeline rollback header.");
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(state.Version);
            writer.Write(state.ConfigId);
            writer.Write(state.ConfigName);
            writer.Write(state.CurrentPhaseIndex);
            writer.Write(state.HasCurrentPhase);
            writer.Write(state.IsPaused);
            writer.Write(state.IsCancelled);
            var nodes = 0;
            WritePhases(writer, state.Phases, 0, ref nodes);
            writer.Flush();
            if (stream.Length > MaxPayloadBytes)
                throw new InvalidDataException("Pipeline rollback payload is too large.");
            return stream.ToArray();
        }

        public static PipelineRunRollbackState Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxPayloadBytes)
                throw new InvalidDataException("Invalid pipeline rollback payload length.");
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var state = new PipelineRunRollbackState
            {
                Version = reader.ReadInt32(),
                ConfigId = reader.ReadInt32(),
                ConfigName = reader.ReadString(),
                CurrentPhaseIndex = reader.ReadInt32(),
                HasCurrentPhase = reader.ReadBoolean(),
                IsPaused = reader.ReadBoolean(),
                IsCancelled = reader.ReadBoolean()
            };
            if (state.Version != PipelineRunRollbackState.CurrentVersion || state.ConfigName.Length > 512)
                throw new InvalidDataException("Unsupported pipeline rollback version.");
            var nodes = 0;
            state.Phases = ReadPhases(reader, 0, ref nodes);
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Trailing bytes in pipeline rollback payload.");
            return state;
        }

        private static void WritePhases(BinaryWriter writer, PipelinePhaseRollbackState[] phases,
            int depth, ref int nodes)
        {
            if (phases == null || depth > MaxDepth || phases.Length > MaxPhases ||
                nodes + phases.Length > MaxPhases)
                throw new InvalidDataException("Invalid pipeline rollback phase tree.");
            nodes += phases.Length;
            writer.Write(phases.Length);
            foreach (var phase in phases)
            {
                if (phase == null || string.IsNullOrEmpty(phase.PhaseType) ||
                    phase.PhaseType.Length > 512 || (phase.PhaseId?.Length ?? 0) > 512)
                    throw new InvalidDataException("Invalid pipeline rollback phase identity.");
                writer.Write(phase.PhaseId ?? string.Empty);
                writer.Write(phase.PhaseType);
                writer.Write(phase.IsComplete);
                WriteValues(writer, phase.Integers);
                WriteValues(writer, phase.Longs);
                WriteValues(writer, phase.Numbers);
                WritePhases(writer, phase.Children, depth + 1, ref nodes);
            }
        }

        private static PipelinePhaseRollbackState[] ReadPhases(BinaryReader reader, int depth, ref int nodes)
        {
            if (depth > MaxDepth) throw new InvalidDataException("Pipeline rollback phase tree is too deep.");
            var count = ReadCount(reader, MaxPhases);
            if (nodes + count > MaxPhases) throw new InvalidDataException("Pipeline rollback phase tree is too large.");
            nodes += count;
            var phases = new PipelinePhaseRollbackState[count];
            for (int i = 0; i < count; i++)
            {
                var phase = new PipelinePhaseRollbackState
                {
                    PhaseId = reader.ReadString(),
                    PhaseType = reader.ReadString(),
                    IsComplete = reader.ReadBoolean(),
                    Integers = ReadIntegers(reader),
                    Longs = ReadLongs(reader),
                    Numbers = ReadNumbers(reader)
                };
                if (phase.PhaseId.Length > 512 || phase.PhaseType.Length == 0 || phase.PhaseType.Length > 512)
                    throw new InvalidDataException("Invalid pipeline rollback phase identity.");
                phase.Children = ReadPhases(reader, depth + 1, ref nodes);
                phases[i] = phase;
            }
            return phases;
        }

        private static void WriteValues(BinaryWriter writer, int[] values)
        {
            if (values == null || values.Length > MaxValues) throw new InvalidDataException("Invalid integer state.");
            writer.Write(values.Length);
            foreach (var value in values) writer.Write(value);
        }

        private static void WriteValues(BinaryWriter writer, long[] values)
        {
            if (values == null || values.Length > MaxValues) throw new InvalidDataException("Invalid long state.");
            writer.Write(values.Length);
            foreach (var value in values) writer.Write(value);
        }

        private static void WriteValues(BinaryWriter writer, double[] values)
        {
            if (values == null || values.Length > MaxValues) throw new InvalidDataException("Invalid numeric state.");
            writer.Write(values.Length);
            foreach (var value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new InvalidDataException("Pipeline rollback state contains a non-finite value.");
                writer.Write(value);
            }
        }

        private static int[] ReadIntegers(BinaryReader reader)
        {
            var values = new int[ReadCount(reader, MaxValues)];
            for (int i = 0; i < values.Length; i++) values[i] = reader.ReadInt32();
            return values;
        }

        private static long[] ReadLongs(BinaryReader reader)
        {
            var values = new long[ReadCount(reader, MaxValues)];
            for (int i = 0; i < values.Length; i++) values[i] = reader.ReadInt64();
            return values;
        }

        private static double[] ReadNumbers(BinaryReader reader)
        {
            var values = new double[ReadCount(reader, MaxValues)];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadDouble();
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    throw new InvalidDataException("Pipeline rollback state contains a non-finite value.");
            }
            return values;
        }

        private static int ReadCount(BinaryReader reader, int max)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max) throw new InvalidDataException("Invalid pipeline rollback state count.");
            return count;
        }
    }
}
