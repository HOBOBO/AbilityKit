using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaGameplayRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10021;
        private readonly MobaGameplayService _gameplay;
        private readonly MobaGameplayVariableService _variables;
        private readonly MobaUnitDeathSubscriber _deaths;

        public MobaGameplayRollbackProvider(MobaGameplayService gameplay, MobaGameplayVariableService variables,
            MobaUnitDeathSubscriber deaths = null)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
            _deaths = deaths;
        }

        public int Key => DefaultKey;
        public string Name => "Gameplay";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var variables = _variables.Capture();
            var keys = new int[variables.Length];
            var values = new long[variables.Length];
            for (var i = 0; i < variables.Length; i++)
            {
                keys[i] = variables[i].KeyId;
                values[i] = variables[i].RawValue;
            }
            var result = _gameplay.LastResult;
            return MemoryPackSerializer.Serialize(new MobaGameplayRollbackPayload(
                1, _gameplay.CurrentGameplayId, (int)_gameplay.Phase, _gameplay.ElapsedRaw,
                _gameplay.TimeExpired, result.Reason, result.WinTeamId, result.EndFrame,
                result.ElapsedSeconds, keys, values, _deaths?.CaptureReportedDeaths() ?? Array.Empty<int>()));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(payload);

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            var entries = new MobaGameplayVariableEntry[state.VariableKeys.Length];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = new MobaGameplayVariableEntry(state.VariableKeys[i], state.VariableValues[i]);
            _variables.Restore(entries);
            _gameplay.RestoreState(state.GameplayId, (MobaGameplayPhase)state.Phase,
                state.ElapsedRaw, state.TimeExpired, state.ResultReason,
                state.WinTeamId, state.EndFrame, state.ResultElapsedSeconds);
            _deaths?.RestoreReportedDeaths(state.ReportedDeaths);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static MobaGameplayRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing gameplay rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaGameplayRollbackPayload>(payload);
            if (state.Version != 1 || state.ElapsedRaw < 0 ||
                state.Phase < (int)MobaGameplayPhase.NotStarted || state.Phase > (int)MobaGameplayPhase.Ended ||
                state.Phase == (int)MobaGameplayPhase.Ending ||
                state.VariableKeys == null || state.VariableValues == null ||
                state.ReportedDeaths == null ||
                state.VariableKeys.Length != state.VariableValues.Length)
                throw new InvalidOperationException("Invalid gameplay rollback payload.");
            for (var i = 0; i < state.VariableKeys.Length; i++)
                if (state.VariableKeys[i] <= 0 || (i > 0 && state.VariableKeys[i - 1] >= state.VariableKeys[i]))
                    throw new InvalidOperationException("Invalid gameplay rollback variable keys.");
            for (var i = 0; i < state.ReportedDeaths.Length; i++)
                if (state.ReportedDeaths[i] <= 0 ||
                    (i > 0 && state.ReportedDeaths[i - 1] >= state.ReportedDeaths[i]))
                    throw new InvalidOperationException("Invalid gameplay rollback death records.");
            return state;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaGameplayRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int GameplayId;
        [MemoryPackOrder(2)] public readonly int Phase;
        [MemoryPackOrder(3)] public readonly long ElapsedRaw;
        [MemoryPackOrder(4)] public readonly bool TimeExpired;
        [MemoryPackOrder(5)] public readonly string ResultReason;
        [MemoryPackOrder(6)] public readonly int WinTeamId;
        [MemoryPackOrder(7)] public readonly int EndFrame;
        [MemoryPackOrder(8)] public readonly float ResultElapsedSeconds;
        [MemoryPackOrder(9)] public readonly int[] VariableKeys;
        [MemoryPackOrder(10)] public readonly long[] VariableValues;
        [MemoryPackOrder(11)] public readonly int[] ReportedDeaths;

        [MemoryPackConstructor]
        public MobaGameplayRollbackPayload(int version, int gameplayId, int phase, long elapsedRaw,
            bool timeExpired, string resultReason, int winTeamId, int endFrame, float resultElapsedSeconds,
            int[] variableKeys, long[] variableValues, int[] reportedDeaths)
        {
            Version = version;
            GameplayId = gameplayId;
            Phase = phase;
            ElapsedRaw = elapsedRaw;
            TimeExpired = timeExpired;
            ResultReason = resultReason;
            WinTeamId = winTeamId;
            EndFrame = endFrame;
            ResultElapsedSeconds = resultElapsedSeconds;
            VariableKeys = variableKeys;
            VariableValues = variableValues;
            ReportedDeaths = reportedDeaths;
        }
    }
}
