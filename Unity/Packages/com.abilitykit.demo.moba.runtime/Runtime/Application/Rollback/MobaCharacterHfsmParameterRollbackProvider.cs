using System;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.StateSync;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>Rollback state for source-owned HFSM parameter contributions.</summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaCharacterHfsmParameterRollbackProvider : IRollbackStateProvider,
        IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 10044;
        private readonly MobaCharacterHfsmParameterService _parameters;

        public MobaCharacterHfsmParameterRollbackProvider(MobaCharacterHfsmParameterService parameters)
        { _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters)); }

        public int Key => DefaultKey;
        public string Name => "CharacterHfsmParameters";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = _parameters.CaptureSnapshot();
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entries));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => _parameters.ValidateSnapshot(Read(payload));

        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            var entries = Read(context.RequirePayload(Key).ToArray());
            // HFSM import precedes parameters and can replace/clear declarations.
            // Validate against that target profile, never the current profile alone.
            if (context.Providers.TryGetValue(MobaCharacterHfsmRollbackProvider.DefaultKey, out var provider) &&
                provider is MobaCharacterHfsmRollbackProvider hfsm)
                hfsm.ValidateParameterRecovery(context.Frame,
                    context.RequirePayload(hfsm.Key).ToArray(), _parameters, entries);
            else
                _parameters.ValidateSnapshot(entries);
        }

        public void ImportState(FrameIndex frame, byte[] payload) => _parameters.RestoreSnapshot(Read(payload));

        private static MobaCharacterHfsmParameterService.SnapshotEntry[] Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return Array.Empty<MobaCharacterHfsmParameterService.SnapshotEntry>();
            return JsonConvert.DeserializeObject<MobaCharacterHfsmParameterService.SnapshotEntry[]>(
                Encoding.UTF8.GetString(payload)) ?? Array.Empty<MobaCharacterHfsmParameterService.SnapshotEntry>();
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var bytes = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(bytes.Length);
            for (var i = 0; i < bytes.Length; i++) hash.AddByte(bytes[i]);
        }
    }
}
