using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSnapshotRouter))]
    [WorldService(typeof(IWorldStateSnapshotProvider))]
    [WorldService(typeof(IWorldStateSnapshotBatchProvider))]
    [WorldService(typeof(IMobaSnapshotHealthProvider))]
    public sealed class MobaSnapshotRouter : IWorldStateSnapshotBatchProvider, IMobaSnapshotBatchProvider, IMobaSnapshotHealthProvider, IWorldInitializable
    {
        private const string WarningNoEmitters = "snapshot.no.emitters";

        private List<IMobaSnapshotEmitter> _emitters;
        private List<MobaSnapshotEmitterHealthEntry> _emitterHealthEntries;
        private List<string> _missingRequiredEmitters;
        private MobaSnapshotOutputContract _outputContract;
        private MobaSnapshotContractProfileRegistry _contractProfiles;
        private MobaGameplayConfigSettings _gameplaySettings;
        private IMobaBattleDiagnosticsService _diagnostics;
        private long _singleRequests;
        private long _batchRequests;
        private long _hitCount;
        private long _emptyCount;
        private int _lastFrame;
        private int _lastSnapshotOpCode;
        private int _lastBatchSnapshotCount;
        private bool _usedAttributeRegistry;

        public MobaSnapshotRouter()
        {
            _emitters = new List<IMobaSnapshotEmitter>(8);
            _emitterHealthEntries = new List<MobaSnapshotEmitterHealthEntry>(8);
            _missingRequiredEmitters = new List<string>(4);
            _lastFrame = -1;
            _lastSnapshotOpCode = 0;
            _lastBatchSnapshotCount = 0;
        }
 
        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _diagnostics);
            services?.TryResolve(out _contractProfiles);
            services?.TryResolve(out _gameplaySettings);

            var registry = MobaSnapshotEmitterRegistry.CreateDefault();
            var resolved = registry.ResolveEmitters(services);
            _emitters = resolved;
            RebuildEmitterHealthEntries();
            RebuildOutputContractHealth();
            _usedAttributeRegistry = resolved.Count > 0;

            RecordEmitterCount();
        }
 
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            ValidateFrame(frame);

            _singleRequests++;
            _lastFrame = frame.Value;
            _diagnostics?.Counter(MobaBattleDiagnosticMetric.SnapshotRequest);

            for (int i = 0; i < _emitters.Count; i++)
            {
                if (!_emitters[i].TryGetSnapshot(frame, out snapshot)) continue;

                RecordHit(snapshot.OpCode, 1);
                return true;
            }
 
            snapshot = default;
            RecordEmpty();
            return false;
        }
 
        public int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            ValidateFrame(frame);

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (maxSnapshots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSnapshots), maxSnapshots, "maxSnapshots must be positive.");
            }

            _batchRequests++;
            _lastFrame = frame.Value;
            _diagnostics?.Counter(MobaBattleDiagnosticMetric.SnapshotBatchRequest);
 
            int count = 0;
            int lastOpCode = 0;
            for (int i = 0; i < _emitters.Count && count < maxSnapshots; i++)
            {
                if (!_emitters[i].TryGetSnapshot(frame, out WorldStateSnapshot snapshot)) continue;
 
                snapshots.Add(snapshot);
                count++;
                lastOpCode = snapshot.OpCode;
            }

            if (count > 0) RecordHit(lastOpCode, count);
            else RecordEmpty();
 
            return count;
        }

        private static void ValidateFrame(FrameIndex frame)
        {
            if (frame.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Value, "frame must be non-negative.");
            }
        }

        public MobaSnapshotRouterHealth GetHealth()
        {
            RebuildOutputContractHealth();
            var requiredCount = _outputContract != null ? _outputContract.RequiredEmitters.Count : 0;
            var missingCount = _missingRequiredEmitters != null ? _missingRequiredEmitters.Count : 0;
            var health = new MobaSnapshotRouterHealth(_emitters.Count, requiredCount, missingCount, _singleRequests, _batchRequests, _hitCount, _emptyCount, _lastFrame, _lastSnapshotOpCode, _lastBatchSnapshotCount, _usedAttributeRegistry, _emitterHealthEntries, _missingRequiredEmitters);
            _diagnostics?.RecordSnapshotRouterHealth(in health);
            return health;
        }

        private void RecordHit(int opCode, int batchCount)
        {
            _hitCount++;
            _lastSnapshotOpCode = opCode;
            _lastBatchSnapshotCount = batchCount;
            _diagnostics?.Counter(MobaBattleDiagnosticMetric.SnapshotHit);
            _diagnostics?.Gauge(MobaBattleDiagnosticMetric.SnapshotBatchSize, batchCount);
            RecordSnapshotHealth();
        }

        private void RecordEmpty()
        {
            _emptyCount++;
            _lastBatchSnapshotCount = 0;
            _diagnostics?.Counter(MobaBattleDiagnosticMetric.SnapshotEmpty);
            RecordSnapshotHealth();
        }

        private void RebuildEmitterHealthEntries()
        {
            _emitterHealthEntries.Clear();
            if (_emitters == null) return;

            for (int i = 0; i < _emitters.Count; i++)
            {
                var emitter = _emitters[i];
                if (emitter == null) continue;

                _emitterHealthEntries.Add(new MobaSnapshotEmitterHealthEntry(emitter.GetType()));
            }
        }

        private void RebuildOutputContractHealth()
        {
            _missingRequiredEmitters.Clear();
            _outputContract = null;

            var gameplayId = _gameplaySettings != null
                ? _gameplaySettings.DefaultGameplayId
                : 0;
            var profileError = "snapshot contract profile registry is unavailable";
            if (_contractProfiles == null ||
                !_contractProfiles.TryResolve(gameplayId, out _outputContract, out profileError))
            {
                _missingRequiredEmitters.Add(profileError ?? "snapshot contract profile registry is unavailable");
                return;
            }

            var required = _outputContract.RequiredEmitters;
            for (int i = 0; i < required.Count; i++)
            {
                var contract = required[i];
                if (HasEmitter(contract.EmitterType)) continue;

                _missingRequiredEmitters.Add($"{contract.Name}:{contract.OpCode}:{contract.EmitterType.Name}");
            }
        }

        private bool HasEmitter(Type emitterType)
        {
            if (emitterType == null || _emitters == null) return false;

            for (int i = 0; i < _emitters.Count; i++)
            {
                var emitter = _emitters[i];
                if (emitter != null && emitter.GetType() == emitterType) return true;
            }

            return false;
        }

        private void RecordEmitterCount()
        {
            var count = _emitters != null ? _emitters.Count : 0;
            _diagnostics?.Gauge(MobaBattleDiagnosticMetric.SnapshotEmitterCount, count);
            RecordSnapshotHealth();
            if (count == 0)
            {
                _diagnostics?.Warning(WarningNoEmitters, "[MobaSnapshotRouter] No snapshot emitters resolved; battle state output will be empty.");
            }
        }
 
        private void RecordSnapshotHealth()
        {
            if (_diagnostics == null) return;

            var requiredCount = _outputContract != null ? _outputContract.RequiredEmitters.Count : 0;
            var missingCount = _missingRequiredEmitters != null ? _missingRequiredEmitters.Count : 0;
            var health = new MobaSnapshotRouterHealth(_emitters.Count, requiredCount, missingCount, _singleRequests, _batchRequests, _hitCount, _emptyCount, _lastFrame, _lastSnapshotOpCode, _lastBatchSnapshotCount, _usedAttributeRegistry, _emitterHealthEntries, _missingRequiredEmitters);
            _diagnostics.RecordSnapshotRouterHealth(in health);
        }
 
        public void Dispose()
        {
            _diagnostics = null;
            _contractProfiles = null;
            _gameplaySettings = null;
            _outputContract = null;
            _emitters?.Clear();
            _emitterHealthEntries?.Clear();
            _missingRequiredEmitters?.Clear();
        }
    }
}
