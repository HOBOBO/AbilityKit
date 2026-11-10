using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.HFSM.Runtime;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaCharacterHfsmRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10017;
        private readonly MobaActorRegistry _actors;
        private readonly StateMachineDefinition _definition;
        private readonly MobaCharacterHfsmProfileCatalog _profiles;
        private readonly MobaCharacterHfsmParameterService _parameters;
        private readonly MobaEffectExecutionService _effects;

        public MobaCharacterHfsmRollbackProvider(MobaActorRegistry actors,
            StateMachineDefinition definition = null,
            MobaCharacterHfsmParameterService parameters = null,
            MobaEffectExecutionService effects = null,
            MobaCharacterHfsmProfileCatalog profiles = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _definition = definition ?? MobaCharacterHfsmProfile.CreateGenericDefinition();
            _parameters = parameters;
            _effects = effects;
            _profiles = profiles;
        }

        public int Key => DefaultKey;
        public string Name => "CharacterHfsm";

        private void ExecuteHfsmLogicTrigger(int actorId, int triggerId, string stateId, int frame)
        {
            // Recovery fixtures can restore the authoritative HFSM without a
            // gameplay effect service. In that mode lifecycle actions remain
            // part of the state graph but have no side-effect sink; live
            // battle runtimes provide the service and execute the trigger.
            if (_effects == null) return;
            _effects.ExecuteRulePlan(triggerId, new MobaCharacterHfsmActionPayload
            {
                ActorId = actorId,
                StateId = stateId,
                Frame = frame
            });
        }
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
                    ActorId = ids[i], Machine = snapshot.Machine, Facts = snapshot.Facts, Path = action.Path,
                    InstanceId = action.InstanceId, StartFrame = action.StartFrame,
                    LocalFrame = action.LocalFrame, CastInstanceId = action.CastInstanceId,
                    SkillId = action.SkillId, Mode = snapshot.Mode,
                    BindingsFingerprint = snapshot.BindingsFingerprint,
                    AimScope = snapshot.AimScope, AimScopeInstanceId = snapshot.AimScopeInstanceId,
                    NextAimScopeSequence = snapshot.NextAimScopeSequence,
                    HasFrozenAim = snapshot.HasFrozenAim,
                    FrozenAimPosX = snapshot.FrozenAimPos.X,
                    FrozenAimPosY = snapshot.FrozenAimPos.Y,
                    FrozenAimPosZ = snapshot.FrozenAimPos.Z,
                    FrozenAimDirX = snapshot.FrozenAimDir.X,
                    FrozenAimDirY = snapshot.FrozenAimDir.Y,
                    FrozenAimDirZ = snapshot.FrozenAimDir.Z
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
                    Facts = entry.Facts,
                    Action = CreateAction(entry),
                    Mode = entry.Mode,
                    BindingsFingerprint = entry.BindingsFingerprint,
                    AimScope = entry.AimScope, AimScopeInstanceId = entry.AimScopeInstanceId,
                    NextAimScopeSequence = entry.NextAimScopeSequence,
                    HasFrozenAim = entry.HasFrozenAim,
                    FrozenAimPos = entry.GetFrozenAimPos(),
                    FrozenAimDir = entry.GetFrozenAimDir()
                };
                using (var candidate = new MobaCharacterHfsmRuntime(entry.ActorId,
                           ResolveDefinition(entry.ActorId), 0, Fixed64.Zero, registerForInspection: false,
                           parameters: _parameters, executeLogicTrigger: ExecuteHfsmLogicTrigger))
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
                    Facts = entry.Facts,
                    Action = CreateAction(entry),
                    Mode = entry.Mode,
                    BindingsFingerprint = entry.BindingsFingerprint,
                    AimScope = entry.AimScope, AimScopeInstanceId = entry.AimScopeInstanceId,
                    NextAimScopeSequence = entry.NextAimScopeSequence,
                    HasFrozenAim = entry.HasFrozenAim,
                    FrozenAimPos = entry.GetFrozenAimPos(),
                    FrozenAimDir = entry.GetFrozenAimDir()
                };
                if (!actor.hasCharacterHfsm)
                    actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(entry.ActorId,
                        ResolveDefinition(entry.ActorId), entry.Machine.Frame,
                        Fixed64.FromRaw(entry.Machine.TimeRaw), parameters: _parameters,
                        executeLogicTrigger: ExecuteHfsmLogicTrigger));
                actor.characterHfsm.Runtime.RestoreSnapshot(snapshot);
            }
            // A full baseline may contain an actor with no character machine yet.
            foreach (var id in _actors.CopyActorIdsInOrder())
            {
                if (restored.Contains(id)) continue;
                if (_actors.TryGet(id, out var actor) && actor.hasCharacterHfsm)
                    actor.RemoveAndDisposeCharacterHfsm();
                _parameters?.ClearActorProfile(id);
            }
        }

        private StateMachineDefinition ResolveDefinition(int actorId)
        {
            if (_actors.TryGet(actorId, out var actor) && actor.hasSkillLoadout)
            {
                var heroId = actor.skillLoadout.HeroId;
                var definition = _profiles?.Resolve(heroId) ??
                    MobaCharacterHfsmProfile.CreateGenericDefinition();
                _parameters?.RegisterActorProfile(actorId, definition);
                return definition;
            }
            _parameters?.RegisterActorProfile(actorId, _definition);
            return _definition;
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
            public MobaCharacterFactSnapshot Facts;
            public string Path;
            public long InstanceId;
            public int StartFrame;
            public int LocalFrame;
            public long CastInstanceId;
            public int SkillId;
            public MobaCharacterModeSnapshot Mode;
            public string BindingsFingerprint;
            public string AimScope;
            public long AimScopeInstanceId;
            public uint NextAimScopeSequence;
            public bool HasFrozenAim;
            // Keep rollback wire data as plain values. Vec3 exposes the
            // computed Normalized property, which makes Json.NET traverse a
            // self-referencing object graph.
            public float FrozenAimPosX;
            public float FrozenAimPosY;
            public float FrozenAimPosZ;
            public float FrozenAimDirX;
            public float FrozenAimDirY;
            public float FrozenAimDirZ;

            public Vec3 GetFrozenAimPos() => new Vec3(FrozenAimPosX, FrozenAimPosY, FrozenAimPosZ);
            public Vec3 GetFrozenAimDir() => new Vec3(FrozenAimDirX, FrozenAimDirY, FrozenAimDirZ);
        }

        private static MobaCharacterActionState CreateAction(Entry entry)
        {
            if (entry.Mode == null)
                return new MobaCharacterActionState(entry.Path, entry.InstanceId,
                    entry.StartFrame, entry.LocalFrame, entry.CastInstanceId, entry.SkillId);
            var mode = entry.Mode.Current;
            return new MobaCharacterActionState(entry.Path, entry.InstanceId,
                entry.StartFrame, entry.LocalFrame, entry.CastInstanceId, entry.SkillId,
                mode.ModeId, mode.InstanceId, mode.StartFrame, mode.LocalFrame);
        }
    }

    // Wire compression is separate from the raw rollback provider so local checkpoints stay unchanged.
    public static class MobaCharacterHfsmWireCodec
    {
        public const int CompressedOpCode = 11017;
        public const int CompressedSchemaVersion = 4;
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
