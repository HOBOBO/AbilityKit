using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaCharacterHfsmRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10017;
        private readonly MobaActorRegistry _actors;
        private readonly StateMachineDefinition _definition;

        public MobaCharacterHfsmRollbackProvider(MobaActorRegistry actors,
            StateMachineDefinition definition = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _definition = definition ?? MobaCharacterHfsmProfile.CreateDefinition();
        }

        public int Key => DefaultKey;
        public string Name => "CharacterHfsm";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = new List<Entry>();
            var ids = _actors.CopyActorIdsInOrder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (!_actors.TryGet(ids[i], out var actor) || !actor.hasCharacterHfsm) continue;
                var snapshot = actor.characterHfsm.Runtime.CaptureSnapshot();
                var action = snapshot.Action;
                entries.Add(new Entry
                {
                    ActorId = ids[i], Machine = snapshot.Machine, Path = action.Path,
                    InstanceId = action.InstanceId, StartFrame = action.StartFrame,
                    LocalFrame = action.LocalFrame, CastInstanceId = action.CastInstanceId,
                    SkillId = action.SkillId
                });
            }
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entries));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            var entries = JsonConvert.DeserializeObject<List<Entry>>(Encoding.UTF8.GetString(payload))
                ?? throw new InvalidOperationException("Character HFSM recovery payload is invalid.");
            var restored = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry?.Machine == null || entry.ActorId <= 0 || !restored.Add(entry.ActorId))
                    throw new InvalidOperationException("Character HFSM recovery contains a duplicate or null entry.");
                if (entry.Machine.Frame != frame.Value)
                    throw new InvalidOperationException("Character HFSM recovery frame differs from actor snapshot frame.");
                var snapshot = new MobaCharacterHfsmSnapshot
                {
                    Machine = entry.Machine,
                    Action = new MobaCharacterActionState(entry.Path, entry.InstanceId,
                        entry.StartFrame, entry.LocalFrame, entry.CastInstanceId, entry.SkillId)
                };
                using (var candidate = new MobaCharacterHfsmRuntime(entry.ActorId,
                           _definition, 0, Fixed64.Zero))
                {
                    if (_actors.TryGet(entry.ActorId, out var existing) && existing.hasCharacterHfsm &&
                        (existing.characterHfsm.Runtime == null ||
                         existing.characterHfsm.Runtime.DefinitionHash != candidate.DefinitionHash))
                        throw new InvalidOperationException("Character HFSM recovery definition differs from actor machine.");
                    candidate.RestoreSnapshot(snapshot);
                }
            }
            foreach (var entry in entries)
            {
                if (!_actors.TryGet(entry.ActorId, out var actor)) continue;
                var snapshot = new MobaCharacterHfsmSnapshot
                {
                    Machine = entry.Machine,
                    Action = new MobaCharacterActionState(entry.Path, entry.InstanceId,
                        entry.StartFrame, entry.LocalFrame, entry.CastInstanceId, entry.SkillId)
                };
                if (!actor.hasCharacterHfsm)
                    actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(entry.ActorId,
                        _definition, entry.Machine.Frame,
                        Fixed64.FromRaw(entry.Machine.TimeRaw)));
                actor.characterHfsm.Runtime.RestoreSnapshot(snapshot);
            }
            // A full baseline may contain an actor with no character machine yet.
            foreach (var id in _actors.CopyActorIdsInOrder())
            {
                if (restored.Contains(id)) continue;
                if (_actors.TryGet(id, out var actor) && actor.hasCharacterHfsm)
                    actor.RemoveCharacterHfsm();
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var bytes = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(bytes.Length);
            foreach (var value in bytes) hash.AddByte(value);
        }

        private sealed class Entry
        {
            public int ActorId;
            public RuntimeSnapshot Machine;
            public string Path;
            public long InstanceId;
            public int StartFrame;
            public int LocalFrame;
            public long CastInstanceId;
            public int SkillId;
        }
    }

    // Wire compression is separate from the raw rollback provider so local checkpoints stay unchanged.
    public static class MobaCharacterHfsmWireCodec
    {
        public const int CompressedOpCode = 11017;
        public const int CompressedSchemaVersion = 3;
        public const int MaxDecodedBytes = 4 * 1024 * 1024;

        public static byte[] Encode(byte[] raw, out int opCode)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            opCode = MobaCharacterHfsmRollbackProvider.DefaultKey;
            if (raw.Length < 128 || raw.Length > MaxDecodedBytes) return raw;

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true))
                    gzip.Write(raw, 0, raw.Length);
                var compressed = output.ToArray();
                if (compressed.Length > raw.Length - 16) return raw;
                opCode = CompressedOpCode;
                return compressed;
            }
        }

        public static byte[] Decode(int opCode, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidDataException("Character HFSM wire payload is empty.");
            if (opCode == MobaCharacterHfsmRollbackProvider.DefaultKey) return payload;
            if (opCode != CompressedOpCode)
                throw new InvalidDataException("Unknown character HFSM wire encoding.");

            using (var input = new MemoryStream(payload))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > MaxDecodedBytes)
                        throw new InvalidDataException("Character HFSM wire payload exceeds the decoded size limit.");
                    output.Write(buffer, 0, count);
                }
                if (output.Length == 0)
                    throw new InvalidDataException("Character HFSM wire payload decoded to an empty baseline.");
                return output.ToArray();
            }
        }
    }
}
